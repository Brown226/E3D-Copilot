import { useTranslation } from "react-i18next"
import PreferredLanguageSetting from "../PreferredLanguageSetting"
import Section from "../Section"

interface GeneralSettingsSectionProps {
	renderSectionHeader: (tabId: string) => JSX.Element | null
}

const GeneralSettingsSection = ({ renderSectionHeader }: GeneralSettingsSectionProps) => {
	// E3D: Telemetry and Remote Config removed — only language setting remains
	return (
		<div>
			{renderSectionHeader("general")}
			<Section>
				<PreferredLanguageSetting />
			</Section>
		</div>
	)
}

export default GeneralSettingsSection
