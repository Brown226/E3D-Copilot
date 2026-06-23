/**
 * PostHog provider stub for E3D WebView2 environment.
 * PostHog analytics are disabled in the E3D standalone build.
 */
import { type ReactNode } from "react"

export function CustomPostHogProvider({ children }: { children: ReactNode }) {
	return <>{children}</>
}
