/**
 * Bridge 事件 → Store 映射
 * 职责：将后端推送的事件路由到 Zustand store 的对应 action
 */

import { useChatStore } from '../../store/useChatStore';
import type { ToolApprovalMode } from '../../store/useChatStore';
import type {
  LlmStreamDeltaPayload,
  ToolDispatchPayload,
  ToolResultPayload,
  NoticePayload,
  ErrorPayload,
  ConfigSyncPayload,
  ApprovalRequestPayload,
  AskRequestPayload,
} from '../messageContracts';
import { MessageTypes } from '../messageContracts';
import type { Bridge } from './BridgeCore';

// ============================================
// 流式 delta 批量合并缓冲（60fps 节流）
// ============================================
let _deltaBuffer: { tabId: string; text: string }[] = [];
let _deltaFlushScheduled = false;

function flushDeltaBuffer(): void {
  _deltaFlushScheduled = false;
  if (_deltaBuffer.length === 0) return;
  const byTab = new Map<string, string>();
  for (const d of _deltaBuffer) {
    byTab.set(d.tabId, (byTab.get(d.tabId) || '') + d.text);
  }
  _deltaBuffer = [];
  const s = useChatStore.getState();
  for (const [tid, combined] of byTab) {
    s.appendAssistantDelta(combined, tid);
  }
}

// ============================================
// 注册 Store 映射
// ============================================

export function registerStoreMappings(bridgeInstance: Bridge): void {
  const store = useChatStore;

  bridgeInstance.on((msg) => {
    const s = store.getState();
    // 事件路由优先级：后端携带的 tabId > streamingTabId（锁定） > undefined（fallback 到 activeTabId）
    const payload = msg.payload as Record<string, unknown> | undefined;
    const tabId = (payload?.tabId as string) || s.streamingTabId || undefined;

    switch (msg.type) {
      case MessageTypes.HostReady:
        s.setBridgeConnected(true);
        break;

      case MessageTypes.ConfigSync: {
        const p = msg.payload as ConfigSyncPayload;
        s.setConfig({
          currentProvider: p.currentProvider || p.provider,
          currentModel: p.currentModel || p.model,
          providers: p.providers || [],
        });
        // 同步版本号到全局变量（供 AboutSection 等使用）
        if ((p as any).version) {
          window.__E3D_VERSION__ = (p as any).version;
          window.__E3D_ABOUT_URL__ = (p as any).aboutUrl || '';
        }
        // 同步 plan mode
        if (p.mode) {
          s.setPlanMode(p.mode === 'plan');
        }
        // 同步 UI 设置到 localStorage
        if (p.ui) {
          if (p.ui.theme) localStorage.setItem('e3d-theme', p.ui.theme)
          if (p.ui.fontSize) localStorage.setItem('e3d-setting-fontSize', String(p.ui.fontSize))
          if (p.ui.fontFamily) localStorage.setItem('e3d-font', p.ui.fontFamily)
          // 应用主题
          if (p.ui.theme) {
            const root = document.documentElement
            const isDark = p.ui.theme === 'dark' || (p.ui.theme === 'system' && window.matchMedia('(prefers-color-scheme:dark)').matches)
            root.classList.toggle('dark', isDark)
            root.classList.toggle('light', !isDark)
            window.dispatchEvent(new Event('theme-changed'))
          }
          // 应用字体
          if (p.ui.fontFamily) {
            if (p.ui.fontFamily === 'mono') {
              document.documentElement.style.setProperty('--font-family', 'JetBrains Mono, Fira Code, Consolas, monospace')
            } else {
              document.documentElement.style.removeProperty('--font-family')
            }
          }
        }
        // 同步模型参数到 localStorage
        if (p.temperature != null) {
          localStorage.setItem('e3d-setting-temperature', String(p.temperature))
        }
        if (p.maxTokens != null) {
          localStorage.setItem('e3d-setting-maxTokens', String(p.maxTokens))
        }
        break;
      }

      case MessageTypes.LlmTurnStarted: {
        s.startStreaming(tabId);
        s.setTurnStart(Date.now());
        s.resetTurnStats();
        s.setRetrying(false);
        break;
      }

      case MessageTypes.LlmStreamDelta: {
        const p = msg.payload as LlmStreamDeltaPayload;
        const state = store.getState();
        const targetId = tabId || state.activeTabId;

        // 过滤空 delta
        if (!p.delta || p.delta === '') break;

        // 调试：检测重复内容（连续相同的 delta）
        const lastDelta = _deltaBuffer.length > 0 ? _deltaBuffer[_deltaBuffer.length - 1] : null;
        if (lastDelta && lastDelta.tabId === targetId && lastDelta.text === p.delta) {
          console.warn('[Bridge] 检测到重复 delta:', p.delta);
        }

        _deltaBuffer.push({ tabId: targetId, text: p.delta });
        if (!_deltaFlushScheduled) {
          _deltaFlushScheduled = true;
          setTimeout(flushDeltaBuffer, 16);
        }
        break;
      }

      case MessageTypes.LlmStreamEnd: {
        flushDeltaBuffer();  // flush 残留 delta
        const state = store.getState();
        const targetId = tabId || state.activeTabId;
        const tab = state.tabs.find((t) => t.id === targetId);
        // Finalize thinking message
        state.finalizeThinkingMessage(tabId);
        if (tab?.currentAssistantMsgId) {
          const endPayload = msg.payload as { usage?: { total_tokens?: number }; error?: string };
          if (endPayload?.error) {
            state.setAssistantErrorMessage(tab.currentAssistantMsgId, endPayload.error, tabId);
          }
        }
        // 解析 usage 中的 token 信息
        const endPayload2 = msg.payload as { usage?: { total_tokens?: number; prompt_tokens?: number; completion_tokens?: number } };
        if (endPayload2?.usage?.total_tokens) {
          state.addTurnTokens(endPayload2.usage.total_tokens);
        }
        break;
      }

      case MessageTypes.LlmThinking: {
        const p = msg.payload as { text: string };
        s.handleThinkingDelta(p.text, tabId);
        break;
      }

      case MessageTypes.ToolDispatch: {
        flushDeltaBuffer();  // 确保文本在工具卡片之前渲染
        const p = msg.payload as ToolDispatchPayload;
        const targetId = tabId || s.activeTabId;
        const tab = s.tabs.find((t) => t.id === targetId);
        if (tab?.currentAssistantMsgId) {
          s.finalizeAssistantMessage(tab.currentAssistantMsgId, targetId);
        }
        s.appendMessage({
          role: 'tool_call',
          content: `正在调用 ${p.name}...`,
          toolId: p.id,
          toolName: p.name,
          toolArgs: p.args,
          agentName: p.agentName,
        }, tabId);
        break;
      }

      case MessageTypes.ToolResult: {
        const p = msg.payload as ToolResultPayload;
        s.handleToolResult(p.id, p.result, p.error, tabId, p.durationMs, p.meta);
        if (p.agentName) {
          s.setMessageAgentName(p.id, p.agentName, tabId);
        }
        break;
      }

      case MessageTypes.ToolError: {
        const p = msg.payload as ToolResultPayload;
        s.handleToolResult(p.id, undefined, p.error, tabId, p.durationMs, p.meta);
        if (p.agentName) {
          s.setMessageAgentName(p.id, p.agentName, tabId);
        }
        break;
      }

      case MessageTypes.ToolApproval: {
        const p = msg.payload as ApprovalRequestPayload;
        s.setPendingApproval({
          toolId: p.id,
          toolName: p.name,
          args: p.args ? JSON.parse(p.args) as unknown : undefined,
          description: p.description,
          agentName: p.agentName,
        }, tabId);
        break;
      }

      case MessageTypes.AskRequest: {
        const askPayload = msg.payload as AskRequestPayload;
        if (askPayload?.id && askPayload?.questions && askPayload.questions.length > 0) {
          const st = useChatStore.getState();
          const tId = tabId || st.activeTabId;
          st.setPendingQuestion({
            questionId: askPayload.id,
            question: askPayload.questions[0]?.prompt || '',
            options: askPayload.questions[0]?.options?.map(o => o.label),
            multiSelect: askPayload.questions[0]?.multi,
            askData: {
              askId: askPayload.id,
              questions: askPayload.questions,
            },
          }, tId);
        }
        break;
      }

      case MessageTypes.TurnDone: {
        s.stopStreaming(tabId);
        s.setTurnStart(null);
        // 每轮结束后自动保存会话
        s.saveSession();
        break;
      }

      case MessageTypes.Error: {
        const p = msg.payload as ErrorPayload;
        s.appendMessage({ role: 'error', content: p.message });
        // Toast 通知
        import('../../store/useToastStore').then(({ useToastStore }) => {
          useToastStore.getState().addToast('error', p.message);
        });
        break;
      }

      case MessageTypes.Notice: {
        const p = msg.payload as NoticePayload;
        // Toast 通知
        import('../../store/useToastStore').then(({ useToastStore }) => {
          useToastStore.getState().addToast('info', p.text);
        });
        break;
      }

      case MessageTypes.Pong:
        s.setLastPingTime(Date.now());
        break;

      case MessageTypes.LlmUsage: {
        const p = msg.payload as { tokens?: number; total_tokens?: number; data?: { total_tokens?: number } };
        const tokens = p?.tokens ?? p?.total_tokens ?? p?.data?.total_tokens;
        if (tokens) {
          s.addTurnTokens(tokens);
        }
        break;
      }

      case MessageTypes.LlmRetry: {
        const p = msg.payload as { text?: string };
        s.setRetrying(true);
        // Toast 通知用户正在重试
        import('../../store/useToastStore').then(({ useToastStore }) => {
          useToastStore.getState().addToast('warning', p?.text || '正在重试...');
        });
        break;
      }

      case MessageTypes.ToolProgress: {
        const p = msg.payload as { id: string; text?: string; progress?: unknown };
        if (p?.id) {
          s.handleToolProgress(p.id, p.text || '', p.progress, tabId);
        }
        break;
      }

      case MessageTypes.UserSetPlanMode: {
        const p = msg.payload as { enabled?: boolean; mode?: string };
        const enabled = p?.enabled ?? (p?.mode === 'plan');
        s.setPlanMode(enabled);
        break;
      }

      case MessageTypes.UserSetApprovalMode: {
        const p = msg.payload as { mode?: string };
        if (p?.mode) {
          useChatStore.setState({ toolApprovalMode: p.mode as ToolApprovalMode });
        }
        break;
      }

      default:
        break;
    }
  });
}
