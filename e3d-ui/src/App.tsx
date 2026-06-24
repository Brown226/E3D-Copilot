import React, { Suspense } from 'react'
import { useChatStore } from './store/useChatStore'
import { useHeartbeat } from './hooks/useHeartbeat'
import { useKeyboardShortcuts } from './hooks/useKeyboardShortcuts'
import { ErrorBoundary } from './components/ErrorBoundary'
import { DisconnectScreen } from './components/DisconnectScreen'
import { Header } from './components/Header'
import { WelcomeScreen } from './components/chat/WelcomeScreen'
import { MessageList } from './components/chat/MessageList'
import { InputBar } from './components/chat/InputBar'
import { HistoryPanel } from './components/HistoryPanel'
import { ToastContainer } from './components/common/Toast'
import { CommandPalette } from './components/CommandPalette'
import { TabBar } from './components/TabBar'

const SettingsPanel = React.lazy(() => import('./components/settings/SettingsPanel'))

function AppInner() {
  useHeartbeat()
  useKeyboardShortcuts()

  // 从当前 tab 读取消息状态
  const messages = useChatStore((s) => s.tabs.find((t) => t.id === s.activeTabId)?.messages ?? [])
  const isStreaming = useChatStore((s) => s.tabs.find((t) => t.id === s.activeTabId)?.isStreaming ?? false)

  const showWelcome = messages.length === 0 && !isStreaming

  return (
    <div className="h-full bg-slate-50 dark:bg-slate-900 flex flex-col overflow-hidden">
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
      <HistoryPanel />
      <Suspense fallback={<div className="absolute inset-0 z-[var(--z-modal)] flex items-center justify-center bg-white/80 dark:bg-slate-900/80 backdrop-blur-sm"><span className="text-slate-500 dark:text-slate-400 text-sm">加载中...</span></div>}>
        <SettingsPanel />
      </Suspense>
      <ToastContainer />
      <CommandPalette />
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
