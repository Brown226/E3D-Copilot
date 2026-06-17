export interface BrowserSettings {
  viewport: 'desktop' | 'tablet' | 'mobile'
  width: number
  height: number
}

export const BROWSER_VIEWPORT_PRESETS = {
  desktop: { width: 1280, height: 800 },
  tablet: { width: 768, height: 1024 },
  mobile: { width: 375, height: 667 },
}

export const DEFAULT_BROWSER_SETTINGS: BrowserSettings = {
  viewport: 'desktop',
  width: 1280,
  height: 800,
}
