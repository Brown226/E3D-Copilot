import * as React from "react"

import { cn } from "@/lib/utils"

interface DataGridProps extends React.TableHTMLAttributes<HTMLTableElement> {
	children?: React.ReactNode
}

const DataGrid = React.forwardRef<HTMLTableElement, DataGridProps>(({ className, ...props }, ref) => (
	<table ref={ref} className={cn("w-full border-collapse text-sm", className)} {...props} />
))
DataGrid.displayName = "DataGrid"

interface DataGridRowProps extends React.HTMLAttributes<HTMLTableRowElement> {
	children?: React.ReactNode
	"row-type"?: "header" | "default"
}

const DataGridRow = React.forwardRef<HTMLTableRowElement, DataGridRowProps>(
	({ className, "row-type": rowType, ...props }, ref) => (
		<tr
			ref={ref}
			className={cn(
				"border-b border-editor-group-border",
				rowType === "header" && "bg-button-background/10 font-medium",
				className,
			)}
			{...props}
		/>
	),
)
DataGridRow.displayName = "DataGridRow"

interface DataGridCellProps extends React.TdHTMLAttributes<HTMLTableCellElement> {
	children?: React.ReactNode
	"cell-type"?: "columnheader" | "default"
	"grid-column"?: string | number
}

const DataGridCell = React.forwardRef<HTMLTableCellElement, DataGridCellProps>(
	({ className, "cell-type": cellType, children, ...props }, ref) => {
		const Comp = cellType === "columnheader" ? "th" : "td"
		return (
			<Comp
				ref={ref}
				className={cn(
					"px-2 py-1.5 text-left text-sm",
					cellType === "columnheader" && "text-description font-medium",
					className,
				)}
				{...props}>
				{children}
			</Comp>
		)
	},
)
DataGridCell.displayName = "DataGridCell"

export { DataGrid, DataGridRow, DataGridCell }
