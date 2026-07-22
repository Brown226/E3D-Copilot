/**
 * Bridge 核心通信类
 * 职责：WebView2 消息收发、请求/响应协议、事件监听、类型化便利方法
 */

import type {
  UserMessagePayload,
  ApprovalPayload,
  QuestionAnswerItem,
  LlmStreamDeltaPayload,
  ToolDispatchPayload,
  ToolResultPayload,
  NoticePayload,
  ErrorPayload,
  ConfigSyncPayload,
  AskRequestPayload,
  ModelsListResultPayload,
  ProvidersListResultPayload,
  ProviderFetchResultPayload,
  SkillsListResultPayload,
  MemoryListResultPayload,
  SessionsListResultPayload,
} from '../messageContracts';
import { MessageTypes } from '../messageContracts';

export type MessageCallback = (msg: { type: string; payload: unknown }) => void;

export class Bridge {
  private listeners = new Set<MessageCallback>();
  private pendingRequests = new Map<string, { resolve: (v: unknown) => void; reject: (e: Error) => void; timeout: ReturnType<typeof setTimeout> }>();
  private requestCounter = 0;

  constructor() {
    if (this.isAvailable()) {
      (window as unknown as { chrome: { webview: { addEventListener: (evt: string, handler: (event: unknown) => void) => void } } }).chrome.webview.addEventListener('message', (event: unknown) => {
        this.handleIncoming((event as { data: string }).data);
      });
    } else {
      console.warn('[Bridge] chrome.webview not available - running in standalone mode');
    }
  }

  handleIncoming = (raw: string) => {
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

  /** 广播事件到所有监听器（供 mock/reconnect 模块使用） */
  emit(type: string, payload: unknown): void {
    this.listeners.forEach(cb => cb({ type, payload }));
  }

  // ============================================
  // 类型化便利方法
  // ============================================

  sendUserMessage(text: string, images?: string[], files?: string[], tabId?: string): void {
    this.send(MessageTypes.UserMessage, { text, images, files, tabId } as UserMessagePayload);
  }

  sendApproval(toolId: string, allow: boolean): void {
    this.send(MessageTypes.UserApprove, { id: toolId, allow } as ApprovalPayload);
  }

  /**
   * 发送结构化回答（对齐 Reasonix AnswerQuestion）
   */
  sendAskAnswer(id: string, answers: QuestionAnswerItem[]): void {
    this.send(MessageTypes.UserAskResponse, { id, answers });
  }

  cancel(): void {
    this.send(MessageTypes.UserCancel);
  }

  newSession(): void {
    this.send(MessageTypes.UserNewSession);
  }

  closeTab(tabId: string): void {
    this.send(MessageTypes.TabClose, { tabId });
  }

  ping(): Promise<unknown> {
    return this.sendAndWait(MessageTypes.Ping, null, 5000);
  }

  listModels(): Promise<ModelsListResultPayload> {
    return this.sendAndWait(MessageTypes.ModelsList, null, 10000) as Promise<ModelsListResultPayload>;
  }

  switchModel(ref: string): Promise<unknown> {
    return this.sendAndWait(MessageTypes.ModelSwitch, { ref }, 10000);
  }

  listProviders(): Promise<ProvidersListResultPayload> {
    return this.sendAndWait(MessageTypes.ProvidersList, null, 10000) as Promise<ProvidersListResultPayload>;
  }

  saveProvider(provider: unknown): Promise<unknown> {
    return this.sendAndWait(MessageTypes.ProviderSave, provider, 10000);
  }

  deleteProvider(name: string): Promise<unknown> {
    return this.sendAndWait(MessageTypes.ProviderDelete, { name }, 10000);
  }

  fetchProviderModels(name: string): Promise<ProviderFetchResultPayload> {
    return this.sendAndWait(MessageTypes.ProviderFetchModels, { name }, 15000) as Promise<ProviderFetchResultPayload>;
  }

  setProviderKey(name: string, apiKey: string): Promise<unknown> {
    return this.sendAndWait(MessageTypes.ProviderSetKey, { name, apiKey }, 10000);
  }

  // ============================================
  // Skills 管理
  // ============================================

  listSkills(): Promise<SkillsListResultPayload> {
    return this.sendAndWait(MessageTypes.SkillsList, null, 10000) as Promise<SkillsListResultPayload>;
  }

  toggleSkill(name: string): Promise<unknown> {
    return this.sendAndWait(MessageTypes.SkillsToggle, { name }, 10000);
  }

  addSkillSource(path: string): Promise<unknown> {
    return this.sendAndWait(MessageTypes.SkillsAddSource, { path }, 10000);
  }

  removeSkillSource(path: string): Promise<unknown> {
    return this.sendAndWait(MessageTypes.SkillsRemoveSource, { path }, 10000);
  }

  refreshSkills(): Promise<unknown> {
    return this.sendAndWait(MessageTypes.SkillsRefresh, null, 10000);
  }

  // ============================================
  // Memory 管理
  // ============================================

  listMemories(): Promise<MemoryListResultPayload> {
    return this.sendAndWait(MessageTypes.MemoryList, null, 10000) as Promise<MemoryListResultPayload>;
  }

  saveMemory(entry: { title: string; content: string; kind: string; tags?: string[]; id?: string }): Promise<unknown> {
    return this.sendAndWait(MessageTypes.MemorySave, entry, 10000);
  }

  deleteMemory(id: string): Promise<unknown> {
    return this.sendAndWait(MessageTypes.MemoryDelete, { id }, 5000);
  }

  // ============================================
  // Settings 管理
  // ============================================

  saveSetting(key: string, value: string): Promise<unknown> {
    return this.sendAndWait(MessageTypes.SettingsSave, { key, value }, 5000);
  }

  // ============================================
  // Sessions 管理
  // ============================================

  listSessions(): Promise<SessionsListResultPayload> {
    return this.sendAndWait(MessageTypes.SessionsList, null, 10000) as Promise<SessionsListResultPayload>;
  }

  deleteSession(id: string): Promise<unknown> {
    return this.sendAndWait(MessageTypes.SessionsDelete, { id }, 5000);
  }

  // ============================================
  // 消息监听便利方法
  // ============================================

  onLlmStreamDelta(callback: (delta: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === MessageTypes.LlmStreamDelta) {
        callback((msg.payload as LlmStreamDeltaPayload)?.delta || '');
      }
    });
  }

  onToolDispatch(callback: (id: string, name: string, args: unknown) => void): () => void {
    return this.on((msg) => {
      if (msg.type === MessageTypes.ToolDispatch) {
        const p = msg.payload as ToolDispatchPayload;
        callback(p?.id || '', p?.name || '', p?.args);
      }
    });
  }

  onToolResult(callback: (id: string, result?: string, error?: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === MessageTypes.ToolResult) {
        const p = msg.payload as ToolResultPayload;
        callback(p?.id || '', p?.result, p?.error);
      }
    });
  }

  onToolError(callback: (id: string, error: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === MessageTypes.ToolError) {
        const p = msg.payload as ToolResultPayload;
        callback(p?.id || '', p?.error || '');
      }
    });
  }

  onToolApproval(callback: (id: string, name: string, args?: string, description?: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === MessageTypes.ToolApproval) {
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
      if (msg.type === MessageTypes.AskRequest) {
        const p = msg.payload as AskRequestPayload;
        if (p?.id && p?.questions) {
          callback(p);
        }
      }
    });
  }

  onHostReady(callback: (version: string, platform: string, timestamp: number) => void): () => void {
    return this.on((msg) => {
      if (msg.type === MessageTypes.HostReady) {
        const p = msg.payload as { version: string; platform: string; timestamp: number };
        callback(p?.version || '', p?.platform || '', p?.timestamp || 0);
      }
    });
  }

  onConfigSync(callback: (provider: string, model: string, baseUrl: string, apiKey: string, mode: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === MessageTypes.ConfigSync) {
        const p = msg.payload as ConfigSyncPayload;
        callback(p?.provider || '', p?.model || '', p?.baseUrl || '', p?.apiKey || '', p?.mode || '');
      }
    });
  }

  onLlmStreamEnd(callback: (usage?: unknown) => void): () => void {
    return this.on((msg) => {
      if (msg.type === MessageTypes.LlmStreamEnd) {
        callback(msg.payload as { usage?: unknown } | undefined);
      }
    });
  }

  onLlmThinking(callback: (text: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === MessageTypes.LlmThinking) {
        callback((msg.payload as { text: string })?.text || '');
      }
    });
  }

  onPong(callback: (timestamp: number) => void): () => void {
    return this.on((msg) => {
      if (msg.type === MessageTypes.Pong) {
        callback((msg.payload as { timestamp: number })?.timestamp || 0);
      }
    });
  }

  onModelsListResult(callback: (models: unknown[], currentProvider: string, currentModel: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === MessageTypes.ModelsListResult) {
        const p = msg.payload as { models: unknown[]; currentProvider: string; currentModel: string };
        callback(p?.models || [], p?.currentProvider || '', p?.currentModel || '');
      }
    });
  }

  onProvidersListResult(callback: (providers: unknown[], currentProvider: string, currentModel: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === MessageTypes.ProvidersListResult) {
        const p = msg.payload as { providers: unknown[]; currentProvider: string; currentModel: string };
        callback(p?.providers || [], p?.currentProvider || '', p?.currentModel || '');
      }
    });
  }

  onProviderFetchResult(callback: (providerName: string, success: boolean, models: string[], error?: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === MessageTypes.ProviderFetchResult) {
        const p = msg.payload as { providerName: string; success: boolean; models: string[]; error?: string };
        callback(p?.providerName || '', p?.success || false, p?.models || [], p?.error);
      }
    });
  }

  onNotice(callback: (text: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === MessageTypes.Notice) {
        callback((msg.payload as NoticePayload)?.text || '');
      }
    });
  }

  onError(callback: (message: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === MessageTypes.Error) {
        callback((msg.payload as ErrorPayload)?.message || 'Unknown error');
      }
    });
  }

  // ============================================
  // 断连 / 重连事件
  // ============================================

  onDisconnected(callback: () => void): () => void {
    return this.on((msg) => {
      if (msg.type === MessageTypes.BridgeDisconnected) {
        callback();
      }
    });
  }

  onReconnected(callback: () => void): () => void {
    return this.on((msg) => {
      if (msg.type === MessageTypes.BridgeReconnected) {
        callback();
      }
    });
  }

  /** 手动触发断连事件（供心跳检测使用） */
  emitDisconnected(): void {
    this.emit(MessageTypes.BridgeDisconnected, null);
  }

  /** 手动触发重连事件 */
  emitReconnected(): void {
    this.emit(MessageTypes.BridgeReconnected, null);
  }
}
