/**
 * E小智 WebView2 Bridge — C# ↔ React 双向通信桥
 * 使用 WebView2 原生 chrome.webview 进行通信
 */

import type { WebviewMessage, ExtensionMessage } from "./bridge-messages"

type MessageCallback = (msg: { type: string; payload: any }) => void

class Bridge {
  private listeners = new Set<MessageCallback>()
  private pendingRequests = new Map<string, { resolve: (v: any) => void; reject: (e: Error) => void; timeout: NodeJS.Timeout }>()
  private requestCounter = 0

  constructor() {
    if ((window as any).chrome?.webview) {
      ;(window as any).chrome.webview.addEventListener("message", (event: any) => {
        this.handleIncoming(event.data)
      })
    }
  }

  private handleIncoming = (raw: string) => {
    let data: any
    try { data = JSON.parse(raw) } catch { return }
    if (data._requestId && this.pendingRequests.has(data._requestId)) {
      const pending = this.pendingRequests.get(data._requestId)!
      clearTimeout(pending.timeout)
      this.pendingRequests.delete(data._requestId)
      pending.resolve(data.payload)
      return
    }
    this.listeners.forEach(cb => cb(data))
  }

  send(type: string, payload?: any) {
    const msg = JSON.stringify({ type, payload })
    if ((window as any).chrome?.webview) {
      ;(window as any).chrome.webview.postMessage(msg)
    } else {
      console.log("[Bridge -> Host]", type, payload)
    }
  }

  sendAndWait(type: string, payload?: any, timeoutMs = 30000): Promise<any> {
    return new Promise((resolve, reject) => {
      const requestId = `req_${++this.requestCounter}_${Date.now()}`
      const timeout = setTimeout(() => {
        this.pendingRequests.delete(requestId)
        reject(new Error(`Request ${type} timed out after ${timeoutMs}ms`))
      }, timeoutMs)
      this.pendingRequests.set(requestId, { resolve, reject, timeout })
      const msg = JSON.stringify({ type, payload, _requestId: requestId })
      if ((window as any).chrome?.webview) {
        ;(window as any).chrome.webview.postMessage(msg)
      } else {
        console.log("[Bridge -> Host]", type, payload, "(requestId:", requestId, ")")
      }
    })
  }

  on(callback: MessageCallback): () => void {
    this.listeners.add(callback)
    return () => this.listeners.delete(callback)
  }

  once(type: string): Promise<{ type: string; payload: any }> {
    return new Promise(resolve => {
      const unsub = this.on(msg => {
        if (msg.type === type) { unsub(); resolve(msg) }
      })
    })
  }

  // ── 类型化便利方法 ──

  isAvailable(): boolean {
    return !!(window as any).chrome?.webview
  }

  sendMessage(msg: WebviewMessage): void {
    this.send(msg.type, msg)
  }

  sendMessageAndWait<T = unknown>(msg: WebviewMessage, timeoutMs = 30000): Promise<T> {
    return this.sendAndWait(msg.type, msg, timeoutMs)
  }

  onMessage(handler: (msg: ExtensionMessage) => void): () => void {
    return this.on((raw) => {
      const { type, payload = {}, _requestId: _rid } = raw as any
      const msg = { type, ...payload } as ExtensionMessage
      handler(msg)
    })
  }

  waitMessage<T extends ExtensionMessage["type"]>(
    type: T,
    timeoutMs = 30000,
  ): Promise<Extract<ExtensionMessage, { type: T }>> {
    return new Promise((resolve, reject) => {
      const timeout = setTimeout(() => { unsub(); reject(new Error(`waitMessage(${type}) timed out`)) }, timeoutMs)
      const unsub = this.onMessage((msg) => {
        if (msg.type === type) { clearTimeout(timeout); unsub(); resolve(msg as Extract<ExtensionMessage, { type: T }>) }
      })
    })
  }
}

const bridge = new Bridge()
export default bridge
