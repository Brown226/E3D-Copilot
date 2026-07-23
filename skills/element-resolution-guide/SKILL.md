---
name: element-resolution-guide
description: 元素定位规范 — DBURI格式、NAME匹配、UI显示名差异、通配符规则。定位元素失败时必读。
runAs: inline
tags: [元素, 定位, DBURI, NAME, 通配符]
---

# 元素定位规范

> 汇总 E3D 元素定位的工程约定，源自真实踩坑经验。
> **定位元素失败时必读此 skill**，避免 ResolveElement 返回空、query 返回空、modify 报"元素不存在"。

---

## 一、元素定位规则

### 1.1 DBURI 格式（推荐，必须以 `/` 开头）

```
✅ /PIPE-001、/ZONE-PIPE-01/PIPE-002、/SITE-01
❌ PIPE-001（不带斜杠，走 NAME 递归匹配，慢且可能匹配错）
```

- DBURI 是元素的层级路径，类似文件系统路径
- 顶层是 SITE，下级 ZONE，再下 PIPE/EQUI/STRU 等
- 调用 `query(scope=...)` / `modify(dburi=...)` / `check(element=...)` 时优先用 DBURI

### 1.2 NAME 递归匹配（兜底）

不带 `/` 的字符串走 NAME 递归匹配：

- 从当前 CE 开始向上找到 SITE，再向下递归找 NAME 完全匹配的元素
- **慢**（O(N) 遍历）
- **可能匹配多个**同名元素，取第一个

### 1.3 UI 显示名 ≠ 内部 NAME（最常见的坑）

E3D UI 树状图显示的名称可能不是真实 NAME 属性，导致定位失败。

**定位失败时**：
1. 先 `query(type=PIPE, name='*关键字*')` 查真实 NAME
2. 用返回的 NAME 拼 DBURI
3. 再调用 `modify` / `get_attributes` / `check`

---

## 二、通配符规则

### 2.1 类型通配

```
query(type='*')      — 返回所有类型
query(type='ALL')    — 返回所有类型（不是只匹配 WALL！）
query(type='PIPE')   — 只返回 PIPE
```

**坑**：早期实现用 substring 匹配，`ALL` 只匹配到 `WALL`。现已修复，但要明确传 `*` 或 `ALL`。

### 2.2 名称通配

```
query(type=PIPE, name='*DN100*')   — 含 DN100 的管道
query(type=PIPE, name='PIPE-*')    — 以 PIPE- 开头
query(type=PIPE, name='*-001')     — 以 -001 结尾
```

- `*` 匹配任意字符
- PML 内对应 `Matchwild(name, '*DN100*')`

---

## 三、CE 跟踪约定

- `RealE3DEnvironment` 订阅 `CurrentElementChanged` 事件，自动跟踪 CE
- 不要手动 `$!elementName` 设 CE（会触发事件链）
- `scope=CE` 表示从当前元素开始查询

---

## 四、常见定位失败场景

| 现象 | 原因 | 解决 |
|------|------|------|
| `query` 返回空 | scope 元素未找到 | 改用 DBURI 或先 `query(name='*关键字*')` 查真实 NAME |
| `modify` 报"元素不存在" | NAME 不匹配 | 同上，先用 query 查 |
| `check` 报"元素不存在" | element 参数不是 DBURI | 加 `/` 前缀，或先 query 查真实 NAME |
| `hierarchy(direction=down)` 不可靠 | 实现缺陷 | 改用 `query(type=ALL, scope=...)` |
| UI 里的名称 copy 到代码里无效 | 显示名 ≠ 内部 NAME | 用 query 查真实 NAME |

---

## 五、关于 PML 执行（AI 不需要关心的内部细节）

AI 通过 `execute_pml(script="...")` 工具传 PML 脚本字符串即可，以下由 `RealE3DEnvironment` 内部处理：

- 写入临时宏文件
- 用 `$m "路径"` 执行
- UTF-8 无 BOM 编码
- 内层 30s + 外层 60s 超时保护
- 错误通过 `RunInPdms() == false` + `cmd.Result` 回传

**AI 只需要**：
- 写正确的 PML 语法（参见 `run_skill(name='aveva-pml-language')`）
- 不需要自己包 `handle any`（内部已移除前置 handle，错误由 PmlErrorMapper 转换）
- 不需要关心宏文件路径、编码、超时

---

## 六、相关资源

- 元素导航范式：`read_file("knowledge/patterns/element_navigation.md")`
- 错误处理范式：`read_file("knowledge/patterns/error_handling.md")`
- PML 语法基础：`run_skill(name='aveva-pml-language')`
- 属性映射表：`read_file("knowledge/domain/attribute_map.md")`
