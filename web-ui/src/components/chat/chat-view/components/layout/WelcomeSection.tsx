import { BANNER_DATA, BannerAction, BannerActionType, BannerCardData } from "@shared/cline/banner"
import React, { useCallback, useEffect, useMemo, useState } from "react"
import { useTranslation } from "react-i18next"
import BannerCarousel from "@/components/common/BannerCarousel"
import WhatsNewModal from "@/components/common/WhatsNewModal"
import HistoryPreview from "@/components/history/HistoryPreview"
import { useApiConfigurationHandlers } from "@/components/settings/utils/useApiConfigurationHandlers"
import { Tooltip, TooltipContent, TooltipTrigger } from "@/components/ui/tooltip"
import HomeHeader from "@/components/welcome/HomeHeader"
import { SuggestedTasks } from "@/components/welcome/SuggestedTasks"
import { useExtensionState } from "@/context/ExtensionStateContext"
import { StateServiceClient, UiServiceClient } from "@/services/grpc-client"
import { convertBannerData } from "@/utils/bannerUtils"
import { getCurrentPlatform } from "@/utils/platformUtils"
import { WelcomeSectionProps } from "../../types/chatTypes"

/**
 * Welcome section shown when there's no active task
 * Includes info banner, announcements, home header, and history preview
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

	// Track if we've shown the "What's New" modal this session
	const [hasShownWhatsNewModal, setHasShownWhatsNewModal] = useState(false)
	const [showWhatsNewModal, setShowWhatsNewModal] = useState(false)

	// E3D: no worktree support
	const [showCreateWorktreeModal] = useState(false)

	const {
		openRouterModels,
		navigateToSettings,
		navigateToSettingsModelPicker,
		banners,
		welcomeBanners,
	} = useExtensionState()
	const { handleFieldsChange } = useApiConfigurationHandlers()

	// Open modal once we have welcome banners
	useEffect(() => {
		if (showAnnouncement && !hasShownWhatsNewModal && welcomeBanners && welcomeBanners.length > 0) {
			setShowWhatsNewModal(true)
			setHasShownWhatsNewModal(true)
		}
	}, [welcomeBanners, showAnnouncement, hasShownWhatsNewModal])

	const handleCloseWhatsNewModal = useCallback(() => {
		setShowWhatsNewModal(false)
		// Call hideAnnouncement to persist dismissal (same as old banner behavior)
		hideAnnouncement()
		if (welcomeBanners && welcomeBanners.length > 0) {
			for (const banner of welcomeBanners) {
				StateServiceClient.dismissBanner({ value: banner.id }).catch(console.error)
			}
		}
	}, [hideAnnouncement, welcomeBanners])

	// E3D: worktree not supported

	/**
	 * Check if a banner has been dismissed based on its ID or legacy version
	 */
	const isBannerDismissed = useCallback(
		(bannerId: string): boolean => {
			// Check if banner is in the dismissed banners list (new approach)
			if (
				dismissedBanners?.some((dismissed: { bannerId: string; dismissedAt: number }) => dismissed.bannerId === bannerId)
			) {
				return true
			}

			// Legacy version-based tracking (deprecated)
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

	/**
	 * Banner configuration from backend
	 * In production, this would come from an API/gRPC call
	 * For now, using EXAMPLE_BANNER_DATA with version-based filtering
	 */
	const bannerConfig = useMemo((): BannerCardData[] => {
		return BANNER_DATA.filter((banner) => {
			if (isBannerDismissed(banner.id)) {
				return false
			}

			if (banner.isClineUserOnly) {
				return false // E3D: no Cline auth, hide Cline-only banners
			}

			if (banner.platforms && !banner.platforms.includes(getCurrentPlatform())) {
				return false
			}

			return true
		})
	}, [isBannerDismissed])

	/**
	 * Action handler - maps action types to actual implementations
	 */
	const handleBannerAction = useCallback(
		(action: BannerAction) => {
			switch (action.action) {
				case BannerActionType.Link:
					if (action.arg) {
						UiServiceClient.openUrl({ value: action.arg }).catch(console.error)
					}
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
					// E3D: no account system
					break

				case BannerActionType.ShowApiSettings:
					if (action.arg) {
						// Pre-select the provider before navigating
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

	/**
	 * Dismissal handler - updates version tracking
	 */
	const handleBannerDismiss = useCallback((bannerId: string) => {
		// !! Do not continue use these version numbers or add new banners that don't have unique IDs. !!
		// Banner versions are **deprecated**. Going forward, we are tracking which banners have
		// been dismissed using the **banner ID**.
		if (bannerId.startsWith("info-banner")) {
			StateServiceClient.updateInfoBannerVersion({ value: 1 }).catch(console.error)
		} else if (bannerId.startsWith("new-model")) {
			StateServiceClient.updateModelBannerVersion({ value: 1 }).catch(console.error)
		} else if (bannerId.startsWith("cli-")) {
			StateServiceClient.updateCliBannerVersion({ value: 1 }).catch(console.error)
		} else {
			// Mark the banner as dismissed by its ID.
			StateServiceClient.dismissBanner({ value: bannerId }).catch(console.error)
		}
	}, [])

	/**
	 * Build array of active banners for carousel
	 * Combines hardcoded banners (bannerConfig) with dynamic banners from extension state
	 */
	const activeBanners = useMemo(() => {
		// Start with the hardcoded banners (bannerConfig)
		const hardcodedBanners = bannerConfig.map((banner) =>
			convertBannerData(banner, {
				onAction: handleBannerAction,
				onDismiss: handleBannerDismiss,
			}),
		)

		// Add banners from extension state (if any)
		const extensionStateBanners = (banners ?? []).map((banner) =>
			convertBannerData(banner, {
				onAction: handleBannerAction,
				onDismiss: handleBannerDismiss,
			}),
		)

		// Combine both sources: extension state banners first, then hardcoded banners
		return [...extensionStateBanners, ...hardcodedBanners]
	}, [bannerConfig, banners, handleBannerAction, handleBannerDismiss])

	return (
		<div className="flex flex-col flex-1 w-full h-full p-0 m-0">
			<WhatsNewModal
				onBannerAction={handleBannerAction}
				onClose={handleCloseWhatsNewModal}
				open={showWhatsNewModal}
				version={version}
				welcomeBanners={welcomeBanners}
			/>
			<div className="overflow-y-auto flex flex-col pb-2.5">
				<HomeHeader shouldShowQuickWins={shouldShowQuickWins} />
				{!showWhatsNewModal && (
					<div>
						{!shouldShowQuickWins && taskHistory.length > 0 && <HistoryPreview showHistoryView={showHistoryView} />}
					</div>
				)}
			</div>
			<SuggestedTasks shouldShowQuickWins={shouldShowQuickWins} />
		</div>
	)
}
