import '@testing-library/jest-dom/vitest'

// 确保 react 加载 development 构建（act 支持需要）。
// 系统环境 NODE_ENV=production 会导致 react 走 production.min.js，
// 此处覆盖为 'test'，仅在测试 worker 中生效，不影响生产构建。
process.env.NODE_ENV = 'test'

// Mock localStorage
const localStorageMock = (() => {
  let store: Record<string, string> = {}
  return {
    getItem: (key: string) => store[key] ?? null,
    setItem: (key: string, value: string) => { store[key] = value },
    removeItem: (key: string) => { delete store[key] },
    clear: () => { store = {} },
    get length() { return Object.keys(store).length },
    key: (index: number) => Object.keys(store)[index] ?? null,
  }
})()

Object.defineProperty(window, 'localStorage', { value: localStorageMock })

// Mock matchMedia
Object.defineProperty(window, 'matchMedia', {
  value: (query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  }),
})

// Mock chrome.webview (E3D environment)
Object.defineProperty(window, 'chrome', {
  value: {
    webview: {
      postMessage: () => {},
      addEventListener: () => {},
      removeEventListener: () => {},
    },
  },
  writable: true,
})
