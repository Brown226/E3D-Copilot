import React, { Suspense } from 'react'
import { useActiveTab } from './store/useChatStore'
import { useHeartbeat } from './hooks/useHeartbeat'
import { useKeyboardShortcuts } from './hooks/useKeyboardShortcuts'
import { ErrorBoundary } from './components/ErrorBoundary'
import { DisconnectScreen } from './components/DisconnectScreen'
import { Header } from './components/Header'
import { WelcomeScreen } from './components/chat/WelcomeScreen'
import { MessageList } from './components/chat/MessageList'
import { InputBar } from './components/chat/InputBar'
import { ToastContainer } from './components/common/Toast'
import { TabBar } from './components/TabBar'

const SettingsPanel = React.lazy(() => import('./components/settings/SettingsPanel'))
const HistoryPanel = React.lazy(() => import('./components/HistoryPanel').then(m => ({ default: m.HistoryPanel })))
const CommandPalette = React.lazy(() => import('./components/CommandPalette').then(m => ({ default: m.CommandPalette })))

function AppInner() {
  useHeartbeat()
  useKeyboardShortcuts()

  // 从当前 tab 读取消息状态
  const { messages, isStreaming } = useActiveTab()

  const showWelcome = messages.length === 0 && !isStreaming

  return (
    <div className="h-full bg-slate-50 dark:bg-slate-900 flex flex-col">
      <DisconnectScreen />
      <Header />
      <TabBar />
      {showWelcome ? (
        <main className="flex-1 flex flex-col overflow-hidden">
          <WelcomeScreen />
        </main>
      ) : (
        <MessageList />
      )}
      <InputBar />
      <Suspense fallback={null}>
        <HistoryPanel />
        <CommandPalette />
      </Suspense>
      <Suspense fallback={<div className="absolute inset-0 z-[var(--z-modal)] flex items-center justify-center bg-white/80 dark:bg-slate-900/80 backdrop-blur-sm"><span className="text-slate-500 dark:text-slate-400 text-sm">加载中...</span></div>}>
        <SettingsPanel />
      </Suspense>
      <ToastContainer />
    </div>
  )
}

export default function App() {
  return (
    <ErrorBoundary>
      <AppInner />
    </ErrorBoundary>
  )
}
