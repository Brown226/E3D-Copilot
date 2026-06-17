/**
 * E小智 WebView2 Bridge — C# ↔ React 双向通信桥
 *
 * 替代 cline-chinese-main 的 VSCode acquireVsCodeApi().postMessage()。
 * E3D 中使用 WebView2 的 chrome.webview.postMessage() / onmessage。
 */

type MessageCallback = (msg: { type: string; payload: any }) => void

class Bridge {
  private listeners = new Set<MessageCallback>()
  private pendingRequests = new Map<string, { resolve: (v: any) => void; reject: (e: Error) => void; timeout: NodeJS.Timeout }>()
  private requestCounter = 0

  constructor() {
    // 监听来自 C# 后端的消息
    window.addEventListener("message", this.handleMessage)
  }

  private handleMessage = (event: MessageEvent) => {
    let data: any
    if (typeof event.data === "string") {
      try { data = JSON.parse(event.data) } catch { return }
    } else if (event.data && typeof event.data === "object") {
      data = event.data
    } else {
      return
    }

    // 处理有 requestId 的响应（sendAndWait）
    if (data._requestId && this.pendingRequests.has(data._requestId)) {
      const pending = this.pendingRequests.get(data._requestId)!
      clearTimeout(pending.timeout)
      this.pendingRequests.delete(data._requestId)
      pending.resolve(data.payload)
      return
    }

    // 广播到所有监听器
    this.listeners.forEach(cb => cb(data))
  }

  /**
   * 发送消息到 C# 后端（fire-and-forget）
   */
  send(type: string, payload?: any) {
    const msg = JSON.stringify({ type, payload })
    if ((window as any).chrome?.webview) {
      ;(window as any).chrome.webview.postMessage(msg)
    } else {
      console.log("[Bridge → Host]", type, payload)
    }
  }

  /**
   * 发送消息并等待响应
   */
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
        console.log("[Bridge → Host]", type, payload, "(requestId:", requestId, ")")
      }
    })
  }

  /**
   * 注册消息监听器
   */
  on(callback: MessageCallback): () => void {
    this.listeners.add(callback)
    return () => this.listeners.delete(callback)
  }

  /**
   * 等待特定类型的消息
   */
  once(type: string): Promise<{ type: string; payload: any }> {
    return new Promise(resolve => {
      const unsub = this.on(msg => {
        if (msg.type === type) {
          unsub()
          resolve(msg)
        }
      })
    })
  }
}

const bridge = new Bridge()
export default bridge
