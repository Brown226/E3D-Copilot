import { useCallback, useEffect } from "react"
import ChatView from "./components/chat/ChatView"
import HistoryView from "./components/history/HistoryView"
import OnboardingView from "./components/onboarding/OnboardingView"
import SettingsView from "./components/settings/SettingsView"
import { useExtensionState } from "./context/ExtensionStateContext"
import { Providers } from "./Providers"

const AppContent = () => {
	const {
		didHydrateState,
		showWelcome,
		shouldShowAnnouncement,
		showSettings,
		settingsTargetSection,
		showHistory,
		showAnnouncement,
		setShowAnnouncement,
		setShouldShowAnnouncement,
		navigateToHistory,
		hideSettings,
		hideHistory,
		hideAnnouncement,
	} = useExtensionState()

	const showUpdateAnnouncementModal = useCallback(() => {
		setShowAnnouncement(true)
		setShouldShowAnnouncement(false)
	}, [setShouldShowAnnouncement, setShowAnnouncement])

	useEffect(() => {
		if (!didHydrateState || showWelcome || !shouldShowAnnouncement || showAnnouncement) return
		showUpdateAnnouncementModal()
	}, [didHydrateState, showWelcome, shouldShowAnnouncement, showAnnouncement, showUpdateAnnouncementModal])

	if (!didHydrateState) return null
	if (showWelcome) return <OnboardingView />

	return (
		<div className="flex h-screen w-full flex-col">
			{showSettings && <SettingsView onDone={hideSettings} targetSection={settingsTargetSection} />}
			{showHistory && <HistoryView onDone={hideHistory} />}
			<ChatView
				hideAnnouncement={hideAnnouncement}
				isHidden={showSettings || showHistory}
				showAnnouncement={showAnnouncement}
				showHistoryView={navigateToHistory}
			/>
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
