import { useTranslation } from "react-i18next"
import Section from "../Section"

interface AboutSectionProps {
	version: string
	renderSectionHeader: (tabId: string) => JSX.Element | null
}
const AboutSection = ({ version, renderSectionHeader }: AboutSectionProps) => {
	const { t } = useTranslation("settings")
	return (
		<div>
			{renderSectionHeader("about")}
			<Section>
				<div className="flex px-4 flex-col gap-2">
					<h2 className="text-lg font-semibold">{t("settingsSections.aboutVersion", { version })}</h2>
					<p>{t("settingsSections.aboutDescription")}</p>

					<h3 className="text-md font-semibold">{t("settingsSections.resources")}</h3>
					<p>E3D-E小智 — AVEVA E3D AI 编程助手</p>
					<p>仅供内网离线使用</p>
				</div>
			</Section>
		</div>
	)
}

export default AboutSection
