/**
 * AskUserCard — AI 主动提问卡片
 * 当模型调用 ask_user 工具时显示，让用户回答问题或选择选项
 */

import { useState, useRef, useEffect } from 'react'
import { MessageCircleQuestion, Send } from 'lucide-react'
import type { PendingQuestion } from '@/store/useChatStore'

interface AskUserCardProps {
  question: PendingQuestion
  onAnswer: (questionId: string, answer: string) => void
}

export function AskUserCard({ question, onAnswer }: AskUserCardProps) {
  const [inputValue, setInputValue] = useState('')
  const [selectedOptions, setSelectedOptions] = useState<Set<number>>(new Set())
  const inputRef = useRef<HTMLTextAreaElement>(null)

  useEffect(() => {
    inputRef.current?.focus()
  }, [])

  const handleSubmit = () => {
    if (question.options && question.options.length > 0) {
      // 选项模式：提交选中的选项
      const selected = question.options.filter((_, i) => selectedOptions.has(i))
      if (selected.length > 0) {
        onAnswer(question.questionId, selected.join(', '))
      }
    } else {
      // 自由输入模式
      const trimmed = inputValue.trim()
      if (trimmed) {
        onAnswer(question.questionId, trimmed)
      }
    }
  }

  const handleOptionToggle = (index: number) => {
    setSelectedOptions((prev) => {
      const next = new Set(prev)
      if (question.multiSelect) {
        if (next.has(index)) next.delete(index)
        else next.add(index)
      } else {
        // 单选：切换选中
        if (next.has(index)) next.clear()
        else { next.clear(); next.add(index) }
      }
      return next
    })
  }

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      handleSubmit()
    }
  }

  const hasOptions = question.options && question.options.length > 0
  const canSubmit = hasOptions
    ? selectedOptions.size > 0
    : inputValue.trim().length > 0

  return (
    <div className="mx-1 my-2 rounded-xl border border-blue-200 dark:border-blue-700 bg-blue-50 dark:bg-blue-900/20 overflow-hidden shadow-sm">
      {/* 头部 */}
      <div className="flex items-center gap-2 px-3 py-2 border-b border-blue-200 dark:border-blue-800">
        <MessageCircleQuestion className="w-4 h-4 text-blue-600 dark:text-blue-400 shrink-0" />
        <span className="text-sm font-semibold text-blue-800 dark:text-blue-200">AI 向您提问</span>
      </div>

      {/* 问题内容 */}
      <div className="px-3 py-2 text-sm text-blue-900 dark:text-blue-100">
        {question.question}
      </div>

      {/* 选项或输入框 */}
      <div className="px-3 pb-3">
        {hasOptions ? (
          <div className="flex flex-wrap gap-1.5">
            {question.options!.map((opt, i) => (
              <button
                key={i}
                onClick={() => handleOptionToggle(i)}
                className={`px-3 py-1.5 rounded-lg text-xs font-medium border transition-all ${
                  selectedOptions.has(i)
                    ? 'border-blue-500 bg-blue-100 dark:bg-blue-800 text-blue-700 dark:text-blue-200 ring-1 ring-blue-400'
                    : 'border-blue-200 dark:border-blue-700 bg-white dark:bg-blue-900/40 text-blue-700 dark:text-blue-300 hover:bg-blue-50 dark:hover:bg-blue-800/50'
                }`}
              >
                {opt}
              </button>
            ))}
          </div>
        ) : (
          <div className="flex gap-2 items-end">
            <textarea
              ref={inputRef}
              value={inputValue}
              onChange={(e) => setInputValue(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder="输入您的回答…"
              rows={2}
              className="flex-1 px-3 py-2 text-sm rounded-lg border border-blue-200 dark:border-blue-700 bg-white dark:bg-blue-900/40 text-blue-900 dark:text-blue-100 placeholder-blue-400 dark:placeholder-blue-500 resize-none focus:outline-none focus:ring-1 focus:ring-blue-400"
            />
            <button
              onClick={handleSubmit}
              disabled={!canSubmit}
              className="h-9 px-3 rounded-lg bg-blue-600 text-white text-sm font-medium hover:bg-blue-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors flex items-center gap-1"
            >
              <Send className="w-3.5 h-3.5" />
              <span>发送</span>
            </button>
          </div>
        )}

        {/* 选项模式的发送按钮 */}
        {hasOptions && (
          <button
            onClick={handleSubmit}
            disabled={!canSubmit}
            className="mt-2 w-full h-9 rounded-lg bg-blue-600 text-white text-sm font-medium hover:bg-blue-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors flex items-center justify-center gap-1.5"
          >
            <Send className="w-3.5 h-3.5" />
            <span>确认回答</span>
          </button>
        )}
      </div>
    </div>
  )
}
