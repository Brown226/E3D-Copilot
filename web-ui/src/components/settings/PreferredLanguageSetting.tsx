import { VSCodeDropdown, VSCodeOption } from "@/components/ui/vscode-compat"
import React from "react"
import { useTranslation } from "react-i18next"
import { useExtensionState } from "@/context/ExtensionStateContext"
import { updateSetting } from "./utils/settingsHandlers"

const PreferredLanguageSetting: React.FC = () => {
	const { t } = useTranslation("settings")
	const { preferredLanguage } = useExtensionState()

	const handleLanguageChange = (newLanguage: string) => {
		updateSetting("preferredLanguage", newLanguage)
	}

	return (
		<div style={{}}>
			<label className="block mb-1 text-base font-medium" htmlFor="preferred-language-dropdown">
				{t("settings.preferredLanguage")}
			</label>
			<VSCodeDropdown
				currentValue={preferredLanguage || "Simplified Chinese - "}
				id="preferred-language-dropdown"
				onChange={(e: any) => {
					handleLanguageChange(e.target.value)
				}}
				style={{ width: "100%" }}>
				<VSCodeOption value="English">English</VSCodeOption>
				<VSCodeOption value="Arabic - 19181719101411">Arabic - 19181719101411</VSCodeOption>
				<VSCodeOption value="Portuguese - Portugus (Brasil)">Portuguese - Portugus (Brasil)</VSCodeOption>
				<VSCodeOption value="Czech - 09e08tina">Czech - 09e08tina</VSCodeOption>
				<VSCodeOption value="French - Fran04ais">French - Fran04ais</VSCodeOption>
				<VSCodeOption value="German - Deutsch">German - Deutsch</VSCodeOption>
				<VSCodeOption value="Hindi - 151118151612">Hindi - 151118151612</VSCodeOption>
				<VSCodeOption value="Hungarian - Magyar">Hungarian - Magyar</VSCodeOption>
				<VSCodeOption value="Italian - Italiano">Italian - Italiano</VSCodeOption>
				<VSCodeOption value="Japanese - ձZ">Japanese - ձZ</VSCodeOption>
				<VSCodeOption value="Korean - 637025">Korean - 637025</VSCodeOption>
				<VSCodeOption value="Polish - Polski">Polish - Polski</VSCodeOption>
				<VSCodeOption value="Portuguese - Portugus (Portugal)">Portuguese - Portugus (Portugal)</VSCodeOption>
				<VSCodeOption value="Russian - ܧڧ">Russian - ܧڧ</VSCodeOption>
				<VSCodeOption value="Simplified Chinese - ">Simplified Chinese - </VSCodeOption>
				<VSCodeOption value="Spanish - Espa09ol">Spanish - Espa09ol</VSCodeOption>
				<VSCodeOption value="Traditional Chinese - w">Traditional Chinese - w</VSCodeOption>
				<VSCodeOption value="Turkish - Trk04e">Turkish - Trk04e</VSCodeOption>
			</VSCodeDropdown>
			<p className="text-sm text-description mt-1">{t("settings.preferredLanguageDescription")}</p>
		</div>
	)
}

export default React.memo(PreferredLanguageSetting)
