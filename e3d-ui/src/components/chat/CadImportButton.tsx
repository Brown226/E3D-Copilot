/**
 * CadImportButton — CAD 导入按钮组件
 * 
 * 功能：
 * 1. 点击弹出 CAD 导入面板
 * 2. 支持三种导入方式：
 *    - 连接 AutoCAD（获取选中对象）
 *    - 选择 DWG 文件
 *    - 输入坐标字符串
 * 3. 预览导入结果
 * 4. 生成 PML 脚本
 */

import { useState, useCallback } from 'react'
import { FileUp, Link, Code, X, CheckCircle } from 'lucide-react'
import { useChatStore } from '@/store/useChatStore'
import bridge from '@/services/bridgeService'

// ── 导入方式 ──
type ImportMode = 'autocad' | 'file' | 'coordinates'

// ── 组件状态 ──
interface CadImportState {
  isOpen: boolean
  mode: ImportMode
  coordinates: string
  wallHeight: number
  wallThickness: number
}

const INITIAL_STATE: CadImportState = {
  isOpen: false,
  mode: 'autocad',
  coordinates: '',
  wallHeight: 3000,
  wallThickness: 200
}

export function CadImportButton() {
  const [state, setState] = useState<CadImportState>(INITIAL_STATE)
  const sendMessage = useChatStore((s) => s.sendMessage)

  // ── 切换面板显示 ──
  const togglePanel = useCallback(() => {
    setState(prev => ({ ...prev, isOpen: !prev.isOpen }))
  }, [])

  // ── 连接 AutoCAD（发送消息让 AI 处理） ──
  const connectAutoCad = useCallback(() => {
    togglePanel() // 先关闭面板
    sendMessage(bridge.sendUserMessage.bind(bridge), [], '连接 AutoCAD')
  }, [sendMessage, togglePanel])

  // ── 获取选中对象（发送消息让 AI 处理） ──
  const getSelectedObjects = useCallback(() => {
    togglePanel()
    sendMessage(bridge.sendUserMessage.bind(bridge), [], '获取 AutoCAD 中选中的对象')
  }, [sendMessage, togglePanel])

  // ── 从坐标创建墙体（发送消息让 AI 处理） ──
  const importFromCoordinates = useCallback(() => {
    if (!state.coordinates.trim()) return
    togglePanel()
    const message = `从这个坐标创建墙体：${state.coordinates}，墙高${state.wallHeight}mm，墙厚${state.wallThickness}mm`
    sendMessage(bridge.sendUserMessage.bind(bridge), [], message)
  }, [sendMessage, togglePanel, state.coordinates, state.wallHeight, state.wallThickness])

  // ── 渲染状态图标 ──
  if (!state.isOpen) {
    return (
      <button
        onClick={togglePanel}
        className="flex items-center gap-1.5 px-2.5 py-1.5 text-xs font-medium text-gray-600 dark:text-gray-400 hover:text-gray-900 dark:hover:text-gray-100 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-lg transition-colors"
        title="导入 CAD 图纸"
      >
        <FileUp className="w-4 h-4" />
        <span>导入 CAD</span>
      </button>
    )
  }

  return (
    <div 
      className="fixed bg-white dark:bg-gray-900 border border-gray-200 dark:border-gray-700 rounded-xl shadow-xl overflow-hidden"
      style={{
        bottom: '80px',
        left: '20px',
        width: '320px',
        zIndex: 99999
      }}
    >
      {/* 头部 */}
      <div className="flex items-center justify-between px-4 py-3 border-b border-gray-200 dark:border-gray-700">
        <h3 className="text-sm font-semibold text-gray-900 dark:text-gray-100">导入 CAD 图纸</h3>
        <button
          onClick={togglePanel}
          className="p-1 hover:bg-gray-100 dark:hover:bg-gray-800 rounded-lg transition-colors"
        >
          <X className="w-4 h-4 text-gray-500" />
        </button>
      </div>

      {/* 导入方式选择 */}
      <div className="flex gap-1 p-3 border-b border-gray-200 dark:border-gray-700">
        <button
          onClick={() => setState(prev => ({ ...prev, mode: 'autocad' }))}
          className={`flex-1 flex items-center justify-center gap-1.5 px-3 py-2 text-xs font-medium rounded-lg transition-colors ${
            state.mode === 'autocad'
              ? 'bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300'
              : 'text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800'
          }`}
        >
          <Link className="w-3.5 h-3.5" />
          AutoCAD
        </button>
        <button
          onClick={() => setState(prev => ({ ...prev, mode: 'file' }))}
          className={`flex-1 flex items-center justify-center gap-1.5 px-3 py-2 text-xs font-medium rounded-lg transition-colors ${
            state.mode === 'file'
              ? 'bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300'
              : 'text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800'
          }`}
        >
          <FileUp className="w-3.5 h-3.5" />
          DWG 文件
        </button>
        <button
          onClick={() => setState(prev => ({ ...prev, mode: 'coordinates' }))}
          className={`flex-1 flex items-center justify-center gap-1.5 px-3 py-2 text-xs font-medium rounded-lg transition-colors ${
            state.mode === 'coordinates'
              ? 'bg-blue-100 dark:bg-blue-900/30 text-blue-700 dark:text-blue-300'
              : 'text-gray-600 dark:text-gray-400 hover:bg-gray-100 dark:hover:bg-gray-800'
          }`}
        >
          <Code className="w-3.5 h-3.5" />
          坐标
        </button>
      </div>

      {/* 内容区域 */}
      <div className="p-4 space-y-3">
        {/* AutoCAD 模式 */}
        {state.mode === 'autocad' && (
          <>
            <div className="space-y-2">
              <button
                onClick={connectAutoCad}
                className="w-full flex items-center justify-center gap-2 px-4 py-2.5 bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium rounded-lg transition-colors"
              >
                <Link className="w-4 h-4" />
                连接 AutoCAD
              </button>
              
              <button
                onClick={getSelectedObjects}
                className="w-full flex items-center justify-center gap-2 px-4 py-2.5 bg-green-600 hover:bg-green-700 text-white text-sm font-medium rounded-lg transition-colors"
              >
                <CheckCircle className="w-4 h-4" />
                获取选中对象
              </button>
            </div>
            
            <p className="text-xs text-gray-500 dark:text-gray-400">
              1. 先启动 AutoCAD 并打开图纸<br/>
              2. 点击"连接 AutoCAD"<br/>
              3. 在 AutoCAD 中选中要导入的图形<br/>
              4. 点击"获取选中对象"
            </p>
          </>
        )}

        {/* DWG 文件模式 */}
        {state.mode === 'file' && (
          <div className="space-y-2">
            <div className="border-2 border-dashed border-gray-300 dark:border-gray-600 rounded-lg p-6 text-center">
              <FileUp className="w-8 h-8 mx-auto mb-2 text-gray-400" />
              <p className="text-sm text-gray-600 dark:text-gray-400">
                拖拽 DWG 文件到此处，或点击选择文件
              </p>
              <input
                type="file"
                accept=".dwg,.dxf"
                className="hidden"
                id="dwg-file-input"
              />
              <label
                htmlFor="dwg-file-input"
                className="mt-2 inline-block px-4 py-2 bg-gray-100 dark:bg-gray-800 text-sm font-medium text-gray-700 dark:text-gray-300 rounded-lg cursor-pointer hover:bg-gray-200 dark:hover:bg-gray-700 transition-colors"
              >
                选择文件
              </label>
            </div>
          </div>
        )}

        {/* 坐标模式 */}
        {state.mode === 'coordinates' && (
          <div className="space-y-2">
            <textarea
              value={state.coordinates}
              onChange={(e) => setState(prev => ({ ...prev, coordinates: e.target.value }))}
              placeholder="输入坐标，格式：[(0,0,0),(5000,0,0)],[(5000,0,0),(5000,3000,0)]"
              className="w-full h-24 px-3 py-2 text-sm border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-800 text-gray-900 dark:text-gray-100 placeholder-gray-400 focus:outline-none focus:ring-2 focus:ring-blue-500 resize-none"
            />
          </div>
        )}

        {/* 参数设置 */}
        <div className="grid grid-cols-2 gap-3">
          <div>
            <label className="block text-xs font-medium text-gray-700 dark:text-gray-300 mb-1">
              墙高 (mm)
            </label>
            <input
              type="number"
              value={state.wallHeight}
              onChange={(e) => setState(prev => ({ ...prev, wallHeight: Number(e.target.value) }))}
              className="w-full px-3 py-1.5 text-sm border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-800 text-gray-900 dark:text-gray-100 focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
          <div>
            <label className="block text-xs font-medium text-gray-700 dark:text-gray-300 mb-1">
              墙厚 (mm)
            </label>
            <input
              type="number"
              value={state.wallThickness}
              onChange={(e) => setState(prev => ({ ...prev, wallThickness: Number(e.target.value) }))}
              className="w-full px-3 py-1.5 text-sm border border-gray-300 dark:border-gray-600 rounded-lg bg-white dark:bg-gray-800 text-gray-900 dark:text-gray-100 focus:outline-none focus:ring-2 focus:ring-blue-500"
            />
          </div>
        </div>

        {/* 导入按钮（坐标模式） */}
        {state.mode === 'coordinates' && state.coordinates && (
          <button
            onClick={importFromCoordinates}
            className="w-full flex items-center justify-center gap-2 px-4 py-2.5 bg-blue-600 hover:bg-blue-700 text-white text-sm font-medium rounded-lg transition-colors"
          >
            <FileUp className="w-4 h-4" />
            创建墙体
          </button>
        )}
      </div>
    </div>
  )
}
