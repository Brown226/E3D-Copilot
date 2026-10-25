---
name: e3d-element-ops
description: E3D 元素查询/修改/检查/批量操作规范 — 定位、属性读写、批量安全流程、检查类型真实可用性。做 query/modify/batch/check 前必读。合并原 element-resolution-guide + batch-modify-workflow + e3d-batch-modify + model-check-routine + e3d-piping-design 五个 skill。
runAs: inline
tags: [元素, 定位, 查询, 修改, 批量, 检查, 安全]
---

# E3D 元素操作规范

> 覆盖四件事：**怎么定位元素、怎么改属性、批量怎么才安全、check 哪些类型真能用**。
> 核心原则：**先查后改、批量必预览、改完必复核**。

---

## 一、工具选择（先看这张，选错工具是最大浪费）

| 场景 | 用什么 | 不要用 |
|------|--------|--------|
| 查元素清单 | `query` | `execute_pml` |
| **读一个元素的属性** | `get_attributes` | `query`（不含属性）、`execute_pml` |
| 改单个元素 | `modify` | `batch`（杀鸡用牛刀） |
| **改多个元素** | `batch` | `modify`（不支持批量条件） |
| 元素是否存在 | `check(type=exists)` | `query`（空结果不代表不存在） |
| 属性是否合规 | `check(type=attribute)` | 手写 PML 比对 |
| 元素空间位置 | `geometry` | `execute_pml` |
| 层级/父子关系 | `hierarchy` | 手写 PML 遍历 |

---

## 二、元素定位（失败率最高的环节）

### 2.1 DBURI vs NAME

```
✅ /PIPE-001               DBURI，以 / 开头，直接定位
✅ /ZONE-PIPE-01/PIPE-002  多级路径
❌ PIPE-001                不带 / → 走 NAME 递归匹配，慢且可能匹配错
```

- DBURI：层级路径，类似文件系统。`query` / `modify` / `check` / `get_attributes` 都优先用 DBURI
- NAME 递归：不带 `/` 时从 CE 向上找 SITE 再向下递归，**O(N)**，且同名时只取第一个

### 2.2 UI 显示名 ≠ 内部 NAME（最常见的坑）

E3D 树状图里显示的名称**不是** `NAME` 属性。从 UI 复制过来的名字常常定位失败。

**定位失败的标准三步**：

```
1. query(type=<类型>, name='*关键字*')     → 拿真实 NAME
2. 用返回的 NAME 拼 DBURI（加 / 前缀）
3. 再调 modify / get_attributes / check
```

### 2.3 通配符

```
query(type=PIPE, name='*DN100*')    含 DN100
query(type=PIPE, name='PIPE-*')     以 PIPE- 开头
query(type=ALL)                     所有类型（传 '*' 或 'ALL' 都行）
```

> 早期实现里 `type=ALL` 用 substring 匹配曾只匹配到 `WALL`，现已修复。但**明确传具体类型更可靠**。

### 2.4 scope 的语义

`scope` 是**搜索起点**，会递归子元素：

- `scope="/"` 或不传 → 从顶层找
- `scope="CE"` → 从当前元素开始
- `scope="/ZONE-PIPE-01"` → 只在该 ZONE 下找

**坑**：`type=ZONE` 时如果 `scope` 设成一个具体 ZONE，就会在它**内部**找 ZONE，结果为空。查顶层类型时不传 scope。

### 2.5 CE 跟踪

`RealE3DEnvironment` 订阅 `CurrentElementChanged` 自动跟随用户点击。
**不要手动设 CE**（`$!elementName`），会触发事件链干扰自动跟踪。

### 2.6 定位失败排查表

| 现象 | 原因 | 解决 |
|------|------|------|
| `query` 返回空 | scope 元素不存在 | 去掉 scope 或改用 DBURI |
| `modify` 报「元素不存在」 | NAME 不匹配（UI 名 ≠ 内部名） | 先 `query(name='*关键字*')` |
| `check` 报「元素不存在」 | element 参数不带 `/` | 加 `/` 前缀 |
| `hierarchy(direction=down)` 结果异常 | 实现有缺陷 | 改用 `query(type=ALL, scope=...)` |
| 从 UI 复制的名称无效 | 显示名 ≠ NAME | 用 query 查真实 NAME |

---

## 三、属性读写

### 3.1 读：`get_attributes`

```
get_attributes(element=/PIPE-001, attributes=[WTHK, DIA])
get_attributes(element=/PIPE-001)              -- 不指定则返回全部
```

### 3.2 写：`modify`

```
modify(dburi=/PIPE-001, attributes={WTHK: SCH40, DIA: 100})
modify(dburi=/PIPE-001, attributes={WTHK: SCH40}, preview=true)   -- 只预览
```

`modify` 失败时返回 `{"success": false}`，**不抛异常**。调用方必须检查返回值，不能只看工具是否报错。

### 3.3 属性名大小写

E3D 属性名**不区分大小写**，但统一用大写更稳妥：`WTHK` / `DIA` / `SPRE` / `NAME` / `ROOM_NO` / `DESC`。

属性映射表：`read_file("knowledge/domain/attribute_map.md")`

---

## 四、批量修改：`batch` 五步法

**批量是唯一会造成不可逆破坏的操作**（E3D 无全局 Undo）。>5 个元素触发强制审批，**连「完全访问」模式也会拦**（这是安全下限，不是配置问题）。

### 4.1 五步法（不可跳过）

```
Step 1  query          — 确认目标清单与数量
Step 2  batch(preview=true)  — 预览元素 + 当前值
Step 3  展示计划       — 表格列 名称/当前值/目标值，问用户
Step 4  batch(preview=false) — 用户审批后执行
Step 5  get_attributes — 抽样复核改后值
```

**绝对禁止**：
- 跳过 Step 1-3 直接执行
- 用户未明确同意就 Step 4
- 跳过 Step 5 直接汇报「已完成」

### 4.2 参数

```
batch(
  query_type=PIPE,              -- 必填
  query_name='*DN100*',         -- 可选
  query_scope=/ZONE-PIPE-01,    -- 可选
  attributes={WTHK: SCH40},     -- 必填
  preview=true,                 -- 默认 false
  limit=50                      -- 默认 50，内部强制 ≤200
)
```

**返回结构**：`{success: N, failed: M, total: N+M, errors: [...]}`

**单条失败不中断**：会继续处理剩余元素。所以**必须看 `failed` 和 `errors`**，不能说「已完成 N 个」而漏掉失败项。

### 4.3 展示格式（Step 3 必须这么写）

```
找到 12 个目标元素，计划修改 WTHK 为 SCH40：

| # | 名称 | 当前 WTHK | 目标 WTHK |
|---|------|----------|----------|
| 1 | PIPE-001 | SCH20 | SCH40 |
| 2 | PIPE-002 | SCH20 | SCH40 |
| 3 | PIPE-003 | (空)  | SCH40 |
...

确认执行吗？
```

### 4.4 汇报格式（Step 5 必须这么写）

```
✅ 批量修改完成：
- 成功：10 个
- 失败：2 个（PIPE-007 属性不存在、PIPE-011 权限不足）
- 已复核前 3 个：WTHK 均为 SCH40
```

### 4.5 回滚

- `preview` 阶段已记录原值
- 失败项保持原值，**已成功的不自动回滚**
- 需要整体回滚时，用 `batch` 按记录的原值反向改
- 大批量前建议先 `/rewind` 记一个检查点

---

## 五、`check` 类型真实可用性（重要）

`schema` 里列了 10 种，但**只有 6 种真正实现**。选错会得到「尚未实现」的空结果。

| type | 状态 | 说明 |
|------|:----:|------|
| `exists` | ✅ | 元素存在性 |
| `attribute` | ✅ | 单属性值校验，需 `attribute` + `expected` |
| `attribute_complete` | ✅ | 必填属性非空 |
| `naming` | ✅ | 正则命名校验，需 `pattern` |
| `name_consistency` | ✅ | 内置命名规则 |
| `clearance` | ❌ | **返回「净距检查尚未实现」** |
| `distance` | ❌ | **返回「净距检查尚未实现」** |
| `bore_consistency` | ❌ | **返回「管径一致性检查尚未实现」** |
| `change_status` | ❌ | **返回「变更状态检查尚未实现」** |
| `room_number` | ❌ | **返回「房间号检查尚未实现」** |

**遇到 ❌ 类型时**：不要反复重试，也不要假装它返回了结果。改为：
- 间距类 → `geometry(action=distance_between, element=...)`
- 通径一致性 → `query` 取清单 + 逐个 `get_attributes` 比对
- 复杂规则 → `execute_pml` 写脚本（先查 `patterns/` 范式）

`check` 只支持**单元素**，没有批量参数。批量检查用 `query` + 循环 `check`。

---

## 六、管道专项

### 6.1 查询与修改

```
query(type=PIPE, name='*DN100*', scope=/ZONE-PIPE-01)
get_attributes(element=/PIPE-001, attributes=[DIA, WTHK, SPRE])
modify(dburi=/PIPE-001, attributes={WTHK: SCH40})
```

### 6.2 创建

```
piping(action=create_pipe, name=PIPE-NEW-001, parent=/ZONE-PIPE-01, attributes={...})
piping(action=create_branch, pipe=/PIPE-NEW-001)
piping(action=add_fitment, pipe=/PIPE-NEW-001, fitmentType=FLANGE)
```

**创建前先查同名是否已存在**（`check(type=exists)`），否则会产生重名元素。

### 6.3 设计参数合规

材料/壁厚/等级的行业规则见 `run_skill(name='iso-and-standards')`。

---

## 七、安全红线

| 规则 | 说明 |
|------|------|
| 单次批量 ≤ 50 个 | 超过分批。工具内部上限 200，但 200 是硬截断不是推荐值 |
| 必须预览 | 任何批量前展示计划表格 |
| 必须审批 | 用户未明确确认前不执行 |
| 异常不中断 | 看 `failed` / `errors`，不忽略 |
| 记录旧值 | 大批量前记检查点 |
| 禁止盲改 | 不确定的属性值先 `get_attributes` 确认 |
| 禁止读用 PML | 读属性一律 `get_attributes` |

---

## 八、相关资源

- PML 编写（万不得已时）：`run_skill(name='pml-authoring')`
- 管道设计规范：`run_skill(name='iso-and-standards')`
- 属性映射表：`read_file("knowledge/domain/attribute_map.md")`
- 元素类型表：`read_file("knowledge/domain/element_types.md")`
- 工具选择指南：`read_file("knowledge/domain/tool_selection_guide.md")`
- 错误码：`read_file("knowledge/domain/error_codes.md")`
- 属性读写范式：`read_file("knowledge/patterns/modify_attributes.md")`
- 错误处理范式：`read_file("knowledge/patterns/error_handling.md")`
