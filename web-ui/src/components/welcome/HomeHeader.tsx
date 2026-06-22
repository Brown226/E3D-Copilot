import { useTranslation } from "react-i18next"
import { useExtensionState } from "@/context/ExtensionStateContext"

interface HomeHeaderProps {
	shouldShowQuickWins?: boolean
}

const HomeHeader = ({ shouldShowQuickWins = false }: HomeHeaderProps) => {
	const { t } = useTranslation("common")
	const headingText = t("homeHeader.defaultHeading")

	return (
		<div className="flex flex-col items-center mb-5">
			<div className="my-7">
				{/* E小智 Logo */}
				<svg className="size-20" viewBox="0 0 100 100" fill="none" xmlns="http://www.w3.org/2000/svg">
					<circle cx="50" cy="50" r="45" stroke="currentColor" strokeWidth="3" fill="none" opacity="0.3" />
					<text x="50" y="38" textAnchor="middle" fill="currentColor" fontSize="18" fontWeight="bold">E3D</text>
					<text x="50" y="58" textAnchor="middle" fill="currentColor" fontSize="12" opacity="0.7">E小智</text>
					<circle cx="50" cy="50" r="45" stroke="currentColor" strokeWidth="3" fill="none" strokeDasharray="8 4" opacity="0.15" />
				</svg>
			</div>
			<div className="text-center flex items-center justify-center px-4">
				<h1 className="m-0 font-bold">{headingText}</h1>
			</div>
		</div>
	)
}

export default HomeHeader
