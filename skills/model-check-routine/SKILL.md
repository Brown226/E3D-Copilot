---
name: model-check-routine
description: 模型检查路由表 — 按检查目的映射到 check 工具的正确 type 参数。做模型检查前先调用此 skill 选对子类型。
runAs: inline
tags: [检查, 验证, 模型, 路由]
---

# 模型检查路由表

> `check` 工具有 10 种 `type` 子类型，选错会导致检查逻辑跑偏。
> 做模型检查前先按此表选对 `type` 和必填参数。
> 原则：**先明确检查目的，再选 type**。

---

## 一、check 工具的真实参数

```
check(type=..., element=..., target=..., attribute=..., expected=..., pattern=...)
```

| 参数 | 说明 |
|------|------|
| `type`（必填） | 检查类型，见下表 |
| `element` / `target` | 目标元素名称或 DBURI（两者是别名） |
| `attribute` | 属性名（用于属性检查） |
| `expected` | 期望值（用于属性值检查） |
| `pattern` | 命名正则（用于命名规范检查） |

**重要**：`check` 工具**只支持单元素检查**，没有 `scope` / `elementType` / `attributes`（数组）等批量参数。批量检查需要 `query` 获取清单后循环 `check`，或用 `execute_pml` 写 PML 脚本。

---

## 二、检查目的 → check type 映射表

| 用户意图 | check type | 必填参数 | 说明 |
|---------|-----------|---------|------|
| "这个元素存在吗" | `exists` | `element` | 返回存在性 |
| "某个属性值对吗" | `attribute` | `element`, `attribute`, `expected` | 单属性值校验 |
| "属性填全了吗" | `attribute_complete` | `element` | 检查必填属性是否非空 |
| "命名符合正则吗" | `naming` | `element`, `pattern` | 用正则校验名称 |
| "命名规范吗" | `name_consistency` | `element` | 按内置命名规则校验 |
| "净距够吗" | `clearance` | `element` | 检查元素净距 |
| "两元素间距够吗" | `distance` | `element` | 计算与目标元素距离 |
| "通径一致吗" | `bore_consistency` | `element` | 检查通径一致性 |
| "改动了吗" | `change_status` | `element` | 查询修改状态 |
| "房间号对吗" | `room_number` | `element` | 检查房间号格式 |

---

## 三、典型用法

### 3.1 exists — 存在性检查

```
check(type=exists, element=/PIPE-001)
```

- 失败时建议用 `query(name='/PIPE-001*')` 查实际名称
- 参见 `read_file("knowledge/patterns/check_exists.md")`

### 3.2 attribute — 单属性值校验

```
check(type=attribute, element=/PIPE-001, attribute=WTHK, expected=SCH40)
```

### 3.3 attribute_complete — 属性完整性

```
check(type=attribute_complete, element=/PIPE-001)
```

**常见必填属性**（用 `get_attributes(all=true)` 读出后人工核对）：

| 元素类型 | 常用必填属性 |
|---------|------------|
| BRAN | DIA, WTHK, SPRE, SREF |
| PIPE | DIA, WTHK, SPRE, ROOM_NO |
| EQUI | ROOM_NO, DESC, FLNN |

参见 `read_file("knowledge/patterns/check_attribute_complete.md")`

### 3.4 naming — 命名正则校验

```
check(type=naming, element=/PIPE-001, pattern='^PIPE-\d{3}$')
```

### 3.5 name_consistency — 内置命名规范

```
check(type=name_consistency, element=/BRAN-001)
```

### 3.6 distance / clearance — 间距检查

```
check(type=distance, element=/EQUI-001)
check(type=clearance, element=/EQUI-001)
```

**注意**：`check` 没有提供第二个元素或 `min_distance` 参数。如需自定义间距阈值，改用 `geometry(action=distance_between, element=...)` 或 `execute_pml` 写 PML 脚本。

参见 `read_file("knowledge/patterns/check_distance.md")`

### 3.7 bore_consistency — 通径一致性

```
check(type=bore_consistency, element=/PIPE-001)
```

参见 `read_file("knowledge/patterns/check_bore_consistency.md")`

### 3.8 room_number — 房间号检查

```
check(type=room_number, element=/EQUI-001)
```

### 3.9 change_status — 改动状态

```
check(type=change_status, element=/PIPE-001)
```

---

## 四、批量检查编排

由于 `check` 只支持单元素，批量检查有两种方式：

### 方式 A：query + 循环 check

```
1. query(type=BRAN, scope=/ZONE-PIPE-01) → 获取元素清单
2. 对每个元素调用 check(type=attribute_complete, element=...)
3. 汇总 N 通过 / M 未通过
```

### 方式 B：execute_pml 写 PML 脚本

适合复杂批量检查（如遍历所有 BRAN 检查通径一致性）：

```pml
var !branches coll all BRAN for $!scope
var !failed !list()
DO !bran values !branches
    -- 调用检查逻辑
ENDDO
```

参见 `read_file("knowledge/patterns/element_navigation.md")`

---

## 五、工具选择决策

不确定用 `check` 还是 `get_attributes` 时：

| 场景 | 用什么 |
|------|-------|
| 验证属性是否符合规则 | `check` |
| 读取属性具体值 | `get_attributes` |
| 检查元素关系（通径/间距） | `check(type=bore_consistency/distance)` |
| 单纯查询元素清单 | `query` |
| 批量检查 | `query` + 循环 `check`，或 `execute_pml` |

参见 `read_file("knowledge/domain/tool_selection_guide.md")`

---

## 六、相关资源

- 存在性检查范式：`read_file("knowledge/patterns/check_exists.md")`
- 属性完整性范式：`read_file("knowledge/patterns/check_attribute_complete.md")`
- 通径一致性范式：`read_file("knowledge/patterns/check_bore_consistency.md")`
- 距离检查范式：`read_file("knowledge/patterns/check_distance.md")`
- PML 语法基础：`run_skill(name='aveva-pml-language')`
