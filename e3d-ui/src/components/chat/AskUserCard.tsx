/**
 * AskUserCard — AI 主动提问卡片（对齐 Reasonix AskCard）
 *
 * 统一使用 askData.questions[] 结构，通过 onAskAnswer 回调提交回答
 */

import { useState, useRef, useEffect, useCallback } from 'react'
import { MessageCircleQuestion, Send } from 'lucide-react'
import type { PendingQuestion } from '@/store/useChatStore'

interface AskUserCardProps {
  question: PendingQuestion
  onAskAnswer: (askId: string, answers: Array<{ questionId: string; selected: string[] }>) => void
}

interface QuestionAnswerState {
  questionId: string
  selected: string[]
  freeText: string
}

export function AskUserCard({ question, onAskAnswer }: AskUserCardProps) {
  const questions = question.askData?.questions || []
  const [activeTabIdx, setActiveTabIdx] = useState(0)
  const [answers, setAnswers] = useState<QuestionAnswerState[]>(() =>
    questions.map(q => ({ questionId: q.id, selected: [], freeText: '' }))
  )
  const inputRef = useRef<HTMLTextAreaElement>(null)

  useEffect(() => {
    inputRef.current?.focus()
  }, [activeTabIdx])

  const activeQuestion = questions[activeTabIdx]
  const activeAnswer = answers[activeTabIdx]

  const handleOptionToggle = useCallback((optLabel: string) => {
    setAnswers(prev => prev.map((a, i) => {
      if (i !== activeTabIdx) return a
      const next = new Set(a.selected)
      const q = questions[activeTabIdx]
      if (q?.multi) {
        if (next.has(optLabel)) next.delete(optLabel)
        else next.add(optLabel)
      } else {
        if (next.has(optLabel)) next.clear()
        else { next.clear(); next.add(optLabel) }
      }
      return { ...a, selected: Array.from(next) }
    }))
  }, [activeTabIdx, questions])

  const handleFreeText = useCallback((text: string) => {
    setAnswers(prev => prev.map((a, i) =>
      i === activeTabIdx ? { ...a, freeText: text } : a
    ))
  }, [activeTabIdx])

  const canSubmit = () => {
    const a = answers[activeTabIdx]
    if (!a) return false
    const q = questions[activeTabIdx]
    if (q?.options && q.options.length > 0) {
      return a.selected.length > 0
    }
    return a.freeText.trim().length > 0
  }

  const isLastQuestion = activeTabIdx === questions.length - 1

  const handleSubmit = () => {
    if (!isLastQuestion) {
      setActiveTabIdx(prev => prev + 1)
      return
    }
    const allAnswers = answers
      .filter(a => a.selected.length > 0 || a.freeText.trim().length > 0)
      .map(a => ({
        questionId: a.questionId,
        selected: a.selected.length > 0 ? a.selected : [a.freeText.trim()],
      }))
    onAskAnswer(question.askData!.askId, allAnswers)
  }

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      handleSubmit()
    }
  }

  const renderOptions = () => {
    const opts = activeQuestion?.options || []
    if (opts.length === 0) {
      return (
        <div className="flex gap-2 items-end">
          <textarea
            ref={inputRef}
            value={activeAnswer?.freeText || ''}
            onChange={(e) => handleFreeText(e.target.value)}
            onKeyDown={handleKeyDown}
            placeholder="输入您的回答…"
            rows={2}
            className="flex-1 px-3 py-2 text-sm rounded-lg border border-blue-200 dark:border-blue-700 bg-white dark:bg-blue-900/40 text-blue-900 dark:text-blue-100 placeholder-blue-400 dark:placeholder-blue-500 resize-none focus:outline-none focus:ring-1 focus:ring-blue-400"
          />
        </div>
      )
    }
    return (
      <div className="flex flex-wrap gap-1.5">
        {opts.map((opt, i) => {
          const isSelected = activeAnswer ? activeAnswer.selected.includes(opt.label) : false
          return (
            <button
              key={i}
              onClick={() => handleOptionToggle(opt.label)}
              className={`px-3 py-1.5 rounded-lg text-xs font-medium border transition-all text-left ${
                isSelected
                  ? 'border-blue-500 bg-blue-100 dark:bg-blue-800 text-blue-700 dark:text-blue-200 ring-1 ring-blue-400'
                  : 'border-blue-200 dark:border-blue-700 bg-white dark:bg-blue-900/40 text-blue-700 dark:text-blue-300 hover:bg-blue-50 dark:hover:bg-blue-800/50'
              }`}
            >
              <div>{opt.label}</div>
              {opt.description && (
                <div className="text-[10px] text-blue-500/70 dark:text-blue-400/60 mt-0.5">{opt.description}</div>
              )}
            </button>
          )
        })}
      </div>
    )
  }

  const hasMultiTabs = questions.length > 1

  return (
    <div className="mx-1 my-2 rounded-xl border border-blue-200 dark:border-blue-700 bg-blue-50 dark:bg-blue-900/20 overflow-hidden shadow-sm">
      <div className="flex items-center gap-2 px-3 py-2 border-b border-blue-200 dark:border-blue-800">
        <MessageCircleQuestion className="w-4 h-4 text-blue-600 dark:text-blue-400 shrink-0" />
        <span className="text-sm font-semibold text-blue-800 dark:text-blue-200">AI 向您提问</span>
      </div>

      {hasMultiTabs && (
        <div className="flex border-b border-blue-200 dark:border-blue-800">
          {questions.map((q, i) => (
            <button
              key={q.id}
              onClick={() => setActiveTabIdx(i)}
              className={`px-3 py-1.5 text-xs font-medium transition-colors border-b-2 -mb-[1px] ${
                i === activeTabIdx
                  ? 'border-blue-600 text-blue-700 dark:text-blue-200 bg-blue-100/50 dark:bg-blue-800/30'
                  : 'border-transparent text-blue-400 dark:text-blue-500 hover:text-blue-600'
              }`}
            >
              {q.header || `问题 ${i + 1}`}
            </button>
          ))}
        </div>
      )}

      <div className="px-3 py-2 text-sm text-blue-900 dark:text-blue-100">
        {activeQuestion?.prompt}
      </div>

      <div className="px-3 pb-3 space-y-2">
        {renderOptions()}

        <div className={`flex ${hasMultiTabs ? 'justify-between' : 'justify-end'} items-center gap-2`}>
          {hasMultiTabs && (
            <span className="text-xs text-blue-400">
              {activeTabIdx + 1} / {questions.length}
            </span>
          )}
          <button
            onClick={handleSubmit}
            disabled={!canSubmit()}
            className="h-9 px-4 rounded-lg bg-blue-600 text-white text-sm font-medium hover:bg-blue-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors flex items-center gap-1"
          >
            <Send className="w-3.5 h-3.5" />
            <span>{hasMultiTabs && !isLastQuestion ? '下一题' : '确认回答'}</span>
          </button>
        </div>
      </div>
    </div>
  )
}
