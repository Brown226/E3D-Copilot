# E3D-E小智 前端UI设计文档

## 📋 项目概览

**项目名称**：E3D-E小智 AI Copilot  
**前端技术栈**：React 18 + TypeScript + Vite 7 + Tailwind CSS 4  
**UI组件库**：HeroUI + Radix UI + Lucide React  
**开发服务器**：http://localhost:25463  

---

## 🎨 设计风格

### 主题配置
- **主色调**：深色主题（VS Code风格）
- **背景色**：`#0c0c14` (深色) / `#f3f3f3` (浅色)
- **主强调色**：`#3b82f6` (蓝色)
- **辅助色**：`#9663f1` (紫色 - Cline品牌色)
- **成功色**：`#56D364` (绿色)
- **警告色**：`#f59e0b` (黄色)
- **错误色**：`#ef4444` (红色)

### 设计语言
- **圆角**：`0.625rem` (10px)
- **字体**：
  - 界面：`ui-monospace, SFMono-Regular, Menlo, Monaco, Consolas`
  - 编辑器：`14px`
- **动画**：
  - 淡入：`fadeIn 0.4s ease-out`
  - 光标闪烁：`cursorBlink 1s ease-in-out infinite`
  - 滑动淡入：`fadeSlideIn 0.6s cubic-bezier(0.16, 1, 0.3, 1)`

---

## 🏗️ 核心组件架构

### 1. 主应用布局 (`App.tsx`)
```
Providers (主题/国际化/状态管理)
  └─ AppContent
       ├─ WelcomeView (欢迎页)
       ├─ SettingsView (设置页)
       ├─ HistoryView (历史记录)
       └─ ChatView (聊天主界面)
```

### 2. 聊天界面 (`ChatView.tsx`)

#### 布局结构
```
ChatLayout
├─ 顶部区域
│  ├─ TaskSection (任务标题 + API指标)
│  └─ WelcomeSection (欢迎信息)
├─ 中间区域
│  └─ MessagesArea (消息列表)
│      ├─ ChatRow (AI消息)
│      ├─ UserMessage (用户消息)
│      ├─ ThinkingRow (思考过程)
│      ├─ CommandOutputRow (命令输出)
│      └─ ErrorRow (错误信息)
└─ 底部区域 (footer)
   ├─ AutoApproveBar (自动批准栏)
   ├─ ActionButtons (操作按钮)
   └─ InputSection (输入区域)
      └─ ChatTextArea (多行输入框)
```

#### 关键特性
- ✅ **流式输出**：支持打字机效果的流式消息渲染
- ✅ **多文件支持**：支持上传图片和文件（最多8个）
- ✅ **Markdown渲染**：`react-markdown` + `rehype-highlight`
- ✅ **Mermaid图表**：支持流程图/时序图渲染
- ✅ **@提及功能**：支持@文件/@目录/@问题等上下文提及
- ✅ **斜杠命令**：支持 `/help`, `/clear` 等快捷命令
- ✅ **思考过程展示**：可展开/折叠的AI思考过程
- ✅ **工具调用展示**：展示AI调用的工具和执行结果

### 3. 输入框组件 (`ChatTextArea.tsx`)

#### 功能特性
- **自动调整高度**：`react-textarea-autosize`
- **拖拽上传**：支持拖拽文件到输入框
- **粘贴图片**：支持Ctrl+V粘贴剪贴板图片
- **@提及菜单**：输入`@`触发上下文菜单
- **斜杠命令菜单**：输入`/`触发命令菜单
- **引用消息**：支持引用之前的消息
- **计划/执行模式切换**：支持Plan Mode和Act Mode切换

### 4. 设置页面 (`SettingsView.tsx`)

#### 标签页结构
```typescript
[
  { id: "api-config", name: "API配置", icon: SlidersHorizontal },
  { id: "features", name: "功能设置", icon: CheckCheck },
  { id: "browser", name: "浏览器设置", icon: SquareMousePointer },
  { id: "terminal", name: "终端设置", icon: SquareTerminal },
  { id: "general", name: "通用设置", icon: Wrench },
  { id: "about", name: "关于", icon: Info },
  { id: "debug", name: "调试", icon: FlaskConical, hidden: !IS_DEV }
]
```

---

## 🎯 UI组件库

### HeroUI 组件
- `Button` - 按钮
- `Input` - 输入框
- `Select` - 下拉选择
- `Switch` - 开关
- `Tabs` - 标签页
- `Tooltip` - 工具提示
- `Modal` - 模态框
- `Dropdown` - 下拉菜单

### Radix UI 原语
- `@radix-ui/react-dialog` - 对话框
- `@radix-ui/react-tooltip` - 提示
- `@radix-ui/react-popover` - 弹出框
- `@radix-ui/react-select` - 选择器
- `@radix-ui/react-switch` - 开关

### Lucide React 图标
常用图标：
- `Send` - 发送
- `Plus` - 添加
- `AtSign` - @提及
- `Paperclip` - 附件
- `Image` - 图片
- `Trash` - 删除
- `RefreshCw` - 刷新
- `ChevronDown` - 展开/折叠

---

## 📱 响应式设计

### 断点配置
```css
--breakpoint-xxs: 180px;
--breakpoint-xs: 400px;
```

### 适配策略
- **移动端**：隐藏部分UI元素，简化布局
- **平板**：显示完整功能，优化触摸交互
- **桌面端**：完整功能 + 快捷键支持

---

## ⚡ 性能优化

### 已实现的优化
1. **代码分割**：使用 Vite 动态导入
2. **虚拟滚动**：`react-virtuoso` 处理长消息列表
3. **懒加载**：组件级懒加载
4. **图片优化**：WebP格式 + 响应式图片
5. **CSS优化**：Tailwind CSS 按需生成
6. **Tree Shaking**：移除未使用代码

### 构建配置
```typescript
build: {
  outDir: "../src/E3DCopilot.WebHost/wwwroot",
  rollupOptions: {
    output: {
      inlineDynamicImports: true, // 单文件输出（适配WebView2）
    }
  }
}
```

---

## 🌐 国际化 (i18n)

### 支持的语言
- 🇨🇳 中文 (zh-CN)
- 🇺🇸 英文 (en-US)

### 实现方式
```typescript
import { useTranslation } from "react-i18next"

const { t } = useTranslation("common")
return <div>{t("chat.typeMessage")}</div>
```

---

## 🎬 动画效果

### Framer Motion 动画
- **消息出现**：`fadeIn + scale(0.98 → 1)`
- **滑动效果**：`translateY(-12px → 0)`
- **按钮悬停**：`scale(1 → 1.05)`
- **模态框**：`opacity(0 → 1) + scale(0.95 → 1)`

### CSS 动画
```css
/* 光标闪烁 */
@keyframes cursorBlink {
  0%, 100% { opacity: 1; }
  50% { opacity: 0; }
}

/* 渐变动画 */
@keyframes shimmer {
  0% { background-position: 200% 0; }
  100% { background-position: -200% 0; }
}
```

---

## 🔧 开发指南

### 启动开发服务器
```bash
cd E小智-v1.0-开发中/web-ui
npm run dev
# 访问 http://localhost:25463
```

### 构建生产版本
```bash
npm run build
# 输出到 ../src/E3DCopilot.WebHost/wwwroot
```

### 预览生产版本
```bash
npm run preview
```

---

## 📊 组件统计

### 聊天相关组件 (40+ 文件)
```
src/components/chat/
├─ ChatView.tsx (主聊天视图)
├─ ChatRow.tsx (AI消息行)
├─ ChatTextArea.tsx (输入框)
├─ UserMessage.tsx (用户消息)
├─ ThinkingRow.tsx (思考过程)
├─ CommandOutputRow.tsx (命令输出)
├─ ErrorRow.tsx (错误信息)
├─ MessagesArea/ (消息区域子组件)
├─ TaskSection/ (任务区域)
├─ InputSection/ (输入区域)
├─ auto-approve-menu/ (自动批准菜单)
└─ task-header/ (任务头)
```

### 设置相关组件
```
src/components/settings/
├─ SettingsView.tsx (设置主视图)
├─ SectionHeader.tsx (区域标题)
├─ sections/
│  ├─ ApiConfigurationSection.tsx (API配置)
│  ├─ FeatureSettingsSection.tsx (功能设置)
│  ├─ BrowserSettingsSection.tsx (浏览器设置)
│  ├─ TerminalSettingsSection.tsx (终端设置)
│  ├─ GeneralSettingsSection.tsx (通用设置)
│  ├─ AboutSection.tsx (关于)
│  └─ DebugSection.tsx (调试)
└─ utils/
   └─ providerUtils.ts (提供商工具)
```

---

## 🎨 主题定制

### CSS 变量覆盖
```css
:root {
  --vscode-focusBorder: #3b82f6; /* 焦点边框色 */
  --vscode-button-background: #3b82f6; /* 按钮背景 */
  --vscode-input-background: #1a1a24; /* 输入框背景 */
}
```

### 深色/浅色主题切换
```typescript
// 自动检测系统主题
const isDarkMode = window.matchMedia("(prefers-color-scheme: dark)").matches

// 手动切换
document.documentElement.classList.toggle("dark")
```

---

## 🚀 下一步优化建议

### UI/UX 改进
1. ✨ 添加消息搜索功能
2. 📌 固定重要消息
3. 🏷️ 消息标签分类
4. 📤 导出对话记录
5. 🎨 主题定制器（让用户自定义配色）
6. ⌨️ 更多快捷键支持
7. 🔊 语音输入支持
8. 📷 拍照识别（移动端）

### 性能优化
1. 🚀 实现 Service Worker 缓存
2. 📦 进一步优化包体积
3. 🖼️ 图片懒加载 + 渐进式加载
4. ⚡ 虚拟滚动优化（百万级消息）

---

## 📸 预览截图

**开发服务器已启动**：http://localhost:25463

请在浏览器中访问上述地址查看完整UI效果。

---

**文档版本**：v1.0  
**更新时间**：2026-06-18  
**维护者**：八哥
