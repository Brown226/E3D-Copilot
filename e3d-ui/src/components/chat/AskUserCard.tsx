/**
 * AskUserCard — AI 主动提问卡片（对齐 Reasonix AskCard）
 *
 * 支持两种模式：
 * 1. 旧版单问题：questionId + question + options → 直接渲染
 * 2. 新版多问题：askData.questions[] → Tab 切换多问题
 *
 * 对齐 Reasonix：每个选项有 label + description，多选支持
 */

import { useState, useRef, useEffect, useCallback } from 'react'
import { MessageCircleQuestion, Send } from 'lucide-react'
import type { PendingQuestion } from '@/store/useChatStore'

interface AskUserCardProps {
  question: PendingQuestion
  onAnswer: (questionId: string, answer: string) => void
  /** 新版回答回调：发送结构化 AnswerQuestion */
  onAskAnswer?: (askId: string, answers: Array<{ questionId: string; selected: string[] }>) => void
}

interface QuestionAnswerState {
  questionId: string
  selected: string[]
  /** 自由文本输入（无选项时） */
  freeText: string
}

export function AskUserCard({ question, onAnswer, onAskAnswer }: AskUserCardProps) {
  const hasMultiQuestions = question.askData && question.askData.questions.length > 1
  const questions = question.askData?.questions || []
  const [activeTabIdx, setActiveTabIdx] = useState(0)
  const [answers, setAnswers] = useState<QuestionAnswerState[]>(() =>
    questions.map(q => ({ questionId: q.id, selected: [], freeText: '' }))
  )
  const [singleSelected, setSingleSelected] = useState<Set<number>>(new Set())
  const [singleFreeText, setSingleFreeText] = useState('')
  const inputRef = useRef<HTMLTextAreaElement>(null)

  useEffect(() => {
    inputRef.current?.focus()
  }, [activeTabIdx])

  // ── 单问题模式（旧版兼容）──
  const singleOpts = question.options || []
  const hasOptions = singleOpts.length > 0
  const canSubmitSingle = hasOptions
    ? singleSelected.size > 0
    : singleFreeText.trim().length > 0

  const handleSingleOptionToggle = (index: number) => {
    setSingleSelected((prev) => {
      const next = new Set(prev)
      if (question.multiSelect) {
        if (next.has(index)) next.delete(index)
        else next.add(index)
      } else {
        if (next.has(index)) next.clear()
        else { next.clear(); next.add(index) }
      }
      return next
    })
  }

  const handleSingleSubmit = () => {
    if (hasOptions) {
      const selected = question.options!.filter((_, i) => singleSelected.has(i))
      onAnswer(question.questionId, selected.join(', '))
    } else {
      onAnswer(question.questionId, singleFreeText.trim())
    }
  }

  // ── 多问题模式（新版）──
  const activeQuestion = questions[activeTabIdx]
  const activeAnswer = answers[activeTabIdx]

  const handleMultiOptionToggle = useCallback((optLabel: string) => {
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

  const handleMultiFreeText = useCallback((text: string) => {
    setAnswers(prev => prev.map((a, i) =>
      i === activeTabIdx ? { ...a, freeText: text } : a
    ))
  }, [activeTabIdx])

  const canSubmitMulti = () => {
    const a = answers[activeTabIdx]
    if (!a) return false
    const q = questions[activeTabIdx]
    if (q?.options && q.options.length > 0) {
      return a.selected.length > 0
    }
    return a.freeText.trim().length > 0
  }

  const isLastQuestion = activeTabIdx === questions.length - 1

  const handleMultiSubmit = () => {
    if (!isLastQuestion) {
      // 下一题
      setActiveTabIdx(prev => prev + 1)
      return
    }
    // 最后一题：提交所有回答
    if (onAskAnswer) {
      const allAnswers = answers
        .filter(a => a.selected.length > 0 || a.freeText.trim().length > 0)
        .map(a => ({
          questionId: a.questionId,
          selected: a.selected.length > 0 ? a.selected : [a.freeText.trim()],
        }))
      onAskAnswer(question.askData!.askId, allAnswers)
    }
  }

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      if (hasMultiQuestions) handleMultiSubmit()
      else handleSingleSubmit()
    }
  }

  // ── 渲染 ──
  const renderQuestion = (q: typeof activeQuestion, a: typeof activeAnswer) => {
    const opts = q?.options || []
    if (opts.length > 0) {
      return (
        <div className="flex flex-wrap gap-1.5">
          {opts.map((opt, i) => {
            const isSelected = a ? a.selected.includes(opt.label) : false
            return (
              <button
                key={i}
                onClick={() => handleMultiOptionToggle(opt.label)}
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
    return (
      <div className="flex gap-2 items-end">
        <textarea
          ref={inputRef}
          value={a?.freeText || ''}
          onChange={(e) => handleMultiFreeText(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="输入您的回答…"
          rows={2}
          className="flex-1 px-3 py-2 text-sm rounded-lg border border-blue-200 dark:border-blue-700 bg-white dark:bg-blue-900/40 text-blue-900 dark:text-blue-100 placeholder-blue-400 dark:placeholder-blue-500 resize-none focus:outline-none focus:ring-1 focus:ring-blue-400"
        />
      </div>
    )
  }

  // ── 多问题 Tab ──
  if (hasMultiQuestions) {
    return (
      <div className="mx-1 my-2 rounded-xl border border-blue-200 dark:border-blue-700 bg-blue-50 dark:bg-blue-900/20 overflow-hidden shadow-sm">
        {/* 头部 */}
        <div className="flex items-center gap-2 px-3 py-2 border-b border-blue-200 dark:border-blue-800">
          <MessageCircleQuestion className="w-4 h-4 text-blue-600 dark:text-blue-400 shrink-0" />
          <span className="text-sm font-semibold text-blue-800 dark:text-blue-200">AI 向您提问</span>
        </div>

        {/* Tab 切换 */}
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

        {/* 当前问题 */}
        <div className="px-3 py-2 text-sm text-blue-900 dark:text-blue-100">
          {activeQuestion?.prompt}
        </div>

        {/* 选项/输入 */}
        <div className="px-3 pb-3 space-y-2">
          {renderQuestion(activeQuestion, activeAnswer)}

          {/* 导航按钮 */}
          <div className="flex justify-between items-center gap-2">
            <span className="text-xs text-blue-400">
              {activeTabIdx + 1} / {questions.length}
            </span>
            <button
              onClick={handleMultiSubmit}
              disabled={!canSubmitMulti()}
              className="h-9 px-4 rounded-lg bg-blue-600 text-white text-sm font-medium hover:bg-blue-700 disabled:opacity-40 disabled:cursor-not-allowed transition-colors flex items-center gap-1"
            >
              <Send className="w-3.5 h-3.5" />
              <span>{isLastQuestion ? '确认回答' : '下一题'}</span>
            </button>
          </div>
        </div>
      </div>
    )
  }

  // ── 单问题模式（旧版兼容）──
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
                onClick={() => handleSingleOptionToggle(i)}
                className={`px-3 py-1.5 rounded-lg text-xs font-medium border transition-all ${
                  singleSelected.has(i)
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
              value={singleFreeText}
              onChange={(e) => setSingleFreeText(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder="输入您的回答…"
              rows={2}
              className="flex-1 px-3 py-2 text-sm rounded-lg border border-blue-200 dark:border-blue-700 bg-white dark:bg-blue-900/40 text-blue-900 dark:text-blue-100 placeholder-blue-400 dark:placeholder-blue-500 resize-none focus:outline-none focus:ring-1 focus:ring-blue-400"
            />
            <button
              onClick={handleSingleSubmit}
              disabled={!canSubmitSingle}
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
            onClick={handleSingleSubmit}
            disabled={!canSubmitSingle}
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
