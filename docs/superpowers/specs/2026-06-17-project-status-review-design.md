# E3D-E小智 项目现状梳理与路径 A 保守修复方案

**文档类型**：设计规格说明书  
**创建日期**：2026-06-17  
**版本**：v1.0  
**状态**：待审批

---

## 一、项目现状分析

### 1.1 文档体系（优秀）

项目已建立完整的文档体系：
- **22+ 设计文档**：架构、工具、UI、部署、错误处理、记忆系统、API 核实
- **API 核实报告**：7793 个 API 交叉验证，修正 3 处致命幻觉
- **Phase 计划**：4 个阶段详细规划（Phase 0-3）
- **开发引领手册**：编码前必读指南

### 1.2 代码实际情况（与文档有偏差）

#### 实际存在的代码

| 项目 | 文件数 | 核心组件 |
|------|--------|----------|
| **E3DCopilot.Core** | 22 | AgentLoop、CopilotController、VllmProvider、ToolExecutor、PermissionGate、7 个 ToolHandler |
| **E3DCopilot.Tools** | 9 | ToolRegistry、ToolRouter、PmlEngine、PmlGenerator、CsharpBridge（mock） |
| **E3DCopilot.WebHost** | 5 | WebViewForm、Bridge、CopilotAddinBoot、CopilotEventDispatcher |
| **E3DCopilot.TestHost** | 2 | MainForm、Program |
| **web-ui/** | 100+ | React 前端（fork 自 cline-chinese-main） |

#### 文档描述但实际不存在的代码

**Phase 1-3 报告中描述的 29 个 WinForms UI 文件全部不存在**：
- ❌ ChatMessage.cs、EventDispatcher.cs、MarkdownParser.cs、MarkdownPanel.cs
- ❌ AnimationHelper.cs、LcsDiff.cs、ToolCardControl.cs、ThinkingPanel.cs
- ❌ PromptShelfControl.cs、TurnActionsBar.cs、InputPanel.cs
- ❌ NavToolbar.cs、WarmTurnCard.cs
- ❌ SettingsPanel.cs、HistoryPanel.cs、VirtualScrollPanel.cs
- ❌ SessionExportService.cs、ErrorBoundary.cs、UndoRewindBanner.cs

**E3DCopilot.UI 项目从未创建**

**46 个专用工具的实现代码全部缺失**（Tools 项目子目录为空）

---

## 二、关键问题识别

### 2.1 致命问题

#### 问题 1：CsharpBridge 全是 Mock

```csharp
// CsharpBridge.cs - 当前实现
public string QueryElement(string elementName)
{
    return $"[模拟] 查询元素: {elementName}";  // ❌ 没有调用真实 E3D API
}
```

**影响**：端到端闭环从未打通，所有工具调用返回假数据。

#### 问题 2：Phase 报告与实际不符

6 份 Phase 报告（1a-3）描述的文件从未创建，报告记录的是"意图"而非"事实"。

**影响**：后续开发基于错误的前提，无法准确评估进度。

#### 问题 3：前端方案过度工程

`web-ui/` 是 cline-chinese-main 的完整 fork（100+ 组件），包含：
- 40+ LLM Provider 选择器（OpenAI、Anthropic、Gemini...）
- MCP 服务器配置界面
- Onboarding 引导流程
- Browser Session 展示
- Diff Edit 视图

**E3D-E小智 实际只需要**：聊天框 + 工具卡片 + 审批按钮 + 设置页（API 地址）。

### 2.2 中等问题

#### 问题 4：两个 UI 方案并存造成混乱

- WinForms UI（Phase 计划）和 WebView2+React（实际实现）同时存在
- 文档中混用两种方案的术语

#### 问题 5：零测试覆盖

- 测试策略文档存在，但无实际测试代码
- AgentLoop、ToolExecutor 等核心组件无单元测试

#### 问题 6：工具范围过大

- 计划 46 个工具（6 核心 + 41 专用）
- MVP 阶段只需 10-15 个 P0 工具

---

## 三、架构评估

### 3.1 设计优秀的部分

| 方面 | 评价 |
|------|------|
| **分层架构** | Core → Tools → WebHost 清晰分离，依赖方向正确 |
| **PML 优先策略** | 复杂操作生成 PML 脚本，简单操作 C# 直调，务实 |
| **安全三层模型** | 只读自动、写入确认、批量审批，符合工业软件安全要求 |
| **API 核实方法** | 用真实 DLL 反射验证，标杆级实践 |
| **错误处理设计** | 错误分类 + 重试 + 回滚，考虑周全 |

### 3.2 需要改进的部分

| 方面 | 问题 | 建议 |
|------|------|------|
| **前端复杂度** | React fork 过重 | 精简到 15 个核心组件 |
| **工具粒度** | 41 个专用工具太细 | 合并为 15 个 P0 工具 |
| **测试覆盖** | 零测试 | 至少 AgentLoop + ToolExecutor 要有单元测试 |
| **文档同步** | Phase 报告与实际不符 | 立即更新，标注真实状态 |

---

## 四、路径 A 保守修复方案

### 4.1 核心目标

**在 2-3 周内，让"查询 DN100 管道属性"这个场景端到端完整走通**：

1. 用户在 WebView2 输入"查询 DN100 管道"
2. AI 流式回复 + 调用 query 工具
3. CsharpBridge 真实调用 E3D API（不是 mock）
4. 结果返回并显示在 UI 上

### 4.2 三大原则

| 原则 | 说明 |
|------|------|
| **先修文档** | 更新 Phase 报告，标注真实状态，消除认知偏差 |
| **先通后全** | 先打通 1 个场景，再扩展工具数量 |
| **先简后繁** | 前端精简到 15 个组件，不做 cline 的 40+ Provider 选择器 |

### 4.3 任务分解

#### Week 1：修复 + 精简

1. **更新所有 Phase 报告**，标注"计划中"vs"已实现"
2. **精简 web-ui**：删除不需要的组件（MCP、Onboarding、40+ Provider 选择器）
3. **实现 CsharpBridge 的真实 E3D API 调用**（从 DbElement.GetElement 开始）

#### Week 2：打通闭环

4. **实现 query 工具的真实逻辑**（调用 CsharpBridge）
5. **验证 AgentLoop → ToolExecutor → CsharpBridge → E3D 全链路**
6. **添加基础单元测试**（AgentLoop + ToolExecutor）

#### Week 3：验证 + 部署

7. **在 E3D 环境中端到端测试**
8. **编写部署脚本**
9. **演示"查询 DN100 管道"完整场景**

---

## 五、前端精简方案

### 5.1 当前问题

`web-ui/` 包含大量 E3D-E小智 不需要的功能：
- 40+ LLM Provider 选择器
- MCP 服务器配置界面
- Onboarding 引导流程
- Browser Session 展示
- Diff Edit 视图

### 5.2 精简策略

**保留 15 个核心组件**，删除其余 85+ 个：

#### 保留的组件

```
src/components/
├── chat/
│   ├── ChatView.tsx          # 聊天主视图
│   ├── ChatTextArea.tsx      # 输入框
│   ├── ChatRow.tsx           # 消息行
│   ├── UserMessage.tsx       # 用户消息
│   └── ThinkingRow.tsx       # AI 思考过程
├── settings/
│   └── SettingsView.tsx      # 简化版设置（只保留 API 地址、模型选择）
├── history/
│   └── HistoryView.tsx       # 历史记录
└── common/
    ├── MarkdownBlock.tsx     # Markdown 渲染
    ├── CodeBlock.tsx         # 代码块
    └── Button.tsx            # 通用按钮
```

#### 删除的组件（示例）

```
❌ settings/OpenAiPicker.tsx（40+ Provider 选择器）
❌ mcp/McpView.tsx（MCP 配置）
❌ onboarding/OnboardingView.tsx（引导流程）
❌ chat/BrowserSessionRow.tsx（浏览器会话）
❌ chat/DiffEditRow.tsx（代码 Diff）
❌ chat/AutoApproveMenu/（自动批准菜单）
```

### 5.3 精简后的 UI 结构

```
App.tsx
├── ChatView（主视图）
│   ├── ChatTextArea（输入）
│   ├── MessagesArea（消息列表）
│   │   ├── UserMessage（用户消息）
│   │   ├── ChatRow（AI 回复）
│   │   └── ThinkingRow（思考过程）
│   └── ToolCard（工具执行卡片，新增）
├── SettingsView（设置，简化版）
└── HistoryView（历史记录）
```

### 5.4 新增组件

**ToolCard.tsx**：展示工具执行状态和结果

```typescript
interface ToolCardProps {
  toolName: string;        // "query" | "modify" | "execute_pml"
  status: "running" | "success" | "failed";
  input: object;           // 工具输入参数
  output: object;          // 工具执行结果
  requiresApproval?: boolean;  // 是否需要审批
}
```

---

## 六、CsharpBridge 真实实现方案

### 6.1 当前问题

```csharp
// CsharpBridge.cs - 全是 Mock
public string QueryElement(string elementName)
{
    return $"[模拟] 查询元素: {elementName}";
}
```

### 6.2 真实实现策略

#### Step 1：实现基础 API 调用（Week 1）

```csharp
public class CsharpBridge
{
    // 查询元素
    public DbElement QueryElement(string elementName)
    {
        return ThreadMarshaller.Invoke(() => 
        {
            return DbElement.GetElement(elementName);
        });
    }
    
    // 读取属性
    public string GetAttribute(DbElement element, string attributeName)
    {
        return ThreadMarshaller.Invoke(() => 
        {
            var attr = DbAttribute.GetDbAttribute(attributeName);
            return element.GetAsString(attr);
        });
    }
    
    // 执行 PML
    public string ExecutePml(string pmlCode)
    {
        return ThreadMarshaller.Invoke(() => 
        {
            var cmd = Command.CreateCommand(pmlCode);
            return cmd.RunInPdms();
        });
    }
}
```

#### Step 2：实现 query 工具的真实逻辑（Week 2）

```csharp
// Core/Tools/Handlers/DbQueryHandler.cs
public class DbQueryHandler : IToolHandler
{
    public ToolResult Execute(ToolInput input, CsharpBridge bridge)
    {
        var elementName = input.Parameters["elementName"].ToString();
        var element = bridge.QueryElement(elementName);
        
        if (element == null || element.IsNull)
        {
            return ToolResult.Error($"元素不存在: {elementName}");
        }
        
        // 读取常用属性
        var name = bridge.GetAttribute(element, "NAME");
        var type = bridge.GetAttribute(element, "TYPE");
        
        return ToolResult.Success(new
        {
            ElementName = elementName,
            Name = name,
            Type = type
        });
    }
}
```

#### Step 3：端到端验证（Week 2-3）

```
用户输入 → AgentLoop → VllmProvider → LLM 响应
    ↓
解析 ToolUse → ToolExecutor → DbQueryHandler
    ↓
CsharpBridge.QueryElement → E3D API → 返回结果
    ↓
结果反馈给 LLM → 生成最终回复 → 显示在 UI
```

### 6.3 关键技术点

#### 1. 线程模型

```csharp
// 所有 E3D API 调用必须通过 ThreadMarshaller 切换到 UI 线程
ThreadMarshaller.Invoke(() => {
    // E3D API 调用
});
```

#### 2. 错误处理

```csharp
try
{
    var element = bridge.QueryElement("PIPE-001");
    if (element.IsNull)
    {
        return ToolResult.Error("元素不存在");
    }
}
catch (Exception ex)
{
    return ToolResult.Error($"查询失败: {ex.Message}");
}
```

#### 3. 属性读取链路

```csharp
// 正确写法（来自 API 核实报告）
var attr = DbAttribute.GetDbAttribute("NAME");
var value = element.GetAsString(attr);

// 错误写法（会导致运行时崩溃）
var value = element.GetAttribute("NAME").ToString();
```

---

## 七、风险评估

### 7.1 风险矩阵

| 风险 | 概率 | 影响 | 缓解措施 |
|------|:----:|:----:|----------|
| **E3D 环境不可用** — 无法在真实 E3D 中测试 | 中 | 高 | 先用 TestHost 模拟 E3D API 接口，确保代码逻辑正确；真实环境测试推迟到 Week 3 |
| **WebView2 运行时缺失** — 目标机器没有 WebView2 | 低 | 中 | WebHost 启动时检测，提示用户安装 Microsoft Edge WebView2 Runtime |
| **LLM 输出不稳定** — Qwen 生成的工具调用格式不一致 | 中 | 中 | ToolValidator 加强参数校验；System Prompt 中注入严格的工具调用格式示例 |
| **精简前端破坏构建** — 删除组件后 React 项目编译失败 | 中 | 低 | 每删一个组件跑一次 `npm run build`，渐进式删除 |
| **ThreadMarshaller 死锁** — UI 线程阻塞导致死锁 | 低 | 高 | LLM 调用走异步 Task，只有 E3D API 调用才走 ThreadMarshaller；避免在 UI 线程 await |

---

## 八、验证标准

### 8.1 Week 1 结束时

- [ ] Phase 报告已更新，标注真实状态
- [ ] web-ui 精简到 15 个组件
- [ ] CsharpBridge 实现 QueryElement、GetAttribute、ExecutePml

### 8.2 Week 2 结束时

- [ ] query 工具真实调用 CsharpBridge
- [ ] 端到端链路打通（用户输入 → LLM → 工具 → E3D → 结果返回）
- [ ] 基础单元测试通过

### 8.3 Week 3 结束时

- [ ] 在 E3D 环境中演示"查询 DN100 管道"场景
- [ ] 部署脚本可用
- [ ] 文档与代码一致

---

## 九、下一步行动

### 9.1 依赖关系图

```
Week 1                          Week 2                          Week 3
┌─────────────────────┐        ┌─────────────────────┐        ┌─────────────────────┐
│ ① 更新 Phase 报告    │        │ ④ query 工具真实实现  │        │ ⑦ E3D 环境端到端测试 │
│ ② 精简 web-ui       │───────→│ ⑤ 全链路验证         │───────→│ ⑧ 部署脚本           │
│ ③ CsharpBridge 真实  │        │ ⑥ 基础单元测试       │        │ ⑨ 演示 + 文档同步     │
│    实现              │        │                     │        │                     │
└─────────────────────┘        └─────────────────────┘        └─────────────────────┘
         ↑ 无依赖                        ↑ 依赖 ②③                       ↑ 依赖 ④⑤
```

### 9.2 Phase 0 的第一步

| 序号 | 行动 | 预计耗时 | 产出 |
|:----:|------|:-------:|------|
| 1 | 更新 6 份 Phase 报告，标注真实状态 | 0.5 天 | 准确的进度文档 |
| 2 | 精简 web-ui：删除不需要的组件和文件 | 1 天 | 15 个核心组件的精简前端 |
| 3 | 实现 CsharpBridge 的 3 个核心方法 | 1 天 | QueryElement / GetAttribute / ExecutePml |
| 4 | 实现 DbQueryHandler 真实逻辑 | 0.5 天 | 第一个真实可用的工具 |
| 5 | 端到端链路调试 | 1 天 | 用户输入 → E3D 结果完整走通 |

**总计：4 天完成最小闭环**（留有 1 周缓冲应对意外）

---

## 十、成功标准（Definition of Done）

Phase 0 结束时，必须满足以下**全部条件**：

1. ✅ **文档准确**：所有 Phase 报告反映真实代码状态
2. ✅ **前端精简**：web-ui 只保留 15 个核心组件，`npm run build` 通过
3. ✅ **真实调用**：CsharpBridge 调用真实 E3D API（不是 mock）
4. ✅ **端到端**：用户说"查询 /PIPE-001"→ AI 回复管道属性 → 数据来自真实 E3D 数据库
5. ✅ **可复现**：在 TestHost 中可以独立运行演示

---

## 十一、总结

| 维度 | 结论 |
|------|------|
| **项目可行性** | ✅ 完全可行，架构设计成熟，核心代码已存在 |
| **最大障碍** | ⚠️ CsharpBridge 是 mock + 前端过重 + 文档与代码脱节 |
| **推进策略** | 路径 A：保守修复，2-3 周打通最小闭环 |
| **成功关键** | 先让 1 个场景完整走通，再扩展工具数量 |
| **最大风险** | E3D 测试环境不可用（缓解：TestHost 模拟） |

**一句话总结**：项目的基础架构和文档质量远超预期，但需要从"完美设计"切换到"先跑通再迭代"。Phase 0 的唯一目标是：**让一句话从用户输入到 E3D 操作结果完整走通**。

---

## 附录：参考文档

- [API 核实报告](../../verification/API核实报告.md)
- [架构设计](../../arch/架构设计.md)
- [开发引领手册](../../开发引领手册.md)
- [Phase 1-3 报告](../../verification/Phase*.md)
