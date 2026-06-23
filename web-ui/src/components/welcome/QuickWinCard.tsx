import React from "react"
import { QuickWinTask } from "./quickWinTasks"

interface QuickWinCardProps {
	task: QuickWinTask
	onExecute: () => void
}

const renderIcon = (iconName?: string) => {
	if (!iconName) {
		return <span className="codicon codicon-rocket text-[28px]! leading-none!" />
	}

	let iconClass = "codicon-rocket"
	switch (iconName) {
		case "WebAppIcon":
			iconClass = "codicon-dashboard"
			break
		case "TerminalIcon":
			iconClass = "codicon-terminal"
			break
		case "GameIcon":
			iconClass = "codicon-game"
			break
		case "SearchIcon":
			iconClass = "codicon-search"
			break
		case "BoxIcon":
			iconClass = "codicon-package"
			break
		case "AlertIcon":
			iconClass = "codicon-warning"
			break
		case "CodeIcon":
			iconClass = "codicon-code"
			break
		default:
			break
	}
	return <span className={`codicon ${iconClass} text-[28px]! leading-none!`} />
}

const QuickWinCard: React.FC<QuickWinCardProps> = ({ task, onExecute }) => {
	return (
		<div
			className="flex items-center py-2.5 px-3 space-x-3 rounded-lg cursor-pointer group transition-colors duration-150 ease-in-out bg-muted/40 border border-border hover:bg-list-hover hover:border-list-hover"
			onClick={() => onExecute()}>
			<div className="shrink-0 flex items-center justify-center w-8 h-8 text-icon-foreground">
				{renderIcon(task.icon)}
			</div>

			<div className="grow min-w-0">
				<h3 className="text-sm font-medium truncate text-foreground leading-tight mb-0 mt-0">
					{task.title}
				</h3>
				<p className="text-xs truncate text-description leading-tight mt-px">{task.description}</p>
			</div>
		</div>
	)
}

export default QuickWinCard
