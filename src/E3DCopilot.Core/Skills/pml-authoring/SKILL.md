---
name: pml-authoring
description: PML 编写与执行总纲 — 语法索引、黄金范式、执行约束、错误排查。要写 execute_pml 脚本、或 execute_pml 报错时必读。合并原 aveva-pml-language + element-resolution-guide 的执行部分。
runAs: inline
tags: [PML, 语法, 执行, 报错, 索引]
---

# PML 编写与执行

> PML 不是通用语言，模型的先验知识**多数是错的**。
> 本文只给「去哪里找」和「绝对不能写什么」，具体语法必须 `read_file` 加载下方文件后照抄范式。
> 原则：**宁可查一遍，不要凭记忆写。**

---

## 一、先看这个：执行约束（违反必然失败）

| 约束 | 说明 |
|------|------|
| **一个脚本只做一件事** | 长脚本中途失败时定位困难，拆成多次 `execute_pml` 调用 |
| **不要自己包 `handle any`** | `RealE3DEnvironment` 已移除前置 handle，错误由 `PmlErrorMapper` 统一转换。重复包裹会吞掉真实错误行号 |
| **不要写 `$m` / 临时文件路径** | 工具内部已处理：写临时宏文件 → `$m "路径"` → `RunInPdms()`。你只需传脚本内容 |
| **不要手动设 CE** | `RealE3DEnvironment` 订阅 `CurrentElementChanged` 自动跟踪。手动 `$!elementName` 会触发事件链干扰 |
| **坐标成员必须是全称** | `!pos.East` / `!pos.North` / `!pos.Up`，**没有** `.E` / `.N` / `.U` |
| **不要靠 `execute_pml` 读属性** | 读单个属性用 `get_attributes`，查清单用 `query`。PML 读属性慢且易错 |
| **超时** | 内层 30s + 外层 60s。脚本里有大循环（>500 次遍历）要分批 |

---

## 二、写之前先查：`knowledge/pml/` 索引

用 `read_file("knowledge/pml/<文件>.md")` 加载。**每个文件都含来自 33 个真实 PML 工具的代码**。

| 文件 | 行数 | 什么时候读 |
|------|:----:|-----------|
| `commands.md` | 251 | **集合查询、DO 循环、DB 操作**——最常用，先读这个 |
| `expressions.md` | 80 | 变量作用域搞不清时（`!var` / `!!var` / `$!var` / `$P var`） |
| `functions.md` | 58 | 内置函数（`Matchwild` / `MATCH` / `TypeOf` / 字符串） |
| `objects_core.md` | 331 | STRING / ARRAY / FILE / DBREF / ALERT 对象方法 |
| `objects_geometry.md` | 283 | POSITION / ORIENTATION / DIRECTION / LINE / PLANE |
| `type_conversion.md` | 95 | STRING↔REAL↔BOOLEAN 转换 |
| `objects_system.md` | 284 | SESSION / MDB / FMSYS / PROJECT |
| `objects_ui.md` | 389 | FORM / MENU / MACRO / REPORT |
| `forms.md` | 112 | Form 定义（FRAME / TABSET / Button / 布局） |
| `objects_grid.md` | 123 | PLANTGRID / RADIALGRID / LINEARGRID |
| `tools_index.md` | 131 | 按工具名反查编码模式 |

## 三、照抄范式：`knowledge/patterns/`

用 `read_file("knowledge/patterns/<文件>.md")` 加载。**这些是真实工具验证过的代码，直接改参数用**。

| 目标 | 文件 |
|------|------|
| 集合查询 | `collection_query.md`、`query_elements.md` |
| 元素导航（bran of / zone of / owner / next） | `element_navigation.md` |
| 按类型分支处理 | `element_type_dispatch.md` |
| **读写属性**（最关键，`.Dbref().:ATTR`） | `modify_attributes.md` |
| 存在性检查（EXIST + HANDLE） | `check_exists.md` |
| 元素重命名 | `rename_elements.md` |
| 几何操作（位置/方向/距离） | `geometry_operations.md` |
| 文件读写（CSV / Excel） | `file_io.md` |
| List/Grid 控件 | `list_grid_operations.md` |
| 错误处理（HANDLE / ON ERROR） | `error_handling.md` |
| .NET 集成（import DLL / PmlNetCall） | `net_integration.md` |
| 虚拟连接 | `virtual_conn.md` |

## 四、按目标查

| 我要写… | 先读 | 再照抄 |
|---------|------|--------|
| 集合查询 | `pml/commands.md` 集合查询节 | `patterns/collection_query.md` |
| 遍历元素 | `pml/commands.md` 循环节 | `patterns/element_navigation.md` |
| 创建/删除元素 | `pml/commands.md` DB 操作节 | — |
| 读写属性 | `pml/objects_core.md` → DBREF | `patterns/modify_attributes.md` |
| 几何计算 | `pml/objects_geometry.md` | `patterns/geometry_operations.md` |
| 字符串处理 | `pml/objects_core.md` → STRING | — |
| 类型判断 | `pml/functions.md` → TypeOf | `patterns/element_type_dispatch.md` |
| 看真实工具怎么写的 | `pml/tools_index.md` | 按 # 号查对应范式 |

---

## 五、语法骨架（只作回忆提示，细节必须查文件）

```pml
-- 变量
!local = 'value'                     -- 局部
!!global = 'value'                   -- 全局
$!var                                -- 导航到元素（设 CE）
$P var = DB ELEMENT 'NAME'           -- 引用元素（PML1 风格）

-- 数组（索引从 1 开始，不是 0）
!arr = ARRAY()
!arr.append(!val)
!arr[1]
!arr.size()

-- 集合查询
var !list coll all TYPE for $!scope
var !list coll all (FTUB ELBO BEND) within volume $!vol
var !list append coll all TYPE for $!scope

-- 控制流
DO !val values !array ... ENDDO
DO !i from 1 to N by 1 ... ENDDO
if !flag eq 'TRUEA' then ... else ... endif
handle (n,m) ... endhandle

-- 导航与属性
!bran = bran of $!one
!owner = owner
!next = next
!val.Dbref().:ATTR                   -- 读属性
!ce.Dbref().:ATTR = 'value'          -- 写属性

-- 调试
Q VAR !xxx
$p '文字'
```

---

## 六、报错怎么查

先 `read_file("knowledge/patterns/error_handling.md")`，再对 `knowledge/domain/error_codes.md`。

| 报错现象 | 常见原因 |
|---------|---------|
| 语法错误带行号 | 用了 `if` 小写混搭、漏 `ENDIF`、数组下标从 0 开始 |
| 元素不存在 | NAME 与 UI 显示名不一致 → 先 `query(name='*关键字*')` 查真实 NAME |
| 属性写入无效 | 属性名大小写或类型错误 → `patterns/modify_attributes.md` |
| 循环超时 | 集合过大 → 加 `within volume` 限定范围，或分批 |
| `RunInPdms()` 返回 false | 读 `cmd.Result` 拿真实错误（含行号与错误码） |

> 元素定位失败的完整排查：见 `run_skill(name='e3d-element-ops')`。

---

## 七、什么时候**不**该用 PML

这是 E3D 二开最容易走错的分岔口：

| 场景 | 该用什么 | 为什么 |
|------|---------|--------|
| 读一个属性 | `get_attributes` | 比 PML 快，且不会写错语法 |
| 查元素清单 | `query` | 已封装好 DBURI / 通配符 / CE 跟踪 |
| 改单个元素属性 | `modify` | 已封装 `DbAttribute` 工厂链路 |
| 批量改属性 | `batch` | 内置 preview + 逐条失败不中断 |
| 模型检查 | `check` | 已封装 7 种检查类型 |
| 从图纸建模 | `cad_import` / `design_workflow` | 已封装 PML 生成器 |
| **以上都不覆盖的复杂自定义逻辑** | **`execute_pml`** | 这才是 PML 的正确用途 |

**判断标准：如果某个工具能做，就不要写 PML。** PML 是最后手段，不是首选。
