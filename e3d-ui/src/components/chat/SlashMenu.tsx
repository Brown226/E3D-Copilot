/**
 * SlashMenu — / 触发的命令菜单
 * 在输入框中输入 / 时弹出，快速执行常用操作
 */

import { useState, useEffect, useRef, useMemo } from 'react'
import { Database, Edit3, Calculator, Download, Terminal, MessageSquare, Zap, FileUp, PenTool, Box, Link } from 'lucide-react'

interface SlashCommand {
  name: string
  label: string
  description: string
  icon: React.ReactNode
  template: string
  keywords: string[]
}

const COMMANDS: SlashCommand[] = [
  {
    name: 'query',
    label: '/query',
    description: '查询 E3D 数据库元素',
    icon: <Database className="w-4 h-4" />,
    template: '/query ',
    keywords: ['查询', 'query', '数据库', '元素'],
  },
  {
    name: 'modify',
    label: '/modify',
    description: '修改 E3D 元素属性',
    icon: <Edit3 className="w-4 h-4" />,
    template: '/modify ',
    keywords: ['修改', 'modify', '属性'],
  },
  {
    name: 'calculate',
    label: '/calculate',
    description: '执行数学计算',
    icon: <Calculator className="w-4 h-4" />,
    template: '/calculate ',
    keywords: ['计算', 'calculate', '数学'],
  },
  {
    name: 'export',
    label: '/export',
    description: '导出 E3D 数据',
    icon: <Download className="w-4 h-4" />,
    template: '/export ',
    keywords: ['导出', 'export', '文件'],
  },
  {
    name: 'pml',
    label: '/pml',
    description: '执行 PML 脚本命令',
    icon: <Terminal className="w-4 h-4" />,
    template: '/pml ',
    keywords: ['pml', '脚本', '命令'],
  },
  {
    name: 'new',
    label: '/new',
    description: '开始新会话',
    icon: <MessageSquare className="w-4 h-4" />,
    template: '',
    keywords: ['new', '新', '会话', '清空'],
  },
  {
    name: 'plan',
    label: '/plan',
    description: '切换到规划模式',
    icon: <Zap className="w-4 h-4" />,
    template: '',
    keywords: ['plan', '规划', '模式'],
  },
  {
    name: 'cad',
    label: '/cad',
    description: '导入 CAD 图纸到 E3D',
    icon: <FileUp className="w-4 h-4" />,
    template: '/cad ',
    keywords: ['cad', 'dwg', '导入', '图纸', 'dxf'],
  },
  {
    name: 'iso',
    label: '/iso',
    description: '生成管道 ISO 等轴测图',
    icon: <PenTool className="w-4 h-4" />,
    template: '/iso ',
    keywords: ['iso', '出图', '管道图', '等轴测'],
  },
  {
    name: 'structure',
    label: '/structure',
    description: '结构出图（DXF）',
    icon: <Box className="w-4 h-4" />,
    template: '/structure ',
    keywords: ['结构', '出图', 'dxf', '梁', '柱'],
  },
  {
    name: 'autocad',
    label: '/autocad',
    description: '连接 AutoCAD 实时联动',
    icon: <Link className="w-4 h-4" />,
    template: '/autocad ',
    keywords: ['autocad', '连接', '联动', '选中'],
  },
]

interface SlashMenuProps {
  query: string
  onSelect: (command: SlashCommand) => void
  onClose: () => void
}

export function SlashMenu({ query, onSelect, onClose }: SlashMenuProps) {
  const [selectedIdx, setSelectedIdx] = useState(0)
  const menuRef = useRef<HTMLDivElement>(null)

  const filtered = useMemo(() => {
    const q = query.toLowerCase().replace(/^\//, '')
    if (!q) return COMMANDS
    return COMMANDS.filter((cmd) =>
      cmd.name.includes(q) ||
      cmd.description.toLowerCase().includes(q) ||
      cmd.keywords.some((k) => k.includes(q))
    )
  }, [query])

  useEffect(() => { setSelectedIdx(0) }, [query])

  useEffect(() => {
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'ArrowDown') {
        e.preventDefault()
        setSelectedIdx((i) => Math.min(i + 1, filtered.length - 1))
      } else if (e.key === 'ArrowUp') {
        e.preventDefault()
        setSelectedIdx((i) => Math.max(i - 1, 0))
      } else if (e.key === 'Enter' && filtered[selectedIdx]) {
        e.preventDefault()
        onSelect(filtered[selectedIdx])
      } else if (e.key === 'Escape') {
        e.preventDefault()
        onClose()
      }
    }
    window.addEventListener('keydown', handler)
    return () => window.removeEventListener('keydown', handler)
  }, [filtered, selectedIdx, onSelect, onClose])

  if (filtered.length === 0) return null

  return (
    <div
      ref={menuRef}
      className="absolute bottom-full left-0 mb-2 w-72 bg-white dark:bg-slate-800 rounded-xl shadow-xl border border-slate-200 dark:border-slate-700 overflow-hidden z-[var(--z-dropdown)] animate-in fade-in slide-in-from-bottom-2 duration-150"
    >
      <div className="px-3 py-2 border-b border-slate-100 dark:border-slate-700">
        <p className="text-xs text-slate-400 dark:text-slate-500">命令</p>
      </div>
      <div className="max-h-[240px] overflow-y-auto py-1">
        {filtered.map((cmd, i) => (
          <button
            key={cmd.name}
            onClick={() => onSelect(cmd)}
            className={`w-full flex items-center gap-3 px-3 py-2 text-left transition-colors ${
              i === selectedIdx
                ? 'bg-blue-50 dark:bg-blue-900/30'
                : 'hover:bg-slate-50 dark:hover:bg-slate-700/50'
            }`}
          >
            <span className="text-slate-500 dark:text-slate-400">{cmd.icon}</span>
            <div className="flex-1 min-w-0">
              <div className="text-sm font-medium text-slate-800 dark:text-slate-200 font-mono">{cmd.label}</div>
              <div className="text-xs text-slate-400 dark:text-slate-500 truncate">{cmd.description}</div>
            </div>
          </button>
        ))}
      </div>
    </div>
  )
}
