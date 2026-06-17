# E小智 v1.0 — 开发中

## 项目定位

**AVEVA E3D AI Copilot** — 在 E3D 工厂设计软件内嵌的 AI 编程助手，让工程师用自然语言描述需求，AI 自动生成 PML 脚本并直接执行。

## 技术栈

- **语言**: C# / .NET Framework 4.8 (net48)
- **UI**: WinForms (DockedWindow 嵌入 E3D)
- **LLM**: vLLM 本地部署 + Qwen3.5/3.6（内网 OpenAI 兼容接口，`http://localhost:8000/v1`）
- **执行引擎**: PML 脚本执行为主，C# API 直调为辅
- **框架**: AVEVA ApplicationFramework（PML 执行：`Command.CreateCommand(str).RunInPdms()`）

## 项目结构

```
E小智-v1.0-开发中/
├── README.md                    # 本文件 — 项目概览
├── 开发引领手册.md                ← ⭐ 编码前必读（全局指导思想）
├── docs/                        # 文档总目录
│   ├── 文档索引.md                 ← 文档索引 + 关系图（阅读起点）
│   ├── plan/                    # 实施计划（总计划 + 4 个子阶段）
│   │   ├── 文档索引.md
│   │   ├── 阶段0-环境准备.md
│   │   ├── 阶段1-基础骨架.md
│   │   ├── 阶段2-工具扩展.md
│   │   └── 阶段3-增强功能.md
│   ├── arch/                    # 架构设计
│   │   ├── 架构设计.md
│   │   ├── 源码结构.md
│   │   ├── 部署指南.md
│   │   ├── 错误处理.md
│   │   └── 记忆系统.md
│   ├── design/                  # 功能规格
│   │   ├── 工具设计.md
│   │   ├── UI设计.md
│   │   ├── 插件注册.md
│   │   └── 系统提示词.md
│   ├── verification/            # 验证与测试
│   │   ├── API边界.md
│   │   ├── API核实报告.md
│   │   └── 测试策略.md
    │   └── reference/               # 技术参考
    │           ├── PML速查表.md
    │           ├── PDMS-PML基础.pdf
    │           └── Software Customisation Reference Manual.pdf
└── src/                         # 源码目录（待创建）
    ├── E3DCopilot.Addin/
    ├── E3DCopilot.Core/
    ├── E3DCopilot.Tools/
    └── E3DCopilot.UI/
```

## 快速导航

| 分类 | 文档 | 优先级 |
|------|------|:------:|
| 🧭 **开发引领** | [**开发引领手册.md**](开发引领手册.md) — 编码前必读 | 🥇 **先读我** |
| 🏛️ **架构设计** | [项目架构](docs/arch/架构设计.md) · [源码结构](docs/arch/源码结构.md) | ⭐⭐ |
| 📦 **部署** | [部署与打包](docs/arch/部署指南.md) ·  [错误处理](docs/arch/错误处理.md) | ⭐⭐ |
| 🧠 **记忆系统** | [长期记忆](docs/arch/记忆系统.md)（Phase 3）| ⭐ |
| ⚙️ **功能设计** | [工具说明](docs/design/工具设计.md) · [UI 设计](docs/design/UI设计.md) | ⭐⭐⭐ |
| 🔌 **E3D 集成** | [Addin 注册](docs/design/插件注册.md) · [System Prompt](docs/design/系统提示词.md) | ⭐⭐⭐ |
| 📋 **技术参考** | [PML 速查表](docs/reference/PML速查表.md) | ⭐⭐⭐ |
| 🔍 **验证** | [能力边界](docs/verification/API边界.md) · [核实报告](docs/verification/API核实报告.md) | ⚠️ 编码前必读 |
| 🧪 **测试** | [测试策略](docs/verification/测试策略.md) | ⭐ |
| 📅 **规划** | [总计划](docs/plan/文档索引.md) · [环境准备](docs/plan/阶段0-环境准备.md) · [Phase 1](docs/plan/阶段1-基础骨架.md) · [Phase 2](docs/plan/阶段2-工具扩展.md) · [Phase 3](docs/plan/阶段3-增强功能.md) | ⭐⭐⭐ |

## 状态

| 阶段 | 状态 | 说明 |
|------|------|------|
| 需求分析 | ✅ 完成 | 产品愿景、用户场景已确认 |
| 架构设计 | ✅ 完成 | 6 层架构 + PML 执行引擎（[架构设计.md](docs/arch/架构设计.md)）|
| 工具规划 | ✅ 完成 | 6 核心 + 41 专用（[工具设计.md](docs/design/工具设计.md)）|
| 源码结构 | ✅ 完成 | 4 项目结构（[源码结构.md](docs/arch/源码结构.md)）|
| 部署设计 | ✅ 完成 | DLL 注册 + PML 索引（[部署指南.md](docs/arch/部署指南.md)）|
| 错误处理 | ✅ 完成 | 错误分类 + 重试 + 回滚（[错误处理.md](docs/arch/错误处理.md)）|
| 长期记忆 | ✅ 完成 | 四层记忆架构（[记忆系统.md](docs/arch/记忆系统.md)，Phase 3）|
| API 核实 | ✅ 完成 | 7793 API + 257 页 PML 交叉验证（[API核实报告.md](docs/verification/API核实报告.md)）|
| API 修正 | ✅ 完成 | 修正 3 处致命幻觉 + C# 版本统一 |
| MVP 编码 | ⏳ 待开始 | 等待确认后启动 |

