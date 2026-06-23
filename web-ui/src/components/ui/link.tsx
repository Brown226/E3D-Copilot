import * as React from "react"

import { cn } from "@/lib/utils"

export interface LinkProps extends React.AnchorHTMLAttributes<HTMLAnchorElement> {
	children?: React.ReactNode
}

const Link = React.forwardRef<HTMLAnchorElement, LinkProps>(({ className, ...props }, ref) => {
	return (
		<a
			ref={ref}
			className={cn("text-link hover:text-link-hover hover:underline cursor-pointer", className)}
			{...props}
		/>
	)
})
Link.displayName = "Link"

export { Link }
