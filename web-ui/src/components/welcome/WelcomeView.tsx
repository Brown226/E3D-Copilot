import { BooleanRequest } from "@shared/proto/cline/common"
import { VSCodeButton } from "@/components/ui/vscode-compat"
import { memo, useState } from "react"
import { useTranslation } from "react-i18next"
import ClineLogoWhite from "@/assets/ClineLogoWhite"
import { StateServiceClient } from "@/services/grpc-client"

const WelcomeView = memo(() => {
	const { t } = useTranslation("common")
	const [isLoading, setIsLoading] = useState(false)

	const handleSubmit = async () => {
		setIsLoading(true)
		try {
			await StateServiceClient.setWelcomeViewCompleted(BooleanRequest.create({ value: true }))
		} catch (error) {
			console.error("Failed to complete welcome view:", error)
		} finally {
			setIsLoading(false)
		}
	}

	return (
		<div className="fixed inset-0 p-0 flex flex-col">
			<div className="h-full px-5 overflow-auto flex flex-col gap-2.5 items-center justify-center text-center">
				<div className="flex justify-center mb-4">
					<ClineLogoWhite className="size-16" />
				</div>
				<h2 className="text-lg font-semibold">E小智 — AVEVA E3D AI 编程助手</h2>
				<p className="text-(--vscode-descriptionForeground) max-w-md">
					在设置中配置 LLM Provider 后即可开始使用。支持 OpenAI 兼容 API 和 Anthropic 协议。
				</p>

				<VSCodeButton appearance="primary" className="mt-4" disabled={isLoading} onClick={handleSubmit}>
					{isLoading ? "请稍候..." : "开始使用"}
				</VSCodeButton>
			</div>
		</div>
	)
})

export default WelcomeView
