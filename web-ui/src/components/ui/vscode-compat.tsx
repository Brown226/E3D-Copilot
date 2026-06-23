/**
 * VSCode Webview UI Toolkit 兼容? *
 * 目标：在 E3D WebView2 环境中复?VS Code 原生组件?API 与视觉效果，
 * 同时彻底摆脱?@/components/ui/vscode-compat 的依赖? *
 * 所有从 @/components/ui/vscode-compat 导入的组件都应改为从本文件导入? */
import * as React from "react"

import { Badge, type BadgeProps } from "@/components/ui/badge"
import { Button, type ButtonProps } from "@/components/ui/button"
import { Checkbox } from "@/components/ui/checkbox"
import { DataGrid, DataGridCell, DataGridRow } from "@/components/ui/data-grid"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Link, type LinkProps } from "@/components/ui/link"
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group"
import {
	Select,
	SelectContent,
	SelectItem,
	SelectTrigger,
	SelectValue,
	type SelectProps,
} from "@/components/ui/select"
import { Separator } from "@/components/ui/separator"
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs"
import { Textarea } from "@/components/ui/textarea"

// ---------------------------------------------------------------------------
// VSCodeButton
// ---------------------------------------------------------------------------

type VSCodeButtonAppearance = "primary" | "secondary" | "icon" | "danger" | undefined

interface VSCodeButtonProps extends Omit<ButtonProps, "variant"> {
	appearance?: VSCodeButtonAppearance
}

const VSCodeButton = React.forwardRef<HTMLButtonElement, VSCodeButtonProps>(
	({ appearance, variant, ...props }, ref) => {
		let mappedVariant: ButtonProps["variant"] = variant
		if (!mappedVariant) {
			switch (appearance) {
				case "primary":
					mappedVariant = "default"
					break
				case "secondary":
					mappedVariant = "secondary"
					break
				case "icon":
					mappedVariant = "icon"
					break
				case "danger":
					mappedVariant = "danger"
					break
				default:
					mappedVariant = "default"
					break
			}
		}
		return <Button ref={ref} variant={mappedVariant} {...props} />
	},
)
VSCodeButton.displayName = "VSCodeButton"

// ---------------------------------------------------------------------------
// VSCodeTextField
// ---------------------------------------------------------------------------

interface VSCodeTextFieldProps extends React.InputHTMLAttributes<HTMLInputElement> {
	children?: React.ReactNode
}

const VSCodeTextField = React.forwardRef<HTMLInputElement, VSCodeTextFieldProps>(
	({ children, className, ...props }, ref) => {
		// children 既可能是 label（在 input 上方），也可能是 slot 按钮
		// 简单按元素类型分流：纯文本 span 视为 label，其他视为 slot
		const arr = React.Children.toArray(children)
		const labelChild = arr.find(
			(c) => React.isValidElement(c) && (c.type === "span" || (c.props as any)?.role === "label"),
		) as React.ReactElement | undefined
		const slotChild = arr.filter((c) => c !== labelChild)

		return (
			<div className="flex flex-col gap-1 w-full">
				{labelChild && <div className="text-sm font-medium text-foreground">{labelChild}</div>}
				<div className="relative w-full">
					<Input
						ref={ref}
						className={`${slotChild.length > 0 ? "pr-8" : ""} ${className ?? ""}`}
						{...props}
					/>
					{slotChild.length > 0 && (
						<div className="absolute right-1 top-1/2 -translate-y-1/2 flex items-center pointer-events-auto">
							{slotChild}
						</div>
					)}
				</div>
			</div>
		)
	},
)
VSCodeTextField.displayName = "VSCodeTextField"

// ---------------------------------------------------------------------------
// VSCodeTextArea
// ---------------------------------------------------------------------------

const VSCodeTextArea = React.forwardRef<HTMLTextAreaElement, React.TextareaHTMLAttributes<HTMLTextAreaElement>>(
	({ className, ...props }, ref) => {
		return <Textarea ref={ref} className={className} {...props} />
	},
)
VSCodeTextArea.displayName = "VSCodeTextArea"

// ---------------------------------------------------------------------------
// VSCodeCheckbox
// ---------------------------------------------------------------------------

interface VSCodeCheckboxProps extends Omit<React.ComponentPropsWithoutRef<typeof Checkbox>, "onCheckedChange"> {
	checked?: boolean
	onChange?: (e: React.ChangeEvent<HTMLInputElement>) => void
}

const VSCodeCheckbox = React.forwardRef<React.ElementRef<typeof Checkbox>, VSCodeCheckboxProps>(
	({ children, checked, onChange, ...props }, ref) => {
		return (
			<label className="flex items-center gap-2 cursor-pointer">
				<Checkbox ref={ref} checked={checked} onCheckedChange={(v) => onChange?.({ target: { checked: v === true } } as any)} {...props} />
				{children && <span className="text-sm">{children}</span>}
			</label>
		)
	},
)
VSCodeCheckbox.displayName = "VSCodeCheckbox"

// ---------------------------------------------------------------------------
// VSCodeRadio / VSCodeRadioGroup
// ---------------------------------------------------------------------------

interface VSCodeRadioGroupProps extends Omit<React.ComponentPropsWithoutRef<typeof RadioGroup>, "onValueChange"> {
	value?: string
	onChange?: (e: React.ChangeEvent<HTMLInputElement>) => void
}

const VSCodeRadioGroup = React.forwardRef<React.ElementRef<typeof RadioGroup>, VSCodeRadioGroupProps>(
	({ value, onChange, ...props }, ref) => {
		return <RadioGroup ref={ref} value={value} onValueChange={(v) => onChange?.({ target: { value: v } } as any)} {...props} />
	},
)
VSCodeRadioGroup.displayName = "VSCodeRadioGroup"

interface VSCodeRadioProps extends Omit<React.ComponentPropsWithoutRef<typeof RadioGroupItem>, "id"> {
	children?: React.ReactNode
}

const VSCodeRadio = React.forwardRef<React.ElementRef<typeof RadioGroupItem>, VSCodeRadioProps>(
	({ children, value, ...props }, ref) => {
		const id = React.useId()
		return (
			<div className="flex items-center gap-2">
				<RadioGroupItem ref={ref} id={id} value={value ?? ""} {...props} />
				{children && (
					<Label htmlFor={id} className="text-sm cursor-pointer">
						{children}
					</Label>
				)}
			</div>
		)
	},
)
VSCodeRadio.displayName = "VSCodeRadio"

// ---------------------------------------------------------------------------
// VSCodeDropdown / VSCodeOption
// ---------------------------------------------------------------------------

interface VSCodeDropdownProps extends Omit<SelectProps, "onValueChange"> {
	value?: string
	onChange?: (e: React.ChangeEvent<HTMLSelectElement>) => void
}

const VSCodeDropdown = React.forwardRef<React.ElementRef<typeof SelectTrigger>, VSCodeDropdownProps>(
	({ children, value, onChange, ...props }, ref) => {
		return (
			<Select value={value} onValueChange={(v) => onChange?.({ target: { value: v } } as any)} {...props}>
				<SelectTrigger ref={ref}>
					<SelectValue />
				</SelectTrigger>
				<SelectContent>{children}</SelectContent>
			</Select>
		)
	},
)
VSCodeDropdown.displayName = "VSCodeDropdown"

interface VSCodeOptionProps extends Omit<React.ComponentPropsWithoutRef<typeof SelectItem>, "value"> {
	value?: string
	selected?: boolean
}

const VSCodeOption = React.forwardRef<React.ElementRef<typeof SelectItem>, VSCodeOptionProps>(
	({ children, value, ...props }, ref) => {
		return (
			<SelectItem ref={ref} value={value ?? ""} {...props}>
				{children}
			</SelectItem>
		)
	},
)
VSCodeOption.displayName = "VSCodeOption"

// ---------------------------------------------------------------------------
// VSCodeLink
// ---------------------------------------------------------------------------

const VSCodeLink = React.forwardRef<HTMLAnchorElement, LinkProps>(({ className, ...props }, ref) => {
	return <Link ref={ref} className={className} {...props} />
})
VSCodeLink.displayName = "VSCodeLink"

// ---------------------------------------------------------------------------
// VSCodeProgressRing
// ---------------------------------------------------------------------------

const VSCodeProgressRing = React.forwardRef<HTMLDivElement, React.HTMLAttributes<HTMLDivElement>>(
	({ className, ...props }, ref) => {
		return (
			<div
				ref={ref}
				className={`inline-block h-4 w-4 animate-spin rounded-full border-2 border-solid border-current border-r-transparent ${className ?? ""}`}
				{...props}
			/>
		)
	},
)
VSCodeProgressRing.displayName = "VSCodeProgressRing"

// ---------------------------------------------------------------------------
// VSCodePanels / VSCodePanelTab / VSCodePanelView
// ---------------------------------------------------------------------------

interface VSCodePanelsProps extends Omit<React.ComponentPropsWithoutRef<typeof Tabs>, "defaultValue"> {
	defaultValue?: string
}

const VSCodePanels = React.forwardRef<React.ElementRef<typeof Tabs>, VSCodePanelsProps>(
	({ defaultValue, ...props }, ref) => {
		return <Tabs ref={ref} defaultValue={defaultValue ?? "tab-1"} {...props} />
	},
)
VSCodePanels.displayName = "VSCodePanels"

interface VSCodePanelTabProps extends Omit<React.ComponentPropsWithoutRef<typeof TabsTrigger>, "value"> {
	id?: string
}

const VSCodePanelTab = React.forwardRef<React.ElementRef<typeof TabsTrigger>, VSCodePanelTabProps>(
	({ id, children, ...props }, ref) => {
		return (
			<TabsTrigger ref={ref} value={id ?? ""} {...props}>
				{children}
			</TabsTrigger>
		)
	},
)
VSCodePanelTab.displayName = "VSCodePanelTab"

interface VSCodePanelViewProps extends Omit<React.ComponentPropsWithoutRef<typeof TabsContent>, "value"> {
	id?: string
}

const VSCodePanelView = React.forwardRef<React.ElementRef<typeof TabsContent>, VSCodePanelViewProps>(
	({ id, children, ...props }, ref) => {
		return (
			<TabsContent ref={ref} value={id ?? ""} {...props}>
				{children}
			</TabsContent>
		)
	},
)
VSCodePanelView.displayName = "VSCodePanelView"

// ---------------------------------------------------------------------------
// VSCodeBadge / VSCodeTag / VSCodeDivider
// ---------------------------------------------------------------------------

const VSCodeBadge = React.forwardRef<HTMLDivElement, BadgeProps>(({ className, ...props }, ref) => {
	return <Badge ref={ref} className={className} {...props} />
})
VSCodeBadge.displayName = "VSCodeBadge"

const VSCodeTag = React.forwardRef<HTMLDivElement, BadgeProps>(({ className, ...props }, ref) => {
	return <Badge ref={ref} variant="outline" className={className} {...props} />
})
VSCodeTag.displayName = "VSCodeTag"

const VSCodeDivider = React.forwardRef<HTMLDivElement, React.ComponentPropsWithoutRef<typeof Separator>>(
	({ className, ...props }, ref) => {
		return <Separator ref={ref} className={className} {...props} />
	},
)
VSCodeDivider.displayName = "VSCodeDivider"

// ---------------------------------------------------------------------------
// VSCodeDataGrid / VSCodeDataGridRow / VSCodeDataGridCell
// ---------------------------------------------------------------------------

const VSCodeDataGrid = React.forwardRef<HTMLTableElement, React.ComponentPropsWithoutRef<typeof DataGrid>>(
	({ className, ...props }, ref) => {
		return <DataGrid ref={ref} className={className} {...props} />
	},
)
VSCodeDataGrid.displayName = "VSCodeDataGrid"

const VSCodeDataGridRow = React.forwardRef<HTMLTableRowElement, React.ComponentPropsWithoutRef<typeof DataGridRow>>(
	({ className, ...props }, ref) => {
		return <DataGridRow ref={ref} className={className} {...props} />
	},
)
VSCodeDataGridRow.displayName = "VSCodeDataGridRow"

const VSCodeDataGridCell = React.forwardRef<HTMLTableCellElement, React.ComponentPropsWithoutRef<typeof DataGridCell>>(
	({ className, ...props }, ref) => {
		return <DataGridCell ref={ref} className={className} {...props} />
	},
)
VSCodeDataGridCell.displayName = "VSCodeDataGridCell"

export {
	VSCodeBadge,
	VSCodeButton,
	VSCodeCheckbox,
	VSCodeDataGrid,
	VSCodeDataGridCell,
	VSCodeDataGridRow,
	VSCodeDivider,
	VSCodeDropdown,
	VSCodeLink,
	VSCodeOption,
	VSCodePanelTab,
	VSCodePanels,
	VSCodePanelView,
	VSCodeProgressRing,
	VSCodeRadio,
	VSCodeRadioGroup,
	VSCodeTag,
	VSCodeTextArea,
	VSCodeTextField,
}
