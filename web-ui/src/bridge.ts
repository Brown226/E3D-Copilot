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
