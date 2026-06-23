// E3D: Terminal settings removed — no VSCode terminal concept in E3D desktop app
// This component is kept as a stub to avoid import errors but renders nothing.

interface TerminalSettingsSectionProps {
	renderSectionHeader: (tabId: string) => JSX.Element | null
}

const TerminalSettingsSection: React.FC<TerminalSettingsSectionProps> = () => {
	return null
}

export default TerminalSettingsSection
