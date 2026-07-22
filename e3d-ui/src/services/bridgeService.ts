/**
 * E小智 WebView2 Bridge Service
 * 组装入口：实例化 Bridge，注册 store 映射，初始化重连/mock
 */

import { Bridge } from './bridge/BridgeCore';
import { registerStoreMappings } from './bridge/storeMapping';
import { initReconnect } from './bridge/reconnect';
import { startMock, mockStreamResponse, stopMock } from './bridge/mockBridge';

export { MessageTypes } from './messageContracts';
export type { Bridge } from './bridge/BridgeCore';

// ============================================
// 初始化 Bridge 单例
// ============================================

const bridge = new Bridge();

// 注册 store 映射
registerStoreMappings(bridge);

// 初始化重连机制
initReconnect(bridge);

// Standalone 模式：立即启动 mock（无延迟，避免断连屏闪烁）
if (!bridge.isAvailable()) {
  Promise.resolve().then(() => startMock(bridge));
}

// 挂载 mock 方法到 bridge 实例（供外部调用）
(bridge as any).startMock = () => startMock(bridge);
(bridge as any).mockStreamResponse = (text: string) => mockStreamResponse(bridge, text);
(bridge as any).stopMock = () => stopMock();

export default bridge;
