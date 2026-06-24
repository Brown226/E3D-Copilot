/**
 * E小智 v2.0 DisconnectScreen
 * 全屏覆盖，显示断连提示和重连按钮
 */

import { useState, useCallback } from 'react';
import { useChatStore } from '../store/useChatStore';

export function DisconnectScreen() {
  const bridgeConnected = useChatStore((s) => s.bridgeConnected);
  const [isReconnecting, setIsReconnecting] = useState(false);

  const handleReconnect = useCallback(async () => {
    setIsReconnecting(true);
    try {
      const { default: bridge } = await import('../services/bridgeService');
      await bridge.ping();
      useChatStore.getState().setBridgeConnected(true);
    } catch {
      // ping 失败，保持断连状态
      useChatStore.getState().setBridgeConnected(false);
    } finally {
      setIsReconnecting(false);
    }
  }, []);

  // 连接正常时不渲染
  if (bridgeConnected) return null;

  return (
    <div className="fixed inset-0 z-50 bg-slate-900/80 backdrop-blur-sm flex items-center justify-center p-6">
      <div className="max-w-md w-full bg-white dark:bg-slate-800 rounded-2xl shadow-2xl p-8 space-y-6 text-center">
        {/* 断连图标 */}
        <div className="w-20 h-20 rounded-full bg-amber-100 dark:bg-amber-900/30 flex items-center justify-center mx-auto">
          <svg
            className="w-10 h-10 text-amber-500"
            fill="none"
            viewBox="0 0 24 24"
            stroke="currentColor"
          >
            <path
              strokeLinecap="round"
              strokeLinejoin="round"
              strokeWidth={2}
              d="M18.364 5.636a9 9 0 010 12.728m0 0l-2.829-2.829m2.829 2.829L21 21M15.536 8.464a5 5 0 010 7.072m0 0l-2.829-2.829m-4.242 2.829a4.978 4.978 0 01-1.414-2.83m-1.414 5.658a9 9 0 01-2.167-9.238m7.824 2.167a1 1 0 111.414 1.414m-1.414-1.414L3 3"
            />
          </svg>
        </div>

        {/* 提示文字 */}
        <div className="space-y-2">
          <h2 className="text-2xl font-bold text-slate-800 dark:text-slate-100">与 E3D 的连接已断开</h2>
          <p className="text-slate-500 dark:text-slate-400">
            请检查 E3D 是否仍在运行，然后尝试重新连接。
          </p>
        </div>

        {/* 重新连接按钮 */}
        <button
          onClick={handleReconnect}
          disabled={isReconnecting}
          className="w-full px-6 py-3 bg-blue-600 hover:bg-blue-700 disabled:bg-blue-400 text-white font-medium rounded-xl transition-colors flex items-center justify-center gap-2"
        >
          {isReconnecting ? (
            <>
              <svg className="w-5 h-5 animate-spin" fill="none" viewBox="0 0 24 24">
                <circle className="opacity-25" cx="12" cy="12" r="10" stroke="currentColor" strokeWidth="4" />
                <path className="opacity-75" fill="currentColor" d="M4 12a8 8 0 018-8V0C5.373 0 0 5.373 0 12h4z" />
              </svg>
              正在重新连接...
            </>
          ) : (
            '重新连接'
          )}
        </button>
      </div>
    </div>
  );
}
