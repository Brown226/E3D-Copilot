import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import './index.css'

// 关键：必须在 React 渲染前同步导入 bridgeService
// C# 在 NavigationCompleted 事件中发送 host:ready/config:sync，
// 如果 bridge 监听器未注册，这些消息会丢失，导致永久断连
import './services/bridgeService'

import App from './App'

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <App />
  </StrictMode>,
)
