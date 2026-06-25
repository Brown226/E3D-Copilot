/**
 * E小智 v2.0 前端核心类型定义
 */

// 全局变量声明（由 config:sync 设置）
declare global {
  interface Window {
    __E3D_VERSION__?: string;
    __E3D_ABOUT_URL__?: string;
  }
}

// ============================================
// 消息角色
// ============================================

export type MessageRole = 'user' | 'assistant' | 'thinking' | 'tool_call' | 'tool_result' | 'error';

// ============================================
// 消息定义
// ============================================

export interface Message {
  id: string;
  role: MessageRole;
  content: string;
  timestamp: number;
  /** tool_call 专用 */
  toolId?: string;
  toolName?: string;
  toolArgs?: unknown;
  /** 子代理嵌套：父工具调用 ID */
  parentId?: string;
  /** tool_result / assistant 专用：错误信息 */
  toolError?: string;
  /** assistant 专用：LLM 返回的错误信息（用于空回复时诊断） */
  errorMessage?: string;
  /** 工具执行耗时（毫秒） */
  durationMs?: number;
  /** 是否已完成（流式结束） */
  finalized?: boolean;
  /** 附件（用户消息） */
  attachments?: Attachment[];
}

// ============================================
// 附件类型
// ============================================

export interface Attachment {
  id: string;
  name: string;
  type: string;
  size: number;
  previewUrl?: string;
  data?: string; // base64
}

// ============================================
// 生成消息 ID 的工具函数
// ============================================

export function generateMessageId(): string {
  return Date.now().toString(36) + Math.random().toString(36).slice(2);
}
