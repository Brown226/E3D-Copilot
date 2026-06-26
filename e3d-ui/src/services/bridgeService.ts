/**
 * E小智 WebView2 Bridge Service
 * 基于 web-ui/bridge.ts 改造，增加 store 映射 + standalone mock 支持
 */

import { useChatStore } from '../store/useChatStore';
import type { ToolApprovalMode } from '../store/useChatStore';
import type {
  UserMessagePayload,
  ApprovalPayload,
  AskRequestPayload,
  WireAskQuestion,
  QuestionAnswerItem,
  LlmStreamDeltaPayload,
  ToolDispatchPayload,
  ToolResultPayload,
  NoticePayload,
  ErrorPayload,
  ConfigSyncPayload,
} from './messageContracts';

export { MessageTypes } from './messageContracts';

type MessageCallback = (msg: { type: string; payload: unknown }) => void;

class Bridge {
  private listeners = new Set<MessageCallback>();
  private pendingRequests = new Map<string, { resolve: (v: unknown) => void; reject: (e: Error) => void; timeout: ReturnType<typeof setTimeout> }>();
  private requestCounter = 0;
  private _mockTimers: ReturnType<typeof setTimeout>[] = [];

  constructor() {
    if (this.isAvailable()) {
      (window as unknown as { chrome: { webview: { addEventListener: (evt: string, handler: (event: unknown) => void) => void } } }).chrome.webview.addEventListener('message', (event: unknown) => {
        this.handleIncoming((event as { data: string }).data);
      });
    } else {
      console.warn('[Bridge] chrome.webview not available - running in standalone mode');
    }
  }

  private handleIncoming = (raw: string) => {
    let data: { type?: string; payload?: unknown; _requestId?: string };
    try {
      data = JSON.parse(raw) as { type?: string; payload?: unknown; _requestId?: string };
    } catch {
      console.error('[Bridge] Failed to parse message:', raw);
      return;
    }

    // 处理请求响应
    if (data._requestId && this.pendingRequests.has(data._requestId)) {
      const pending = this.pendingRequests.get(data._requestId)!;
      clearTimeout(pending.timeout);
      this.pendingRequests.delete(data._requestId);
      pending.resolve(data.payload);
      return;
    }

    // 通知所有监听器
    this.listeners.forEach(cb => cb(data as { type: string; payload: unknown }));
  };

  /**
   * 发送消息到 C# 后端
   */
  send(type: string, payload?: unknown): void {
    const msg = JSON.stringify({ type, payload });
    if (this.isAvailable()) {
      console.log('[Bridge -> Host]', type, JSON.stringify(payload)?.substring(0, 100));
      (window as unknown as { chrome: { webview: { postMessage: (msg: string) => void } } }).chrome.webview.postMessage(msg);
    } else {
      console.log('[Bridge -> Host] (standalone mode)', type, payload);
    }
  }

  /**
   * 发送消息并等待响应
   */
  sendAndWait(type: string, payload?: unknown, timeoutMs = 30000): Promise<unknown> {
    return new Promise((resolve, reject) => {
      const requestId = `req_${++this.requestCounter}_${Date.now()}`;
      const timeout = setTimeout(() => {
        this.pendingRequests.delete(requestId);
        reject(new Error(`Request ${type} timed out after ${timeoutMs}ms`));
      }, timeoutMs);

      this.pendingRequests.set(requestId, { resolve, reject, timeout });
      const msg = JSON.stringify({ type, payload, _requestId: requestId });

      if (this.isAvailable()) {
        (window as unknown as { chrome: { webview: { postMessage: (msg: string) => void } } }).chrome.webview.postMessage(msg);
      } else {
        console.log('[Bridge -> Host]', type, payload, '(requestId:', requestId, ')');
        // 在独立模式下，模拟响应
        setTimeout(() => {
          this.handleIncoming(JSON.stringify({ _requestId: requestId, payload: null }));
        }, 100);
      }
    });
  }

  /**
   * 注册消息监听器
   */
  on(callback: MessageCallback): () => void {
    this.listeners.add(callback);
    return () => { this.listeners.delete(callback); };
  }

  /**
   * 等待特定类型的消息
   */
  once(type: string): Promise<{ type: string; payload: unknown }> {
    return new Promise(resolve => {
      const unsub = this.on(msg => {
        if (msg.type === type) {
          unsub();
          resolve(msg);
        }
      });
      void unsub;
    });
  }

  /**
   * 检查 Bridge 是否可用
   */
  isAvailable(): boolean {
    return !!(window as unknown as { chrome?: { webview?: unknown } }).chrome?.webview;
  }

  // ============================================
  // 类型化便利方法
  // ============================================

  sendUserMessage(text: string, images?: string[], files?: string[], tabId?: string): void {
    this.send('user:message', { text, images, files, tabId } as UserMessagePayload);
  }

  sendApproval(toolId: string, allow: boolean): void {
    this.send('user:approve', { id: toolId, allow } as ApprovalPayload);
  }

  /**
   * 发送结构化回答（对齐 Reasonix AnswerQuestion）
   */
  sendAskAnswer(id: string, answers: QuestionAnswerItem[]): void {
    this.send('user:ask_response', { id, answers });
  }

  cancel(): void {
    this.send('user:cancel');
  }

  newSession(): void {
    this.send('user:new_session');
  }

  closeTab(tabId: string): void {
    this.send('tab:close', { tabId });
  }

  ping(): Promise<unknown> {
    return this.sendAndWait('ping', null, 5000);
  }

  listModels(): Promise<unknown> {
    return this.sendAndWait('models:list', null, 10000);
  }

  switchModel(ref: string): Promise<unknown> {
    return this.sendAndWait('model:switch', { ref }, 10000);
  }

  listProviders(): Promise<unknown> {
    return this.sendAndWait('providers:list', null, 10000);
  }

  saveProvider(provider: unknown): Promise<unknown> {
    return this.sendAndWait('provider:save', provider, 10000);
  }

  deleteProvider(name: string): Promise<unknown> {
    return this.sendAndWait('provider:delete', { name }, 10000);
  }

  fetchProviderModels(name: string): Promise<unknown> {
    return this.sendAndWait('provider:fetch_models', { name }, 15000);
  }

  setProviderKey(name: string, apiKey: string): Promise<unknown> {
    return this.sendAndWait('provider:set_key', { name, apiKey }, 10000);
  }

  // ============================================
  // Skills 管理
  // ============================================

  listSkills(): Promise<unknown> {
    return this.sendAndWait('skills:list', null, 10000);
  }

  toggleSkill(name: string): Promise<unknown> {
    return this.sendAndWait('skills:toggle', { name }, 10000);
  }

  addSkillSource(path: string): Promise<unknown> {
    return this.sendAndWait('skills:add_source', { path }, 10000);
  }

  removeSkillSource(path: string): Promise<unknown> {
    return this.sendAndWait('skills:remove_source', { path }, 10000);
  }

  refreshSkills(): Promise<unknown> {
    return this.sendAndWait('skills:refresh', null, 10000);
  }

  // ============================================
  // Memory 管理
  // ============================================

  listMemories(): Promise<unknown> {
    return this.sendAndWait('memory:list', null, 10000);
  }

  saveMemory(entry: { title: string; content: string; kind: string; tags?: string[]; id?: string }): Promise<unknown> {
    return this.sendAndWait('memory:save', entry, 10000);
  }

  deleteMemory(id: string): Promise<unknown> {
    return this.sendAndWait('memory:delete', { id }, 5000);
  }

  // ============================================
  // Settings 管理
  // ============================================

  saveSetting(key: string, value: string): Promise<unknown> {
    return this.sendAndWait('settings:save', { key, value }, 5000);
  }

  // ============================================
  // Sessions 管理
  // ============================================

  listSessions(): Promise<unknown> {
    return this.sendAndWait('sessions:list', null, 10000);
  }

  deleteSession(id: string): Promise<unknown> {
    return this.sendAndWait('sessions:delete', { id }, 5000);
  }

  // ============================================
  // 消息监听便利方法
  // ============================================

  onLlmStreamDelta(callback: (delta: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'llm:stream:delta') {
        callback((msg.payload as LlmStreamDeltaPayload)?.delta || '');
      }
    });
  }

  onToolDispatch(callback: (id: string, name: string, args: unknown) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'tool:dispatch') {
        const p = msg.payload as ToolDispatchPayload;
        callback(p?.id || '', p?.name || '', p?.args);
      }
    });
  }

  onToolResult(callback: (id: string, result?: string, error?: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'tool:result') {
        const p = msg.payload as ToolResultPayload;
        callback(p?.id || '', p?.result, p?.error);
      }
    });
  }

  onToolError(callback: (id: string, error: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'tool:error') {
        const p = msg.payload as ToolResultPayload;
        callback(p?.id || '', p?.error || '');
      }
    });
  }

  onToolApproval(callback: (id: string, name: string, args?: string, description?: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'tool:approval') {
        const p = msg.payload as { id: string; name: string; args?: string; description: string };
        callback(p?.id || '', p?.name || '', p?.args, p?.description || '');
      }
    });
  }

  /**
   * 监听 AskRequest 事件（对齐 Reasonix AskRequest）
   */
  onAskRequest(callback: (ask: AskRequestPayload) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'ask_request') {
        const p = msg.payload as AskRequestPayload;
        if (p?.id && p?.questions) {
          callback(p);
        }
      }
    });
  }

  onHostReady(callback: (version: string, platform: string, timestamp: number) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'host:ready') {
        const p = msg.payload as { version: string; platform: string; timestamp: number };
        callback(p?.version || '', p?.platform || '', p?.timestamp || 0);
      }
    });
  }

  onConfigSync(callback: (provider: string, model: string, baseUrl: string, apiKey: string, mode: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'config:sync') {
        const p = msg.payload as ConfigSyncPayload;
        callback(p?.provider || '', p?.model || '', p?.baseUrl || '', p?.apiKey || '', p?.mode || '');
      }
    });
  }

  onLlmStreamEnd(callback: (usage?: unknown) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'llm:stream:end') {
        callback(msg.payload as { usage?: unknown } | undefined);
      }
    });
  }

  onLlmThinking(callback: (text: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'llm:thinking') {
        callback((msg.payload as { text: string })?.text || '');
      }
    });
  }

  onPong(callback: (timestamp: number) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'pong') {
        callback((msg.payload as { timestamp: number })?.timestamp || 0);
      }
    });
  }

  onModelsListResult(callback: (models: unknown[], currentProvider: string, currentModel: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'models:list:result') {
        const p = msg.payload as { models: unknown[]; currentProvider: string; currentModel: string };
        callback(p?.models || [], p?.currentProvider || '', p?.currentModel || '');
      }
    });
  }

  onProvidersListResult(callback: (providers: unknown[], currentProvider: string, currentModel: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'providers:list:result') {
        const p = msg.payload as { providers: unknown[]; currentProvider: string; currentModel: string };
        callback(p?.providers || [], p?.currentProvider || '', p?.currentModel || '');
      }
    });
  }

  onProviderFetchResult(callback: (providerName: string, success: boolean, models: string[], error?: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'provider:fetch_models:result') {
        const p = msg.payload as { providerName: string; success: boolean; models: string[]; error?: string };
        callback(p?.providerName || '', p?.success || false, p?.models || [], p?.error);
      }
    });
  }

  onNotice(callback: (text: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'notice') {
        callback((msg.payload as NoticePayload)?.text || '');
      }
    });
  }

  onError(callback: (message: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'error') {
        callback((msg.payload as ErrorPayload)?.message || 'Unknown error');
      }
    });
  }

  // ============================================
  // 断连 / 重连事件（T2.2 新增）
  // ============================================

  onDisconnected(callback: () => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'bridge:disconnected') {
        callback();
      }
    });
  }

  onReconnected(callback: () => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'bridge:reconnected') {
        callback();
      }
    });
  }

  /** 手动触发断连事件（供心跳检测使用） */
  emitDisconnected(): void {
    this.listeners.forEach(cb => cb({ type: 'bridge:disconnected', payload: null }));
  }

  /** 手动触发重连事件 */
  emitReconnected(): void {
    this.listeners.forEach(cb => cb({ type: 'bridge:reconnected', payload: null }));
  }

  // ============================================
  // Standalone Mock 模式（T2.6）
  // ============================================

  startMock(): void {
    console.log('[Mock] Standalone mock mode activated');
    // 立即发送 host:ready（无延迟，避免断连屏闪烁）
    this.handleIncoming(JSON.stringify({
      type: 'host:ready',
      payload: { version: '2.0.0-mock', platform: 'standalone', timestamp: Date.now() },
    }));
    // config:sync（延迟 100ms 确保 store 映射已就绪）
    setTimeout(() => {
      this.handleIncoming(JSON.stringify({
        type: 'config:sync',
        payload: {
          provider: 'openai',
          model: 'gpt-4o',
          baseUrl: 'https://api.openai.com',
          apiKey: '',
          mode: 'chat',
          currentProvider: 'openai',
          currentModel: 'gpt-4o',
          providers: [
            {
              name: 'openai', kind: 'openai', baseUrl: 'https://api.openai.com',
              apiKey: '', keySet: false, models: ['gpt-4o', 'gpt-4o-mini', 'gpt-3.5-turbo'],
              default: 'gpt-4o', enabled: true, builtIn: true,
            },
          ],
        },
      }));
    }, 100);
  }

  /** 模拟一轮完整的 AI 回复流程 */
  mockStreamResponse(userText: string): void {
    console.log('[Mock] Simulating AI response for:', userText);

    // llm:turn_started
    this._mockTimers.push(setTimeout(() => {
      this.handleIncoming(JSON.stringify({ type: 'llm:turn_started', payload: {} }));
    }, 2000));

    // llm:thinking
    this._mockTimers.push(setTimeout(() => {
      this.handleIncoming(JSON.stringify({
        type: 'llm:thinking',
        payload: { text: `分析用户问题："${userText.substring(0, 30)}..."，正在思考最佳回复方式。` },
      }));
    }, 2200));

    // 流式回复
    const replyChunks = [
      `你好！你问的是"${userText.substring(0, 20)}"。`,
      '\n\n',
      '这是一个 **Mock 模式** 的示例回复。',
      '\n\n在 standalone 模式下，',
      '所有 AI 回复都是模拟数据，',
      '用于开发和调试 UI。',
    ];

    replyChunks.forEach((chunk, i) => {
      this._mockTimers.push(setTimeout(() => {
        this.handleIncoming(JSON.stringify({
          type: 'llm:stream:delta',
          payload: { delta: chunk },
        }));
      }, 3000 + i * 400));
    });

    // llm:stream:end
    this._mockTimers.push(setTimeout(() => {
      this.handleIncoming(JSON.stringify({
        type: 'llm:stream:end',
        payload: { usage: { prompt_tokens: 50, completion_tokens: 80, total_tokens: 130 } },
      }));
    }, 3000 + replyChunks.length * 400 + 200));

    // tool:dispatch
    const toolDelay = 3000 + replyChunks.length * 400 + 800;
    this._mockTimers.push(setTimeout(() => {
      this.handleIncoming(JSON.stringify({
        type: 'tool:dispatch',
        payload: { id: 'mock_tool_001', name: 'search_catalog', args: { query: userText.substring(0, 30) } },
      }));
    }, toolDelay));

    // tool:result
    this._mockTimers.push(setTimeout(() => {
      this.handleIncoming(JSON.stringify({
        type: 'tool:result',
        payload: { id: 'mock_tool_001', result: JSON.stringify({ found: 3, items: ['Pump A', 'Valve B', 'Filter C'] }) },
      }));
    }, toolDelay + 1000));

    // turn:done
    this._mockTimers.push(setTimeout(() => {
      this.handleIncoming(JSON.stringify({ type: 'turn:done', payload: {} }));
    }, toolDelay + 1500));
  }

  /** 清理 mock 定时器 */
  stopMock(): void {
    this._mockTimers.forEach(t => clearTimeout(t));
    this._mockTimers = [];
  }
}

// ============================================
// 流式 delta 批量合并缓冲（60fps 节流）
// ============================================
let _deltaBuffer: { tabId: string; text: string }[] = [];
let _deltaFlushScheduled = false;

// ============================================
// Bridge 事件 → Store 映射（T2.4）
// ============================================

function registerStoreMappings(bridgeInstance: Bridge): void {
  const store = useChatStore;

  bridgeInstance.on((msg) => {
    const s = store.getState();
    // 从 payload 提取 tabId（后端事件可能携带）
    const payload = msg.payload as Record<string, unknown> | undefined;
    const tabId = (payload?.tabId as string) || undefined;

    switch (msg.type) {
      case 'host:ready':
        s.setBridgeConnected(true);
        break;

      case 'config:sync': {
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

      case 'llm:turn_started': {
        s.startStreaming(tabId);
        s.setTurnStart(Date.now());
        s.resetTurnStats();
        s.setRetrying(false);
        break;
      }

      case 'llm:stream:delta': {
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
        
        // 不检查 currentAssistantMsgId 是否存在 — appendAssistantDelta 会在首次 delta 时自动创建 assistant 消息
        _deltaBuffer.push({ tabId: targetId, text: p.delta });
        if (!_deltaFlushScheduled) {
          _deltaFlushScheduled = true;
          setTimeout(() => {
            _deltaFlushScheduled = false;
            if (_deltaBuffer.length === 0) return;
            const byTab = new Map<string, string>();
            for (const d of _deltaBuffer) {
              byTab.set(d.tabId, (byTab.get(d.tabId) || '') + d.text);
            }
            _deltaBuffer = [];
            const s = store.getState();
            for (const [tid, combined] of byTab) {
              s.appendAssistantDelta(combined, tid);
            }
          }, 16);
        }
        break;
      }

      case 'llm:stream:end': {
        // 立即 flush 残留的 delta 缓冲
        if (_deltaBuffer.length > 0) {
          const byTab = new Map<string, string>();
          for (const d of _deltaBuffer) {
            byTab.set(d.tabId, (byTab.get(d.tabId) || '') + d.text);
          }
          _deltaBuffer = [];
          const flushState = store.getState();
          for (const [tid, combined] of byTab) {
            flushState.appendAssistantDelta(combined, tid);
          }
        }
        const state = store.getState();
        const targetId = tabId || state.activeTabId;
        const tab = state.tabs.find((t) => t.id === targetId);
        // Finalize thinking message (auto-collapse ThinkingBlock)
        // 必须在 if 之外，因为 thinking 消息独立于 assistant 消息存在
        state.finalizeThinkingMessage(tabId);
        if (tab?.currentAssistantMsgId) {
          // 解析 error 字段（LLM 空回复时的错误信息）
          const endPayload = msg.payload as { usage?: { total_tokens?: number }; error?: string };
          if (endPayload?.error) {
            state.setAssistantErrorMessage(tab.currentAssistantMsgId, endPayload.error, tabId);
          }
          // 注意：不在这里 finalize assistant 消息和清空 currentAssistantMsgId！
          // AgentLoop 可能有多轮 LLM 调用（step 1: LLM→tools, step 2: LLM→text），
          // 清空 ID 会导致后续 delta 丢失。由 turn:done 统一 finalize。
        }
        // 解析 usage 中的 token 信息
        const endPayload2 = msg.payload as { usage?: { total_tokens?: number; prompt_tokens?: number; completion_tokens?: number } };
        if (endPayload2?.usage?.total_tokens) {
          state.addTurnTokens(endPayload2.usage.total_tokens);
        }
        break;
      }

      case 'llm:thinking': {
        const p = msg.payload as { text: string };
        s.handleThinkingDelta(p.text, tabId);
        break;
      }

      case 'tool:dispatch': {
        const p = msg.payload as ToolDispatchPayload;
        s.appendMessage({
          role: 'tool_call',
          content: `正在调用 ${p.name}...`,
          toolId: p.id,
          toolName: p.name,
          toolArgs: p.args,
        }, tabId);
        break;
      }

      case 'tool:result': {
        const p = msg.payload as ToolResultPayload;
        s.handleToolResult(p.id, p.result, p.error, tabId, p.durationMs);
        break;
      }

      case 'tool:error': {
        const p = msg.payload as ToolResultPayload;
        s.handleToolResult(p.id, undefined, p.error, tabId, p.durationMs);
        break;
      }

      case 'tool:approval': {
        const p = msg.payload as { id: string; name: string; args?: string; description: string };
        s.setPendingApproval({
          toolId: p.id,
          toolName: p.name,
          args: p.args ? JSON.parse(p.args) as unknown : undefined,
          description: p.description,
        }, tabId);
        break;
      }

      case 'ask_request': {
        const askPayload = msg.payload as AskRequestPayload;
        if (askPayload?.id && askPayload?.questions && askPayload.questions.length > 0) {
          const store = useChatStore.getState();
          const tId = tabId || store.activeTabId;
          store.setPendingQuestion({
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

      case 'turn:done': {
        s.stopStreaming(tabId);
        s.setTurnStart(null);
        // 每轮结束后自动保存会话
        s.saveSession();
        break;
      }

      case 'error': {
        const p = msg.payload as ErrorPayload;
        s.appendMessage({ role: 'error', content: p.message });
        // Toast 通知
        import('../store/useToastStore').then(({ useToastStore }) => {
          useToastStore.getState().addToast('error', p.message);
        });
        break;
      }

      case 'notice': {
        const p = msg.payload as NoticePayload;
        // Toast 通知
        import('../store/useToastStore').then(({ useToastStore }) => {
          useToastStore.getState().addToast('info', p.text);
        });
        break;
      }

      case 'pong':
        s.setLastPingTime(Date.now());
        break;

      case 'llm:usage': {
        const p = msg.payload as { tokens?: number; total_tokens?: number; data?: { total_tokens?: number } };
        const tokens = p?.tokens ?? p?.total_tokens ?? p?.data?.total_tokens;
        if (tokens) {
          s.addTurnTokens(tokens);
        }
        break;
      }

      case 'llm:retry': {
        const p = msg.payload as { text?: string };
        s.setRetrying(true);
        // Toast 通知用户正在重试
        import('../store/useToastStore').then(({ useToastStore }) => {
          useToastStore.getState().addToast('warning', p?.text || '正在重试...');
        });
        break;
      }

      case 'tool:progress': {
        const p = msg.payload as { id: string; text?: string; progress?: unknown };
        if (p?.id) {
          s.handleToolProgress(p.id, p.text || '', p.progress, tabId);
        }
        break;
      }

      case 'user:set_plan_mode': {
        // 后端确认 Plan/Act 模式切换（可能是后端主动触发或前端请求的响应）
        const p = msg.payload as { enabled?: boolean; mode?: string };
        const enabled = p?.enabled ?? (p?.mode === 'plan');
        s.setPlanMode(enabled);
        break;
      }

      case 'user:set_approval_mode': {
        // 后端确认工具审批模式切换
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

// ============================================
// 初始化 Bridge 并导出单例
// ============================================

const bridge = new Bridge();

// 注册 store 映射
registerStoreMappings(bridge);

// 等待 host:ready 超时机制 + 自动重连
// WebView2 模式下 C# 端应在 NavigationCompleted 后发送 host:ready
// 如果未收到，自动 ping 重试直到连接成功
let hostReadyTimer: ReturnType<typeof setTimeout> | null = null;
let autoReconnectTimer: ReturnType<typeof setInterval> | null = null;

function startAutoReconnect() {
  if (autoReconnectTimer) return
  autoReconnectTimer = setInterval(async () => {
    const state = useChatStore.getState()
    if (state.bridgeConnected) {
      // 已连接，停止重试
      if (autoReconnectTimer) { clearInterval(autoReconnectTimer); autoReconnectTimer = null }
      return
    }
    try {
      await bridge.ping()
      // ping 成功，标记为已连接
      useChatStore.getState().setBridgeConnected(true)
      if (autoReconnectTimer) { clearInterval(autoReconnectTimer); autoReconnectTimer = null }
    } catch {
      // ping 失败，继续重试
    }
  }, 3000)  // 每 3 秒重试一次
}

if (bridge.isAvailable()) {
  // WebView2 模式：等待 C# 发送 host:ready
  hostReadyTimer = setTimeout(() => {
    const state = useChatStore.getState()
    if (!state.bridgeConnected) {
      console.warn('[Bridge] host:ready 未在 5 秒内收到，启动自动重连')
      startAutoReconnect()
    }
  }, 5000)
} else {
  // Standalone 模式：立即启动 mock（无延迟，避免断连屏闪烁）
  Promise.resolve().then(() => bridge.startMock())
}

// 监听 host:ready 后取消超时定时器和自动重连
bridge.onHostReady(() => {
  if (hostReadyTimer) {
    clearTimeout(hostReadyTimer)
    hostReadyTimer = null
  }
  if (autoReconnectTimer) {
    clearInterval(autoReconnectTimer)
    autoReconnectTimer = null
  }
})

export default bridge;
