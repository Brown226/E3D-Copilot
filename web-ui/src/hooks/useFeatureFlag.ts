/**
 * Feature flag hook stub for E3D WebView2 environment.
 * PostHog is disabled, so all feature flags return false.
 */
export const useHasFeatureFlag = (_flagName: string): boolean => {
	return false
}
