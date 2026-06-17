// gRPC client stubs for browser dev mode (no WebView2 backend)
// All methods return safe empty values — never throw.

type Callbacks = {
	onResponse?: (...args: any[]) => void
	onError?: (error: any) => void
	onComplete?: () => void
}

/**
 * Creates a Proxy-based stub client.
 *
 * - Methods whose name starts with "subscribeTo" are treated as streaming
 *   subscriptions: they accept (request, callbacks) and return a no-op
 *   cleanup function `() => {}`. Callbacks are never invoked.
 * - All other methods are treated as unary RPCs: they return a Promise
 *   that resolves to an empty object `{}`.
 * - Static-like access works because the returned object is the client.
 */
function createClient(_name: string): Record<string, (...args: any[]) => any> {
	return new Proxy(
		{},
		{
			get(_target, prop: string | symbol) {
				if (typeof prop !== "string") {
					return undefined
				}

				// Streaming subscription  →  return cleanup function
				if (prop.startsWith("subscribeTo")) {
					return (_request?: any, _callbacks?: Callbacks) => {
						// Return a no-op cleanup function
						return () => {}
					}
				}

				// Unary RPC  →  return resolved promise
				return async (..._args: any[]) => ({})
			},
		},
	)
}

export const StateServiceClient = createClient("StateService")
export const UiServiceClient = createClient("UiService")
export const FileServiceClient = createClient("FileService")
export const McpServiceClient = createClient("McpService")
export const ModelsServiceClient = createClient("ModelsService")
export const AccountServiceClient = createClient("AccountService")
export const TaskServiceClient = createClient("TaskService")
export const CheckpointsServiceClient = createClient("CheckpointsService")
export const WorktreeServiceClient = createClient("WorktreeService")
export const SlashServiceClient = createClient("SlashService")
export const BrowserServiceClient = createClient("BrowserService")
export const WebServiceClient = createClient("WebService")
export const OcaAccountServiceClient = createClient("OcaAccountService")
