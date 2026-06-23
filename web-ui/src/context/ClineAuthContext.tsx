import type { UserOrganization } from "@shared/proto/cline/account"
import type React from "react"
import { createContext, useContext } from "react"

export interface ClineUser {
	uid: string
	email?: string
	displayName?: string
	photoUrl?: string
	appBaseUrl?: string
}

export interface ClineAuthContextType {
	clineUser: ClineUser | null
	organizations: UserOrganization[] | null
	activeOrganization: UserOrganization | null
}

export const ClineAuthContext = createContext<ClineAuthContextType | undefined>(undefined)

// E3D: no Cline auth backend — always return null
export const ClineAuthProvider: React.FC<{ children: React.ReactNode }> = ({ children }) => {
	return (
		<ClineAuthContext.Provider
			value={{ clineUser: null, organizations: null, activeOrganization: null }}>
			{children}
		</ClineAuthContext.Provider>
	)
}

export const useClineAuth = () => {
	const context = useContext(ClineAuthContext)
	if (context === undefined) {
		return { clineUser: null, organizations: null, activeOrganization: null } as ClineAuthContextType
	}
	return context
}

export const useClineSignIn = () => {
	return { isLoginLoading: false, handleSignIn: () => {} }
}

export const handleSignOut = async () => {}
