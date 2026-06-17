import { useCallback, useEffect } from "react"
import SettingsView from "./components/settings/SettingsView"
import ChatView from "./components/chat/ChatView"
import HistoryView from "./components/history/HistoryView"
import WelcomeView from "./components/welcome/WelcomeView"
import { useExtensionState } from "./context/ExtensionStateContext"
import { Providers } from "./Providers"

const AppContent = () => {
	const {
		didHydrateState,
		showWelcome,
		showSettings,
		settingsTargetSection,
		showHistory,
		hideSettings,
		hideHistory,
	} = useExtensionState()

	if (!didHydrateState) {
		return null
	}

	if (showWelcome) {
		return <WelcomeView />
	}

	return (
		<div className="flex h-screen w-full flex-col">
			{showSettings && <SettingsView onDone={hideSettings} targetSection={settingsTargetSection} />}
			{showHistory && <HistoryView onDone={hideHistory} />}
			<ChatView isHidden={showSettings || showHistory} showHistoryView={() => {}} />
		</div>
	)
}

const App = () => {
	return (
		<Providers>
			<AppContent />
		</Providers>
	)
}

export default App
