---
name: batch-modify-workflow
description: 批量修改安全流程 — 用 batch 工具的 preview 模式强制"查-展-审-改-验"。涉及多个元素属性修改时必先调用此 skill。
runAs: inline
tags: [批量, 修改, 安全, 工作流]
---

# 批量修改安全流程

> 涉及多个元素（>5）的属性修改时，必须严格遵循此流程。
> 核心入口是 `batch` 工具（不是 `modify`），它内置 query + 循环 modify + dry-run 预览。
> 原则：**先查后改、preview 预览、等审批、执行后复核**。

---

## 一、工具选择

| 场景 | 用什么 | 说明 |
|------|-------|------|
| 单元素改属性 | `modify` | `dburi` + `attributes` 对象 |
| 批量改属性（>5 个元素） | `batch` | query + 循环 modify，支持 preview |
| 读取属性当前值 | `get_attributes` | 用于复核 |

**关键**：`modify` 工具**不支持批量**（没有 type/filter/scope 参数）。批量必须用 `batch`。

---

## 二、5 步强制流程

```
Step 1: query           — 查询确认目标元素清单
Step 2: batch(preview=true) — 预览将改的元素 + 当前值
Step 3: 展示计划         — 向用户展示 N 个元素的 名称/当前值/目标值
Step 4: batch(preview=false)— 等用户审批后再执行
Step 5: get_attributes   — 复核已改元素的值
```

**绝对禁止**：
- ❌ 跳过 Step 1-3 直接 `batch(preview=false)`
- ❌ Step 4 在用户未审批前执行
- ❌ Step 5 跳过复核直接汇报"已完成"

---

## 三、Step 1：查询确认目标

```
query(type=PIPE, name='*DN100*', scope='/ZONE-PIPE-01', limit=50)
```

**参数**：
- `type`（必填）：PIPE / EQUI / STRU / BRAN / ZONE 等
- `name`：支持 `*` 通配符
- `scope`：DBURI 或 `CE`
- `limit`：默认 50

**注意**：scope 必须是 DBURI 格式（以 `/` 开头）。定位失败时参见 `run_skill(name='element-resolution-guide')`。

---

## 四、Step 2-3：预览 + 展示计划

```
batch(
  query_type=PIPE,
  query_name='*DN100*',
  query_scope='/ZONE-PIPE-01',
  attributes={"WTHK": "SCH40"},
  preview=true,
  limit=50
)
```

`preview=true` 时 `batch` 工具会：
- 查询匹配元素
- 读取每个元素的当前属性值
- 返回"将被修改的元素 + 当前值"清单（不实际修改）

**展示给用户的格式**：

```
找到 N 个目标元素，计划修改 WTHK 为 SCH40：

| # | 名称        | 当前 WTHK | 目标 WTHK |
|---|------------|----------|----------|
| 1 | PIPE-001   | SCH20    | SCH40    |
| 2 | PIPE-002   | SCH20    | SCH40    |
| 3 | PIPE-003   | (空)     | SCH40    |
...

确认执行吗？
```

---

## 五、Step 4：执行 batch

用户审批后调用 `batch(preview=false)`：

```
batch(
  query_type=PIPE,
  query_name='*DN100*',
  query_scope='/ZONE-PIPE-01',
  attributes={"WTHK": "SCH40"},
  preview=false,
  limit=50
)
```

**工具行为**：
- 逐个调用 `modify`，单条失败不中断
- 返回 `{success: N, failed: M, total: N+M, errors: [...]}`

**limit 上限**：工具内部强制 `Math.Min(limit, 200)`，超过 200 自动截断。超过 200 时分批处理。

---

## 六、Step 5：复核与汇报

改完后用 `get_attributes` 复核前几个元素：

```
get_attributes(element='PIPE-001', attributes=['WTHK'])
```

**汇报格式**：

```
✅ 批量修改完成：
- 成功：N 个
- 失败：M 个（清单见 batch 返回的 errors）
- 已复核前 3 个元素的 WTHK 值均为 SCH40
```

---

## 七、回滚约定

- `batch` 执行前，preview 模式已记录原值
- 失败清单中的元素保持原值，不回滚已成功的
- 全部失败时向用户说明，并询问是否回滚已修改的部分（用 `batch` 改回原值）

---

## 八、相关资源

- 元素定位规范：`run_skill(name='element-resolution-guide')`
- 管道参数合规检查：`run_skill(name='piping-design-standards')`
- 修改属性范式：`read_file("knowledge/patterns/modify_attributes.md")`
- 工具选择决策：`read_file("knowledge/domain/tool_selection_guide.md")`
