import { StrictMode } from "react"
import { createRoot } from "react-dom/client"
import "./i18n/config"
import "./main.css"
import "./index.css"
import App from "./App.tsx"

createRoot(document.getElementById("root")!).render(
	<StrictMode>
		<App />
	</StrictMode>,
)

// React 挂载后隐藏加载提示
const loadingHint = document.getElementById("loading-hint")
if (loadingHint) { loadingHint.classList.add("loaded") }