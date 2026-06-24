# E小智 v2.0 前端

> AVEVA E3D AI 助手 — React 前端

## 技术栈

| 层 | 选型 | 版本 |
|---|------|------|
| 构建工具 | Vite | ^6.0 |
| 框架 | React | ^18.3 |
| 语言 | TypeScript | ~5.6 |
| 样式 | Tailwind CSS | ^4.0 |
| 状态管理 | Zustand | ^5.0 |
| Markdown | react-markdown + rehype-highlight | ^9.0 / ^7.0 |
| 图标 | lucide-react | ^0.468 |

## 目录结构

```
e3d-ui/
├── index.html                     # HTML 入口
├── package.json                   # 依赖与脚本
├── vite.config.ts                 # Vite 配置（路径别名 + Tailwind + Visualizer）
├── tsconfig.json                  # TypeScript 项目引用
├── tsconfig.app.json              # 应用 TS 配置
├── tsconfig.node.json             # Node 工具 TS 配置
├── README.md                      # 本文件
└── src/
    ├── main.tsx                   # React 入口
    ├── App.tsx                    # 根组件（布局）
    ├── index.css                  # Tailwind + 全局样式 + dark 主题
    ├── vite-env.d.ts              # Vite 类型声明
    ├── types/
    │   └── index.ts               # Message 类型定义
    ├── store/
    │   └── useChatStore.ts        # Zustand 状态管理
    ├── services/
    │   ├── messageContracts.ts    # bridge 消息协议（28+ 类型）
    │   └── bridgeService.ts       # bridge 通信 + store 映射 + mock 模式
    ├── hooks/
    │   └── useHeartbeat.ts        # 心跳检测 hook
    ├── utils/
    │   └── highlight.ts           # hljs 按需语言注册
    └── components/
        ├── Header.tsx             # 顶栏（标题 + 模型信息 + 操作按钮）
        ├── ErrorBoundary.tsx      # 全局错误边界
        ├── DisconnectScreen.tsx   # 断连提示
        ├── chat/
        │   ├── ChatView.tsx       # 聊天主视图
        │   ├── WelcomeScreen.tsx  # 空状态欢迎页
        │   ├── MessageList.tsx    # 消息列表（自动滚动）
        │   ├── MessageRow.tsx     # 消息分发器
        │   ├── UserBubble.tsx     # 用户消息
        │   ├── AssistantBubble.tsx # AI 回复（Markdown）
        │   ├── ThinkingBlock.tsx  # AI 推理过程
        │   ├── ToolCallCard.tsx   # 工具调用
        │   ├── ToolResultCard.tsx # 工具结果
        │   ├── ErrorCard.tsx      # 错误消息
        │   ├── InputBar.tsx       # 输入框
        │   └── ModelSwitcher.tsx  # 模型切换
        ├── settings/
        │   ├── SettingsPanel.tsx      # 设置面板（懒加载）
        │   └── ProviderSettingsSection.tsx  # Provider 管理
        └── common/
            ├── MarkdownBlock.tsx  # Markdown 渲染（裁剪自 Cline）
            └── CopyButton.tsx     # 复制按钮
```

## 开发

```bash
cd e3d-ui

# 安装依赖
npm install

# 启动开发服务器（支持 standalone mock，无需 C# 宿主）
npm run dev

# 构建生产版本
npm run build

# 预览构建产物
npm run preview

# 查看 Bundle 分析
# 构建后打开 stats.html（需在 dist/ 或项目根目录查找）
```

> **Standalone 模式**：在浏览器中直接打开 `http://localhost:5173` 即可运行，自动使用 mock 数据模拟 AI 回复，无需连接 E3D。

## 通信协议

- 使用 `chrome.webview.postMessage()` 与 C# 宿主通信
- 完整消息类型列表见 `src/services/messageContracts.ts`（28+ 类型）
- 所有消息以 JSON 格式传输，使用 `_requestId` 支持请求-响应模式
- C# 端对应处理见 `src/E3DCopilot.WebHost/Bridge.cs`

## 部署

```bash
# 方式一：运行项目根目录的 deploy.cmd
# 自动构建 e3d-ui + 复制到 E3D 安装目录

# 方式二：手动
cd e3d-ui
npm run build
# 构建产物自动输出到 ../src/E3DCopilot.WebHost/wwwroot/
```

`vite.config.ts` 已配置构建输出指向 C# 项目的 `wwwroot/` 目录。E3D 启动时 `WebViewForm.cs` 通过虚拟域名 `app.e3dcopilot.local` 加载前端。

## Bundle 体积（Phase 8 验收）

构建方式：`npm run build`

| 文件 | 大小 | Gzip |
|------|------|------|
| `index.html` | 0.5 KB | 0.3 KB |
| `assets/index-*.js`（主应用） | 559 KB | 172 KB |
| `assets/bridgeService-*.js` | 10 KB | 3 KB |
| `assets/SettingsPanel-*.js`（懒加载） | 17 KB | 4 KB |
| `assets/index-*.css` | 36 KB | 7 KB |
| **总计** | **623 KB** | **187 KB** |

> 相比旧 `web-ui/` 的 6.6MB 构建产物，**减少了 91%** 🎯

## 相关文档

- `docs/前端重设计划.md` — 架构设计
- `docs/compose/plans/e3d-ui-implementation-plan.md` — 实施计划
