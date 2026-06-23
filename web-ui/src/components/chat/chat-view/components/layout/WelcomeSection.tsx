import React, { useCallback, useEffect, useMemo, useState } from "react"
import { useTranslation } from "react-i18next"
import BannerCarousel from "@/components/common/BannerCarousel"
import WhatsNewModal from "@/components/common/WhatsNewModal"
import HistoryPreview from "@/components/history/HistoryPreview"
import { useApiConfigurationHandlers } from "@/components/settings/utils/useApiConfigurationHandlers"
import HomeHeader from "@/components/welcome/HomeHeader"
import { SuggestedTasks } from "@/components/welcome/SuggestedTasks"
import { useExtensionState } from "@/context/ExtensionStateContext"
import { StateServiceClient, UiServiceClient } from "@/services/grpc-client"
import { convertBannerData } from "@/utils/bannerUtils"
import { getCurrentPlatform } from "@/utils/platformUtils"
import { BANNER_DATA, BannerAction, BannerActionType, BannerCardData } from "@shared/cline/banner"
import { WelcomeSectionProps } from "../../types/chatTypes"

/**
 * Welcome section shown when there's no active conversation
 * Common AI tool layout: centered header + suggested tasks at bottom
 */
export const WelcomeSection: React.FC<WelcomeSectionProps> = ({
	showAnnouncement,
	hideAnnouncement,
	showHistoryView,
	version,
	taskHistory,
	shouldShowQuickWins,
}) => {
	const { t } = useTranslation("common")
	const { lastDismissedInfoBannerVersion, lastDismissedCliBannerVersion, lastDismissedModelBannerVersion, dismissedBanners } =
		useExtensionState()

	const [hasShownWhatsNewModal, setHasShownWhatsNewModal] = useState(false)
	const [showWhatsNewModal, setShowWhatsNewModal] = useState(false)

	const { openRouterModels, navigateToSettings, navigateToSettingsModelPicker, banners, welcomeBanners } = useExtensionState()
	const { handleFieldsChange } = useApiConfigurationHandlers()

	useEffect(() => {
		if (showAnnouncement && !hasShownWhatsNewModal && welcomeBanners && welcomeBanners.length > 0) {
			setShowWhatsNewModal(true)
			setHasShownWhatsNewModal(true)
		}
	}, [welcomeBanners, showAnnouncement, hasShownWhatsNewModal])

	const handleCloseWhatsNewModal = useCallback(() => {
		setShowWhatsNewModal(false)
		hideAnnouncement()
		if (welcomeBanners && welcomeBanners.length > 0) {
			for (const banner of welcomeBanners) {
				StateServiceClient.dismissBanner({ value: banner.id }).catch(console.error)
			}
		}
	}, [hideAnnouncement, welcomeBanners])

	const isBannerDismissed = useCallback(
		(bannerId: string): boolean => {
			if (dismissedBanners?.some((dismissed: { bannerId: string; dismissedAt: number }) => dismissed.bannerId === bannerId)) {
				return true
			}
			if (bannerId.startsWith("info-banner")) {
				return (lastDismissedInfoBannerVersion ?? 0) >= 1
			}
			if (bannerId.startsWith("new-model")) {
				return (lastDismissedModelBannerVersion ?? 0) >= 1
			}
			if (bannerId.startsWith("cli-")) {
				return (lastDismissedCliBannerVersion ?? 0) >= 1
			}
			return false
		},
		[dismissedBanners, lastDismissedInfoBannerVersion, lastDismissedModelBannerVersion, lastDismissedCliBannerVersion],
	)

	const bannerConfig = useMemo((): BannerCardData[] => {
		return BANNER_DATA.filter((banner) => {
			if (isBannerDismissed(banner.id)) return false
			if (banner.isClineUserOnly) return false
			if (banner.platforms && !banner.platforms.includes(getCurrentPlatform())) return false
			return true
		})
	}, [isBannerDismissed])

	const handleBannerAction = useCallback(
		(action: BannerAction) => {
			switch (action.action) {
				case BannerActionType.Link:
					if (action.arg) UiServiceClient.openUrl({ value: action.arg }).catch(console.error)
					break
				case BannerActionType.SetModel: {
					const modelId = action.arg || "anthropic/claude-sonnet-4.5"
					const initialModelTab = action.tab || "recommended"
					handleFieldsChange({
						planModeOpenRouterModelId: modelId,
						actModeOpenRouterModelId: modelId,
						planModeOpenRouterModelInfo: openRouterModels[modelId],
						actModeOpenRouterModelInfo: openRouterModels[modelId],
						planModeApiProvider: "cline",
						actModeApiProvider: "cline",
					})
					navigateToSettingsModelPicker({ targetSection: "api-config", initialModelTab })
					break
				}
				case BannerActionType.ShowAccount:
					break
				case BannerActionType.ShowApiSettings:
					if (action.arg) {
						handleFieldsChange({
							planModeApiProvider: action.arg as any,
							actModeApiProvider: action.arg as any,
						})
					}
					navigateToSettings("api-config")
					break
				case BannerActionType.ShowFeatureSettings:
					navigateToSettings("features")
					break
				case BannerActionType.InstallCli:
					StateServiceClient.installClineCli({}).catch((error) =>
						console.error("Failed to initiate CLI installation:", error),
					)
					break
				default:
					console.warn("Unknown banner action:", action.action)
			}
		},
		[handleFieldsChange, openRouterModels, navigateToSettings, navigateToSettingsModelPicker],
	)

	const handleBannerDismiss = useCallback((bannerId: string) => {
		if (bannerId.startsWith("info-banner")) {
			StateServiceClient.updateInfoBannerVersion({ value: 1 }).catch(console.error)
		} else if (bannerId.startsWith("new-model")) {
			StateServiceClient.updateModelBannerVersion({ value: 1 }).catch(console.error)
		} else if (bannerId.startsWith("cli-")) {
			StateServiceClient.updateCliBannerVersion({ value: 1 }).catch(console.error)
		} else {
			StateServiceClient.dismissBanner({ value: bannerId }).catch(console.error)
		}
	}, [])

	const activeBanners = useMemo(() => {
		const hardcodedBanners = bannerConfig.map((banner) =>
			convertBannerData(banner, {
				onAction: handleBannerAction,
				onDismiss: handleBannerDismiss,
			}),
		)
		const extensionStateBanners = (banners ?? []).map((banner) =>
			convertBannerData(banner, {
				onAction: handleBannerAction,
				onDismiss: handleBannerDismiss,
			}),
		)
		return [...extensionStateBanners, ...hardcodedBanners]
	}, [bannerConfig, banners, handleBannerAction, handleBannerDismiss])

	return (
		<div className="flex flex-col flex-1 w-full h-full p-0 m-0 overflow-hidden">
			<WhatsNewModal
				onBannerAction={handleBannerAction}
				onClose={handleCloseWhatsNewModal}
				open={showWhatsNewModal}
				version={version}
				welcomeBanners={welcomeBanners}
			/>
			{/* 顶部轮播公告 */}
			<div className="shrink-0 px-4 pt-4">
				{activeBanners.length > 0 && <BannerCarousel banners={activeBanners} />}
			</div>

			{/* 中间：品牌标题 + 历史预览 */}
			<div className="flex-1 flex flex-col items-center justify-center overflow-y-auto px-4 pb-4">
				<HomeHeader shouldShowQuickWins={shouldShowQuickWins} />
				{!showWhatsNewModal && !shouldShowQuickWins && taskHistory.length > 0 && (
					<div className="w-full max-w-2xl mt-6">
						<HistoryPreview showHistoryView={showHistoryView} />
					</div>
				)}
			</div>

			{/* 底部：建议任务 */}
			<div className="shrink-0 w-full max-w-2xl mx-auto px-4 pb-4">
				<SuggestedTasks shouldShowQuickWins={true} />
			</div>
		</div>
	)
}
