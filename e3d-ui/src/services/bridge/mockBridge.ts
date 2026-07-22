/**
 * Standalone Mock 模式
 * 职责：在无 WebView2 环境下模拟后端事件，供 UI 开发调试
 */

import { MessageTypes } from '../messageContracts';
import type { Bridge } from './BridgeCore';

let _mockTimers: ReturnType<typeof setTimeout>[] = [];

/**
 * 启动 Mock 模式：发送 host:ready + config:sync
 */
export function startMock(bridge: Bridge): void {
  console.log('[Mock] Standalone mock mode activated');
  // 立即发送 host:ready（无延迟，避免断连屏闪烁）
  bridge.handleIncoming(JSON.stringify({
    type: MessageTypes.HostReady,
    payload: { version: '2.0.0-mock', platform: 'standalone', timestamp: Date.now() },
  }));
  // config:sync（延迟 100ms 确保 store 映射已就绪）
  setTimeout(() => {
    bridge.handleIncoming(JSON.stringify({
      type: MessageTypes.ConfigSync,
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

/**
 * 模拟一轮完整的 AI 回复流程
 */
export function mockStreamResponse(bridge: Bridge, userText: string): void {
  console.log('[Mock] Simulating AI response for:', userText);

  // llm:turn_started
  _mockTimers.push(setTimeout(() => {
    bridge.handleIncoming(JSON.stringify({ type: MessageTypes.LlmTurnStarted, payload: {} }));
  }, 2000));

  // llm:thinking
  _mockTimers.push(setTimeout(() => {
    bridge.handleIncoming(JSON.stringify({
      type: MessageTypes.LlmThinking,
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
    _mockTimers.push(setTimeout(() => {
      bridge.handleIncoming(JSON.stringify({
        type: MessageTypes.LlmStreamDelta,
        payload: { delta: chunk },
      }));
    }, 3000 + i * 400));
  });

  // llm:stream:end
  _mockTimers.push(setTimeout(() => {
    bridge.handleIncoming(JSON.stringify({
      type: MessageTypes.LlmStreamEnd,
      payload: { usage: { prompt_tokens: 50, completion_tokens: 80, total_tokens: 130 } },
    }));
  }, 3000 + replyChunks.length * 400 + 200));

  // tool:dispatch
  const toolDelay = 3000 + replyChunks.length * 400 + 800;
  _mockTimers.push(setTimeout(() => {
    bridge.handleIncoming(JSON.stringify({
      type: MessageTypes.ToolDispatch,
      payload: { id: 'mock_tool_001', name: 'search_catalog', args: { query: userText.substring(0, 30) } },
    }));
  }, toolDelay));

  // tool:result
  _mockTimers.push(setTimeout(() => {
    bridge.handleIncoming(JSON.stringify({
      type: MessageTypes.ToolResult,
      payload: { id: 'mock_tool_001', result: JSON.stringify({ found: 3, items: ['Pump A', 'Valve B', 'Filter C'] }) },
    }));
  }, toolDelay + 1000));

  // turn:done
  _mockTimers.push(setTimeout(() => {
    bridge.handleIncoming(JSON.stringify({ type: MessageTypes.TurnDone, payload: {} }));
  }, toolDelay + 1500));
}

/**
 * 清理 mock 定时器
 */
export function stopMock(): void {
  _mockTimers.forEach(t => clearTimeout(t));
  _mockTimers = [];
}
