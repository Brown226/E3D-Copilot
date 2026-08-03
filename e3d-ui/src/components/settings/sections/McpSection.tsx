/**
 * McpSection — MCP 服务器管理
 *
 * 功能：
 * 1. 查询所有 MCP 服务器状态（连接/工具数/传输方式）
 * 2. 展示失败记录
 * 3. 重启单个 MCP 服务器
 * 4. 诊断单个 MCP 服务器（健康检查详情）
 *
 * 对应后端：Bridge.Mcp.cs (HandleMcpStatus / HandleMcpRestart / HandleMcpDiagnose)
 */

import { useState, useMemo, useCallback, useEffect } from 'react'
import {
  RefreshCw,
  Server,
  Plug,
  PlugZap,
  Wrench,
  AlertCircle,
  CheckCircle2,
  XCircle,
  Loader2,
  ChevronRight,
  Activity,
  RotateCw,
  Stethoscope,
  Plus,
  Trash2,
  Sparkles,
} from 'lucide-react'
import type {
  McpServerInfo,
  McpFailure,
  McpDiagnoseResultPayload,
  McpServerConfig,
} from '@/services/messageContracts'
import { useToastStore } from '@/store/useToastStore'
import { useChatStore } from '@/store/useChatStore'

// ── Helpers ──

function transportLabel(t: string): string {
  const labels: Record<string, string> = {
    stdio: 'STDIO',
    http: 'HTTP',
    sse: 'SSE',
  }
  return labels[t?.toLowerCase()] || t?.toUpperCase() || '未知'
}

function transportColor(t: string): string {
  switch (t?.toLowerCase()) {
    case 'stdio': return 'bg-blue-100 text-blue-700 dark:bg-blue-900/30 dark:text-blue-400'
    case 'http': return 'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400'
    case 'sse': return 'bg-purple-100 text-purple-700 dark:bg-purple-900/30 dark:text-purple-400'
    default: return 'bg-slate-100 text-slate-700 dark:bg-slate-700 dark:text-slate-300'
  }
}

// ═══════════════════════════════════════════
// 主组件
// ═══════════════════════════════════════════

export default function McpSection() {
  const [servers, setServers] = useState<McpServerInfo[]>([])
  const [failures, setFailures] = useState<McpFailure[]>([])
  const [totalTools, setTotalTools] = useState(0)
  const [message, setMessage] = useState<string>('')
  const [loading, setLoading] = useState(false)
  const [expandedServers, setExpandedServers] = useState<Set<string>>(new Set())
  const [restarting, setRestarting] = useState<string | null>(null)
  const [diagnosing, setDiagnosing] = useState<string | null>(null)
  const [diagnoseResult, setDiagnoseResult] = useState<Record<string, McpDiagnoseResultPayload>>({})
  const [showFailures, setShowFailures] = useState(false)
  const [showAddForm, setShowAddForm] = useState(false)
  const [adding, setAdding] = useState(false)
  const [removing, setRemoving] = useState<string | null>(null)
  // 表单使用 argsText（字符串）而非 args（string[]），提交时转换
  const [formData, setFormData] = useState<{
    name: string
    type: McpServerConfig['type']
    command?: string
    argsText?: string
    url?: string
    dir?: string
  }>({
    name: '',
    type: 'stdio',
    command: '',
    argsText: '',
    url: '',
    dir: '',
  })
  const addToast = useToastStore((s) => s.addToast)
  const sendMessage = useChatStore((s) => s.sendMessage)
  const toggleSettings = useChatStore((s) => s.toggleSettings)

  // ── 通过对话添加 MCP（触发 mcp-manager 元技能） ──
  const handleAddViaChat = useCallback(async () => {
    // 先关闭设置面板，回到对话界面
    toggleSettings()
    try {
      const { default: bridge } = await import('@/services/bridgeService')
      if (!bridge.isAvailable()) {
        addToast('warning', '未连接到后端，无法添加 MCP')
        return
      }
      // 发送一条明确的消息，引导 LLM 调用 run_skill("mcp-manager")
      sendMessage(
        bridge.sendUserMessage.bind(bridge),
        undefined,
        '我想添加一个 MCP 服务器。请先使用 run_skill 工具加载 "mcp-manager" 元技能，然后按照它的指引引导我完成 MCP 服务器配置。'
      )
    } catch {
      addToast('error', '触发 MCP 添加失败')
    }
  }, [sendMessage, toggleSettings, addToast])

  // ── 加载 MCP 状态 ──
  const loadStatus = useCallback(async () => {
    setLoading(true)
    try {
      const { default: bridge } = await import('@/services/bridgeService')
      if (!bridge.isAvailable()) return
      const result = await bridge.getMcpStatus()
      if (result) {
        setServers(result.servers || [])
        setFailures(result.failures || [])
        setTotalTools(result.totalTools ?? 0)
        setMessage(result.message || '')
      }
    } catch {
      addToast('error', '获取 MCP 状态失败')
    } finally {
      setLoading(false)
    }
  }, [addToast])

  // ── 初始化加载 ──
  useEffect(() => {
    loadStatus()
  }, [loadStatus])

  // ── 统计 ──
  const stats = useMemo(() => ({
    total: servers.length,
    connected: servers.filter((s) => s.connected).length,
    tools: totalTools || servers.reduce((sum, s) => sum + s.toolCount, 0),
    failures: failures.length,
  }), [servers, failures, totalTools])

  // ── 切换展开 ──
  const toggleExpand = useCallback((name: string) => {
    setExpandedServers((prev) => {
      const next = new Set(prev)
      if (next.has(name)) next.delete(name)
      else next.add(name)
      return next
    })
  }, [])

  // ── 重启服务器 ──
  const handleRestart = useCallback(async (server: string) => {
    setRestarting(server)
    try {
      const { default: bridge } = await import('@/services/bridgeService')
      if (!bridge.isAvailable()) {
        addToast('warning', '未连接到后端')
        return
      }
      const result = await bridge.restartMcpServer(server)
      if (result?.success) {
        addToast('success', result.message || `MCP server '${server}' 已重启`)
        // 重启后刷新状态
        await loadStatus()
      } else {
        addToast('error', result?.message || `重启 '${server}' 失败`)
      }
    } catch {
      addToast('error', `重启 '${server}' 失败`)
    } finally {
      setRestarting(null)
    }
  }, [addToast, loadStatus])

  // ── 诊断服务器 ──
  const handleDiagnose = useCallback(async (server: string) => {
    setDiagnosing(server)
    try {
      const { default: bridge } = await import('@/services/bridgeService')
      if (!bridge.isAvailable()) {
        addToast('warning', '未连接到后端')
        return
      }
      const result = await bridge.diagnoseMcpServer(server)
      if (result) {
        setDiagnoseResult((prev) => ({ ...prev, [server]: result }))
        if (result.healthy) {
          addToast('success', `'${server}' 诊断通过：${result.summary}`)
        } else {
          addToast('warning', `'${server}' 诊断发现问题：${result.summary}`)
        }
      }
    } catch {
      addToast('error', `诊断 '${server}' 失败`)
    } finally {
      setDiagnosing(null)
    }
  }, [addToast])

  // ── 添加 MCP server ──
  const handleAdd = useCallback(async () => {
    if (!formData.name.trim()) {
      addToast('warning', '请输入 MCP server 名称')
      return
    }
    // 根据类型校验必填字段
    if (formData.type === 'stdio' && !formData.command?.trim()) {
      addToast('warning', 'STDIO 模式需要填写启动命令')
      return
    }
    if ((formData.type === 'http' || formData.type === 'sse' || formData.type === 'streamable-http') && !formData.url?.trim()) {
      addToast('warning', 'HTTP/SSE 模式需要填写 URL')
      return
    }

    setAdding(true)
    try {
      const { default: bridge } = await import('@/services/bridgeService')
      if (!bridge.isAvailable()) {
        addToast('warning', '未连接到后端')
        return
      }

      // 构造配置，argsText 字符串分割为 args 数组
      const config: McpServerConfig = {
        name: formData.name.trim(),
        type: formData.type,
        ...(formData.type === 'stdio' ? {
          command: formData.command?.trim(),
          args: formData.argsText ? formData.argsText.split(/\s+/).filter(Boolean) : undefined,
          dir: formData.dir?.trim() || undefined,
        } : {
          url: formData.url?.trim(),
        }),
      }

      const result = await bridge.addMcpServer(config)
      if (result?.success) {
        addToast('success', result.message || `MCP server '${config.name}' 已添加`)
        setShowAddForm(false)
        setFormData({ name: '', type: 'stdio', command: '', argsText: '', url: '', dir: '' })
        await loadStatus()
      } else {
        addToast('error', result?.message || `添加 '${config.name}' 失败`)
      }
    } catch {
      addToast('error', '添加 MCP server 失败')
    } finally {
      setAdding(false)
    }
  }, [formData, addToast, loadStatus])

  // ── 移除 MCP server ──
  const handleRemove = useCallback(async (server: string) => {
    setRemoving(server)
    try {
      const { default: bridge } = await import('@/services/bridgeService')
      if (!bridge.isAvailable()) {
        addToast('warning', '未连接到后端')
        return
      }
      const result = await bridge.removeMcpServer(server)
      if (result?.success) {
        addToast('success', result.message || `MCP server '${server}' 已移除`)
        await loadStatus()
      } else {
        addToast('error', result?.message || `移除 '${server}' 失败`)
      }
    } catch {
      addToast('error', `移除 '${server}' 失败`)
    } finally {
      setRemoving(null)
    }
  }, [addToast, loadStatus])

  return (
    <div className="space-y-4">
      {/* 顶部操作栏 */}
      <div className="flex items-center justify-between">
        <p className="text-xs text-slate-500 dark:text-slate-400 flex-1">
          MCP (Model Context Protocol) 服务器为 E小智 提供扩展工具能力。
        </p>
        <div className="flex items-center gap-2">
          <button
            onClick={handleAddViaChat}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs rounded-lg bg-gradient-to-r from-purple-600 to-blue-600 text-white hover:from-purple-700 hover:to-blue-700 transition-all"
            title="通过对话引导添加 MCP 服务器"
          >
            <Sparkles className="w-3.5 h-3.5" />
            对话添加
          </button>
          <button
            onClick={() => setShowAddForm(!showAddForm)}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs rounded-lg bg-blue-600 text-white hover:bg-blue-700 transition-colors"
          >
            <Plus className="w-3.5 h-3.5" />
            手动添加
          </button>
          <button
            onClick={loadStatus}
            disabled={loading}
            className="flex items-center gap-1.5 px-3 py-1.5 text-xs rounded-lg bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300 hover:bg-slate-200 dark:hover:bg-slate-700 transition-colors disabled:opacity-50"
          >
            {loading ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <RefreshCw className="w-3.5 h-3.5" />}
            刷新
          </button>
        </div>
      </div>

      {/* 添加 MCP 表单 */}
      {showAddForm && (
        <div className="rounded-xl border border-blue-200 dark:border-blue-900/50 bg-blue-50/30 dark:bg-blue-900/10 p-4 space-y-3">
          <div className="flex items-center justify-between">
            <h4 className="text-sm font-medium text-slate-700 dark:text-slate-200">添加 MCP 服务器</h4>
            <button
              onClick={() => setShowAddForm(false)}
              className="text-xs text-slate-400 hover:text-slate-600"
            >
              取消
            </button>
          </div>

          {/* 名称 + 类型 */}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="text-xs text-slate-500 dark:text-slate-400 mb-1 block">名称 *</label>
              <input
                type="text"
                value={formData.name}
                onChange={(e) => setFormData({ ...formData, name: e.target.value })}
                placeholder="如: filesystem"
                className="w-full px-3 py-1.5 text-xs rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-800 dark:text-slate-100 outline-none focus:border-blue-500"
              />
            </div>
            <div>
              <label className="text-xs text-slate-500 dark:text-slate-400 mb-1 block">传输方式</label>
              <select
                value={formData.type}
                onChange={(e) => setFormData({ ...formData, type: e.target.value as McpServerConfig['type'] })}
                className="w-full px-3 py-1.5 text-xs rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-800 dark:text-slate-100 outline-none focus:border-blue-500"
              >
                <option value="stdio">STDIO（本地进程）</option>
                <option value="http">HTTP</option>
                <option value="streamable-http">Streamable HTTP</option>
                <option value="sse">SSE</option>
              </select>
            </div>
          </div>

          {/* STDIO 模式字段 */}
          {formData.type === 'stdio' && (
            <>
              <div>
                <label className="text-xs text-slate-500 dark:text-slate-400 mb-1 block">启动命令 *</label>
                <input
                  type="text"
                  value={formData.command || ''}
                  onChange={(e) => setFormData({ ...formData, command: e.target.value })}
                  placeholder="如: npx 或 node"
                  className="w-full px-3 py-1.5 text-xs rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-800 dark:text-slate-100 outline-none focus:border-blue-500 font-mono"
                />
              </div>
              <div>
                <label className="text-xs text-slate-500 dark:text-slate-400 mb-1 block">参数（空格分隔）</label>
                <input
                  type="text"
                  value={formData.argsText || ''}
                  onChange={(e) => setFormData({ ...formData, argsText: e.target.value })}
                  placeholder="如: -y @modelcontextprotocol/server-filesystem /tmp"
                  className="w-full px-3 py-1.5 text-xs rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-800 dark:text-slate-100 outline-none focus:border-blue-500 font-mono"
                />
              </div>
              <div>
                <label className="text-xs text-slate-500 dark:text-slate-400 mb-1 block">工作目录（可选）</label>
                <input
                  type="text"
                  value={formData.dir || ''}
                  onChange={(e) => setFormData({ ...formData, dir: e.target.value })}
                  placeholder="如: C:\\project"
                  className="w-full px-3 py-1.5 text-xs rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-800 dark:text-slate-100 outline-none focus:border-blue-500 font-mono"
                />
              </div>
            </>
          )}

          {/* HTTP/SSE 模式字段 */}
          {(formData.type === 'http' || formData.type === 'sse' || formData.type === 'streamable-http') && (
            <div>
              <label className="text-xs text-slate-500 dark:text-slate-400 mb-1 block">URL *</label>
              <input
                type="text"
                value={formData.url || ''}
                onChange={(e) => setFormData({ ...formData, url: e.target.value })}
                placeholder="如: http://localhost:3000/sse"
                className="w-full px-3 py-1.5 text-xs rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 text-slate-800 dark:text-slate-100 outline-none focus:border-blue-500 font-mono"
              />
            </div>
          )}

          {/* 提交按钮 */}
          <div className="flex justify-end gap-2 pt-1">
            <button
              onClick={() => setShowAddForm(false)}
              className="px-3 py-1.5 text-xs text-slate-500 hover:text-slate-700 dark:hover:text-slate-300 transition-colors"
            >
              取消
            </button>
            <button
              onClick={handleAdd}
              disabled={adding}
              className="px-4 py-1.5 text-xs bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors disabled:opacity-50 flex items-center gap-1.5"
            >
              {adding && <Loader2 className="w-3 h-3 animate-spin" />}
              {adding ? '添加中...' : '添加并启动'}
            </button>
          </div>
        </div>
      )}

      {/* 统计概览 */}
      <div className="grid grid-cols-4 gap-2">
        {[
          { label: '服务器', value: stats.total, color: 'text-slate-700 dark:text-slate-200' },
          { label: '已连接', value: stats.connected, color: 'text-emerald-600 dark:text-emerald-400' },
          { label: '工具总数', value: stats.tools, color: 'text-blue-600 dark:text-blue-400' },
          { label: '失败记录', value: stats.failures, color: stats.failures > 0 ? 'text-amber-600 dark:text-amber-400' : 'text-slate-700 dark:text-slate-200' },
        ].map((item) => (
          <div key={item.label} className="text-center p-2 rounded-lg bg-slate-50 dark:bg-slate-800/50">
            <p className={`text-lg font-bold ${item.color}`}>{item.value}</p>
            <p className="text-xs text-slate-500 dark:text-slate-400">{item.label}</p>
          </div>
        ))}
      </div>

      {/* 提示消息（MCP 未配置等） */}
      {message && (
        <div className="flex items-center gap-2 p-3 rounded-lg bg-amber-50 dark:bg-amber-900/20 border border-amber-200 dark:border-amber-800">
          <AlertCircle className="w-4 h-4 text-amber-500 shrink-0" />
          <p className="text-xs text-amber-700 dark:text-amber-400">{message}</p>
        </div>
      )}

      {/* 失败记录 */}
      {failures.length > 0 && (
        <div className="rounded-xl border border-slate-200 dark:border-slate-700 overflow-hidden">
          <button
            onClick={() => setShowFailures(!showFailures)}
            className="w-full flex items-center justify-between px-4 py-3 text-left hover:bg-slate-50 dark:hover:bg-slate-800/50 transition-colors"
          >
            <div className="flex items-center gap-2">
              <AlertCircle className="w-4 h-4 text-amber-500" />
              <span className="text-sm font-medium text-slate-700 dark:text-slate-200">
                失败记录 ({failures.length})
              </span>
            </div>
            <ChevronRight className={`w-4 h-4 text-slate-400 transition-transform ${showFailures ? 'rotate-90' : ''}`} />
          </button>
          {showFailures && (
            <div className="px-4 pb-3 space-y-2 border-t border-slate-100 dark:border-slate-700">
              {failures.map((f, i) => (
                <div key={i} className="py-2 px-3 rounded-lg bg-red-50 dark:bg-red-900/20 border border-red-100 dark:border-red-900/40">
                  <div className="flex items-center justify-between mb-1">
                    <span className="text-xs font-medium text-red-700 dark:text-red-400 font-mono">{f.server}</span>
                    <span className="text-xs text-slate-400">{f.time}</span>
                  </div>
                  <p className="text-xs text-red-600 dark:text-red-400 break-all">{f.error}</p>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* 服务器列表 */}
      <div>
        <div className="flex items-center justify-between mb-2">
          <h3 className="text-sm font-semibold text-slate-700 dark:text-slate-200">
            MCP 服务器
          </h3>
          <span className="text-xs text-slate-400">{servers.length} 个</span>
        </div>

        {servers.length === 0 ? (
          <div className="text-center py-8">
            <Server className="w-10 h-10 text-slate-300 dark:text-slate-600 mx-auto mb-2" />
            <p className="text-sm text-slate-500 dark:text-slate-400">
              {loading ? '加载中...' : '暂无 MCP 服务器'}
            </p>
            <p className="text-xs text-slate-400 mt-1">
              点击上方"添加"按钮，或编辑 config.json 的 mcpServers 字段
            </p>
          </div>
        ) : (
          <div className="space-y-2">
            {servers.map((server) => {
              const isExpanded = expandedServers.has(server.name)
              const isRestarting = restarting === server.name
              const isDiagnosing = diagnosing === server.name
              const diag = diagnoseResult[server.name]

              return (
                <div
                  key={server.name}
                  className={`rounded-xl border overflow-hidden transition-all ${
                    server.connected
                      ? 'border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800'
                      : 'border-red-200 dark:border-red-900/50 bg-red-50/30 dark:bg-red-900/10'
                  }`}
                >
                  {/* 标题行 */}
                  <div className="flex items-center gap-3 px-3 py-2.5">
                    <button
                      onClick={() => toggleExpand(server.name)}
                      className="shrink-0"
                    >
                      <ChevronRight className={`w-4 h-4 text-slate-400 transition-transform ${isExpanded ? 'rotate-90' : ''}`} />
                    </button>

                    {server.connected ? (
                      <PlugZap className="w-4 h-4 text-emerald-500 shrink-0" />
                    ) : (
                      <Plug className="w-4 h-4 text-red-400 shrink-0" />
                    )}

                    <div className="flex items-center gap-2 min-w-0 flex-1">
                      <span className="text-sm font-medium text-slate-800 dark:text-slate-100 font-mono truncate">
                        {server.name}
                      </span>
                      <span className={`inline-flex items-center px-1.5 py-0.5 text-xs rounded ${transportColor(server.transport)}`}>
                        {transportLabel(server.transport)}
                      </span>
                      {server.connected ? (
                        <span className="inline-flex items-center gap-0.5 text-xs text-emerald-600 dark:text-emerald-400">
                          <CheckCircle2 className="w-3 h-3" />
                          已连接
                        </span>
                      ) : (
                        <span className="inline-flex items-center gap-0.5 text-xs text-red-500 dark:text-red-400">
                          <XCircle className="w-3 h-3" />
                          未连接
                        </span>
                      )}
                    </div>

                    <div className="flex items-center gap-1 text-xs text-slate-500 dark:text-slate-400">
                      <Wrench className="w-3 h-3" />
                      <span>{server.toolCount}</span>
                    </div>

                    {/* 操作按钮 */}
                    <div className="flex items-center gap-1">
                      <button
                        onClick={() => handleDiagnose(server.name)}
                        disabled={isDiagnosing}
                        className="p-1 text-slate-400 hover:text-blue-500 dark:hover:text-blue-400 rounded transition-colors disabled:opacity-50"
                        title="诊断"
                      >
                        {isDiagnosing ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Stethoscope className="w-3.5 h-3.5" />}
                      </button>
                      <button
                        onClick={() => handleRestart(server.name)}
                        disabled={isRestarting}
                        className="p-1 text-slate-400 hover:text-amber-500 dark:hover:text-amber-400 rounded transition-colors disabled:opacity-50"
                        title="重启"
                      >
                        {isRestarting ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <RotateCw className="w-3.5 h-3.5" />}
                      </button>
                      <button
                        onClick={() => handleRemove(server.name)}
                        disabled={removing === server.name}
                        className="p-1 text-slate-400 hover:text-red-500 dark:hover:text-red-400 rounded transition-colors disabled:opacity-50"
                        title="移除"
                      >
                        {removing === server.name ? <Loader2 className="w-3.5 h-3.5 animate-spin" /> : <Trash2 className="w-3.5 h-3.5" />}
                      </button>
                    </div>
                  </div>

                  {/* 展开内容：工具列表 + 诊断结果 */}
                  {isExpanded && (
                    <div className="px-3 pb-3 pl-10 space-y-3 border-t border-slate-100 dark:border-slate-700">
                      {/* 能力概览 */}
                      <div className="flex items-center gap-3 pt-2 text-xs text-slate-500 dark:text-slate-400">
                        <span className="flex items-center gap-1">
                          <Wrench className="w-3 h-3" /> 工具 {server.toolCount}
                        </span>
                        {server.hasPrompts && <span className="text-blue-500">✓ Prompts</span>}
                        {server.hasResources && <span className="text-purple-500">✓ Resources</span>}
                      </div>

                      {/* 工具列表 */}
                      {server.tools && server.tools.length > 0 ? (
                        <div className="space-y-1">
                          <p className="text-xs font-medium text-slate-600 dark:text-slate-400">工具清单</p>
                          {server.tools.map((tool) => (
                            <div key={tool.name} className="flex items-start gap-2 py-1 px-2 rounded bg-slate-50 dark:bg-slate-800/50">
                              <Wrench className="w-3 h-3 text-slate-400 mt-0.5 shrink-0" />
                              <div className="min-w-0 flex-1">
                                <div className="flex items-center gap-2 flex-wrap">
                                  <span className="text-xs font-mono text-slate-700 dark:text-slate-300">{tool.name}</span>
                                  {tool.readOnly && (
                                    <span className="text-xs px-1 rounded bg-blue-100 text-blue-600 dark:bg-blue-900/30 dark:text-blue-400">只读</span>
                                  )}
                                  {tool.destructive && (
                                    <span className="text-xs px-1 rounded bg-red-100 text-red-600 dark:bg-red-900/30 dark:text-red-400">破坏性</span>
                                  )}
                                </div>
                                {tool.description && (
                                  <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5 break-words">
                                    {tool.description}
                                  </p>
                                )}
                              </div>
                            </div>
                          ))}
                        </div>
                      ) : (
                        <p className="text-xs text-slate-400 italic">无工具</p>
                      )}

                      {/* 诊断结果 */}
                      {diag && (
                        <div className={`rounded-lg p-3 border ${
                          diag.healthy
                            ? 'bg-emerald-50 dark:bg-emerald-900/20 border-emerald-200 dark:border-emerald-800'
                            : 'bg-amber-50 dark:bg-amber-900/20 border-amber-200 dark:border-amber-800'
                        }`}>
                          <div className="flex items-center gap-2 mb-2">
                            <Activity className={`w-4 h-4 ${diag.healthy ? 'text-emerald-500' : 'text-amber-500'}`} />
                            <span className="text-xs font-medium text-slate-700 dark:text-slate-200">
                              诊断结果 {diag.timestamp && `· ${diag.timestamp}`}
                            </span>
                          </div>
                          <p className="text-xs text-slate-600 dark:text-slate-400 mb-2">{diag.summary}</p>
                          {diag.checks && diag.checks.length > 0 && (
                            <div className="space-y-1">
                              {diag.checks.map((c, i) => (
                                <div key={i} className="flex items-start gap-2 text-xs">
                                  {c.passed ? (
                                    <CheckCircle2 className="w-3 h-3 text-emerald-500 mt-0.5 shrink-0" />
                                  ) : (
                                    <XCircle className="w-3 h-3 text-red-500 mt-0.5 shrink-0" />
                                  )}
                                  <div className="min-w-0 flex-1">
                                    <span className="font-mono text-slate-600 dark:text-slate-400">{c.check}</span>
                                    {c.detail && (
                                      <p className="text-slate-500 dark:text-slate-500 break-words mt-0.5">{c.detail}</p>
                                    )}
                                  </div>
                                  {c.durationMs != null && (
                                    <span className="text-slate-400 shrink-0">{c.durationMs}ms</span>
                                  )}
                                </div>
                              ))}
                            </div>
                          )}
                        </div>
                      )}
                    </div>
                  )}
                </div>
              )
            })}
          </div>
        )}
      </div>
    </div>
  )
}
