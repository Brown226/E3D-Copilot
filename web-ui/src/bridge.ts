/**
 * E小智 WebView2 Bridge — C# ↔ React 双向通信桥（改进版）
 * 
 * 改进点：
 * 1. 使用 TypeScript 类型定义（messageContracts.ts）
 * 2. 类型安全的消息发送和接收
 * 3. 更好的错误处理
 */

export { MessageTypes } from './messageContracts';
import type {
  UserMessagePayload,
  ApprovalPayload,
  AskResponsePayload,
  LlmStreamDeltaPayload,
  ToolDispatchPayload,
  ToolResultPayload,
  NoticePayload,
  ErrorPayload,
} from './messageContracts';

type MessageCallback = (msg: { type: string; payload: any }) => void;

class Bridge {
  private listeners = new Set<MessageCallback>();
  private pendingRequests = new Map<string, { resolve: (v: any) => void; reject: (e: Error) => void; timeout: NodeJS.Timeout }>();
  private requestCounter = 0;

  constructor() {
    if ((window as any).chrome?.webview) {
      (window as any).chrome.webview.addEventListener('message', (event: any) => {
        this.handleIncoming(event.data);
      });
    } else {
      console.warn('[Bridge] chrome.webview not available - running in standalone mode');
    }
  }

  private handleIncoming = (raw: string) => {
    let data: any;
    try { 
      data = JSON.parse(raw); 
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
    this.listeners.forEach(cb => cb(data));
  };

  /**
   * 发送消息到 C# 后端
   */
  send(type: string, payload?: any): void {
    const msg = JSON.stringify({ type, payload });
    if ((window as any).chrome?.webview) {
      console.log('[Bridge -> Host]', type, JSON.stringify(payload)?.substring(0, 100));
      (window as any).chrome.webview.postMessage(msg);
    } else {
      console.log('[Bridge -> Host] (standalone mode)', type, payload);
    }
  }

  /**
   * 发送消息并等待响应
   */
  sendAndWait(type: string, payload?: any, timeoutMs = 30000): Promise<any> {
    return new Promise((resolve, reject) => {
      const requestId = `req_${++this.requestCounter}_${Date.now()}`;
      const timeout = setTimeout(() => {
        this.pendingRequests.delete(requestId);
        reject(new Error(`Request ${type} timed out after ${timeoutMs}ms`));
      }, timeoutMs);
      
      this.pendingRequests.set(requestId, { resolve, reject, timeout });
      const msg = JSON.stringify({ type, payload, _requestId: requestId });
      
      if ((window as any).chrome?.webview) {
        (window as any).chrome.webview.postMessage(msg);
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
    return () => this.listeners.delete(callback);
  }

  /**
   * 等待特定类型的消息
   */
  once(type: string): Promise<{ type: string; payload: any }> {
    return new Promise(resolve => {
      const unsub = this.on(msg => {
        if (msg.type === type) { 
          unsub(); 
          resolve(msg); 
        }
      });
    });
  }

  // ============================================
  // 类型化便利方法（改进点）
  // ============================================

  /**
   * 检查 Bridge 是否可用
   */
  isAvailable(): boolean {
    return !!(window as any).chrome?.webview;
  }

  /**
   * 发送用户消息（类型安全）
   */
  sendUserMessage(text: string, images?: string[], files?: string[]): void {
    this.send('user:message', { text, images, files } as UserMessagePayload);
  }

  /**
   * 发送审批响应（类型安全）
   */
  sendApproval(toolId: string, allow: boolean): void {
    this.send('user:approve', { id: toolId, allow } as ApprovalPayload);
  }

  /**
   * 发送用户回答（类型安全）
   */
  sendAskResponse(questionId: string, answer: string): void {
    this.send('user:ask_response', { questionId, answer } as AskResponsePayload);
  }

  /**
   * 取消当前任务
   */
  cancel(): void {
    this.send('user:cancel');
  }

  /**
   * 创建新会话
   */
  newSession(): void {
    this.send('user:new_session');
  }

  /**
   * Ping 后端
   */
  ping(): Promise<any> {
    return this.sendAndWait('ping', null, 5000);
  }

  /**
   * 获取模型列表
   */
  listModels(): Promise<any> {
    return this.sendAndWait('models:list', null, 10000);
  }

  /**
   * 切换模型
   */
  switchModel(ref: string): Promise<any> {
    return this.sendAndWait('model:switch', { ref }, 10000);
  }

  /**
   * 获取 Provider 列表
   */
  listProviders(): Promise<any> {
    return this.sendAndWait('providers:list', null, 10000);
  }

  /**
   * 保存 Provider
   */
  saveProvider(provider: any): Promise<any> {
    return this.sendAndWait('provider:save', provider, 10000);
  }

  /**
   * 删除 Provider
   */
  deleteProvider(name: string): Promise<any> {
    return this.sendAndWait('provider:delete', { name }, 10000);
  }

  /**
   * 拉取 Provider 模型列表
   */
  fetchProviderModels(name: string): Promise<any> {
    return this.sendAndWait('provider:fetch_models', { name }, 15000);
  }

  /**
   * 设置 Provider API Key
   */
  setProviderKey(name: string, apiKey: string): Promise<any> {
    return this.sendAndWait('provider:set_key', { name, apiKey }, 10000);
  }

  // ============================================
  // 消息监听便利方法（改进点）
  // ============================================

  /**
   * 监听 LLM 流式输出
   */
  onLlmStreamDelta(callback: (delta: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'llm:stream:delta') {
        callback(msg.payload?.delta || '');
      }
    });
  }

  /**
   * 监听工具调度
   */
  onToolDispatch(callback: (id: string, name: string, args: any) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'tool:dispatch') {
        callback(msg.payload?.id || '', msg.payload?.name || '', msg.payload?.args);
      }
    });
  }

  /**
   * 监听工具结果
   */
  onToolResult(callback: (id: string, result?: string, error?: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'tool:result') {
        callback(msg.payload?.id || '', msg.payload?.result, msg.payload?.error);
      }
    });
  }

  /**
   * 监听 LLM 流式输出结束
   */
  onLlmStreamEnd(callback: (usage?: any) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'llm:stream:end') {
        callback(msg.payload?.usage);
      }
    });
  }

  /**
   * 监听 LLM 思考/推理内容
   */
  onLlmThinking(callback: (text: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'llm:thinking') {
        callback(msg.payload?.text || '');
      }
    });
  }

  /**
   * 监听工具错误
   */
  onToolError(callback: (id: string, error: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'tool:error') {
        callback(msg.payload?.id || '', msg.payload?.error || '');
      }
    });
  }

  /**
   * 监听工具审批请求
   */
  onToolApproval(callback: (id: string, name: string, args?: string, description?: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'tool:approval') {
        callback(msg.payload?.id || '', msg.payload?.name || '', msg.payload?.args, msg.payload?.description || '');
      }
    });
  }

  /**
   * 监听 AI 询问用户
   */
  onAskUser(callback: (questionId: string, question: string, data?: any) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'ask_user') {
        callback(msg.payload?.questionId || '', msg.payload?.question || '', msg.payload?.data);
      }
    });
  }

  /**
   * 监听宿主就绪
   */
  onHostReady(callback: (version: string, platform: string, timestamp: number) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'host:ready') {
        callback(msg.payload?.version || '', msg.payload?.platform || '', msg.payload?.timestamp || 0);
      }
    });
  }

  /**
   * 监听配置同步
   */
  onConfigSync(callback: (provider: string, model: string, baseUrl: string, apiKey: string, mode: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'config:sync') {
        callback(msg.payload?.provider || '', msg.payload?.model || '', msg.payload?.baseUrl || '', msg.payload?.apiKey || '', msg.payload?.mode || '');
      }
    });
  }

  /**
   * 监听 Pong 响应
   */
  onPong(callback: (timestamp: number) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'pong') {
        callback(msg.payload?.timestamp || 0);
      }
    });
  }

  /**
   * 监听模型列表响应
   */
  onModelsListResult(callback: (models: any[], currentProvider: string, currentModel: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'models:list:result') {
        callback(msg.payload?.models || [], msg.payload?.currentProvider || '', msg.payload?.currentModel || '');
      }
    });
  }

  /**
   * 监听 Provider 列表响应
   */
  onProvidersListResult(callback: (providers: any[], currentProvider: string, currentModel: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'providers:list:result') {
        callback(msg.payload?.providers || [], msg.payload?.currentProvider || '', msg.payload?.currentModel || '');
      }
    });
  }

  /**
   * 监听 Provider 模型拉取响应
   */
  onProviderFetchResult(callback: (providerName: string, success: boolean, models: string[], error?: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'provider:fetch_models:result') {
        callback(msg.payload?.providerName || '', msg.payload?.success || false, msg.payload?.models || [], msg.payload?.error);
      }
    });
  }

  /**
   * 监听通知消息
   */
  onNotice(callback: (text: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'notice') {
        callback(msg.payload?.text || '');
      }
    });
  }

  /**
   * 监听错误消息
   */
  onError(callback: (message: string) => void): () => void {
    return this.on((msg) => {
      if (msg.type === 'error') {
        callback(msg.payload?.message || 'Unknown error');
      }
    });
  }
}

const bridge = new Bridge();
export default bridge;
