import { useTranslation } from "react-i18next"

export interface QuickWinTask {
	id: string
	title: string
	description: string
	icon?: string
	actionCommand: string
	prompt: string
	buttonText?: string
}

const useQuickWinTasks = () => {
	const { t } = useTranslation("common")
	const quickWinTasks: QuickWinTask[] = [
		{
			id: "e3d_query_elements",
			title: t("suggestedTasks.queryElements"),
			description: t("suggestedTasks.queryElementsDesc"),
			icon: "SearchIcon",
			actionCommand: "e3d/queryElements",
			prompt: "查询当前项目中所有 DN200 的管道，并列出它们的名称、等级和位置。",
			buttonText: ">",
		},
		{
			id: "e3d_create_equipment",
			title: t("suggestedTasks.createEquipment"),
			description: t("suggestedTasks.createEquipmentDesc"),
			icon: "BoxIcon",
			actionCommand: "e3d/createEquipment",
			prompt: "在坐标 (1000, 2000, 3000) 处创建一个泵设备，命名为 PUMP-001，并设置基本属性。",
			buttonText: ">",
		},
		{
			id: "e3d_check_collision",
			title: t("suggestedTasks.checkCollision"),
			description: t("suggestedTasks.checkCollisionDesc"),
			icon: "AlertIcon",
			actionCommand: "e3d/checkCollision",
			prompt: "检查当前区域是否存在管道与设备之间的碰撞，并输出碰撞列表。",
			buttonText: ">",
		},
		{
			id: "e3d_pml_command",
			title: t("suggestedTasks.runPmlCommand"),
			description: t("suggestedTasks.runPmlCommandDesc"),
			icon: "CodeIcon",
			actionCommand: "e3d/runPmlCommand",
			prompt: "执行 PML 命令：show !!ce 并解释当前选中元素的属性。",
			buttonText: ">",
		},
	]
	return quickWinTasks
}

// For backward compatibility, keep the static export (translations won't work here but this maintains the interface)
export const quickWinTasks: QuickWinTask[] = [
	{
		id: "e3d_query_elements",
		title: "查询元件",
		description: "按条件查询 E3D 模型中的管道、设备或结构",
		icon: "SearchIcon",
		actionCommand: "e3d/queryElements",
		prompt: "查询当前项目中所有 DN200 的管道，并列出它们的名称、等级和位置。",
		buttonText: ">",
	},
	{
		id: "e3d_create_equipment",
		title: "创建设备",
		description: "通过自然语言在 E3D 中创建基础设备或结构",
		icon: "BoxIcon",
		actionCommand: "e3d/createEquipment",
		prompt: "在坐标 (1000, 2000, 3000) 处创建一个泵设备，命名为 PUMP-001，并设置基本属性。",
		buttonText: ">",
	},
	{
		id: "e3d_check_collision",
		title: "碰撞检查",
		description: "检查指定区域内的模型碰撞情况",
		icon: "AlertIcon",
		actionCommand: "e3d/checkCollision",
		prompt: "检查当前区域是否存在管道与设备之间的碰撞，并输出碰撞列表。",
		buttonText: ">",
	},
	{
		id: "e3d_pml_command",
		title: "执行 PML",
		description: "让 AI 执行 PML 命令并解释结果",
		icon: "CodeIcon",
		actionCommand: "e3d/runPmlCommand",
		prompt: "执行 PML 命令：show !!ce 并解释当前选中元素的属性。",
		buttonText: ">",
	},
]

export { useQuickWinTasks }
