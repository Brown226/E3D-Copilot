import { ClineMessage } from "@shared/ExtensionMessage"
import { useCallback, useEffect, useMemo, useRef, useState } from "react"
import { useExtensionState } from "@/context/ExtensionStateContext"
import { ChatState } from "../types/chatTypes"

/**
 * Custom hook for managing chat state
 * Handles input values, selection states, and UI state
 *
 * E3D 修复：监听 isTaskRunning 状态，任务结束时自动重置 sendingDisabled/enableButtons
 */
export function useChatState(messages: ClineMessage[]): ChatState {
	const { isTaskRunning } = useExtensionState()
	// Input and selection state
	const [inputValue, setInputValue] = useState("")
	const [activeQuote, setActiveQuote] = useState<string | null>(null)
	const [isTextAreaFocused, setIsTextAreaFocused] = useState(false)
	const [selectedImages, setSelectedImages] = useState<string[]>([])
	const [selectedFiles, setSelectedFiles] = useState<string[]>([])

	// UI state
	const [sendingDisabled, setSendingDisabled] = useState(false)
	const [enableButtons, setEnableButtons] = useState<boolean>(false)
	const [primaryButtonText, setPrimaryButtonText] = useState<string | undefined>("Approve")
	const [secondaryButtonText, setSecondaryButtonText] = useState<string | undefined>("Reject")
	const [expandedRows, setExpandedRows] = useState<Record<number, boolean>>({})

	// Refs
	const textAreaRef = useRef<HTMLTextAreaElement>(null)

	// Derived state
	const lastMessage = useMemo(() => messages.at(-1), [messages])
	const secondLastMessage = useMemo(() => messages.at(-2), [messages])
	const clineAsk = useMemo(() => (lastMessage?.type === "ask" ? lastMessage.ask : undefined), [lastMessage])

	// Clear expanded rows when task changes
	const task = useMemo(() => messages.at(0), [messages])
	const clearExpandedRows = useCallback(() => {
		setExpandedRows({})
	}, [])

	// Reset state when starting new conversation
	const resetState = useCallback(() => {
		setInputValue("")
		setActiveQuote(null)
		setSelectedImages([])
		setSelectedFiles([])
	}, [])

	// Handle focus change
	const handleFocusChange = useCallback((isFocused: boolean) => {
		setIsTextAreaFocused(isFocused)
	}, [])

	// Auto-expand last message row when task or messages first changed.
	useEffect(() => {
		clearExpandedRows()
	}, [clearExpandedRows])

	// 修复：任务结束（isTaskRunning: true → false）时自动重置 UI 状态
	// 解决"sendingDisabled 永不重置导致输入框永久禁用"问题
	useEffect(() => {
		if (isTaskRunning === false) {
			setSendingDisabled(false)
			setEnableButtons(true)
		}
	}, [isTaskRunning])

	return {
		// State values
		inputValue,
		setInputValue,
		activeQuote,
		setActiveQuote,
		isTextAreaFocused,
		setIsTextAreaFocused,
		selectedImages,
		setSelectedImages,
		selectedFiles,
		setSelectedFiles,
		sendingDisabled,
		setSendingDisabled,
		enableButtons,
		setEnableButtons,
		primaryButtonText,
		setPrimaryButtonText,
		secondaryButtonText,
		setSecondaryButtonText,
		expandedRows,
		setExpandedRows,

		// Refs
		textAreaRef,

		// Derived values
		lastMessage,
		secondLastMessage,
		clineAsk,
		task,

		// Handlers
		handleFocusChange,
		clearExpandedRows,
		resetState,
	}
}
