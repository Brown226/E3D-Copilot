/**
 * E小智 v2.0 心跳检测 Hook
 * 每 30s 发送 ping，10s 内无 pong 则标记断连
 */

import { useEffect, useRef } from 'react';
import { useChatStore } from '../store/useChatStore';

export function useHeartbeat() {
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  useEffect(() => {
    // 动态导入 bridge 以避免循环依赖
    const loadAndStart = async () => {
      const { default: bridge } = await import('../services/bridgeService');

      // standalone 模式不启动心跳
      if (!bridge.isAvailable()) return;

      const PING_INTERVAL = 30_000;  // 30 秒
      const PONG_TIMEOUT = 10_000;   // 10 秒

      intervalRef.current = setInterval(() => {
        const { setBridgeConnected, lastPingTime } = useChatStore.getState();

        // 如果上次 ping 超过 10s 无 pong，标记断连
        if (lastPingTime && Date.now() - lastPingTime > PONG_TIMEOUT) {
          setBridgeConnected(false);
        }

        // 发送 ping
        bridge.ping().catch(() => {
          setBridgeConnected(false);
        });
      }, PING_INTERVAL);
    };

    loadAndStart();

    return () => {
      if (intervalRef.current) {
        clearInterval(intervalRef.current);
        intervalRef.current = null;
      }
    };
  }, []);
}
