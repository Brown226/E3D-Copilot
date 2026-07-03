---
name: aveva-pml-language
description: PML 语言索引指南 — 快速定位语法和编码模式。需要写 PML 时先调用此 skill 获得文件指引，然后用 read_file 加载详细信息。
runAs: inline
tags: [PML, E3D, 语法, 参考]
---

# PML 语言索引指南

> 通过此文档快速定位 PML 语法位置和编码模式。详细内容在 `knowledge/pml/` 和 `knowledge/patterns/` 中。
> 使用方法：`read_file("knowledge/pml/文件名.md")` 加载完整内容。

---

## 一、PML 语言基础（`knowledge/pml/`）

| 文件 | 内容 | 关键信息 |
|------|------|----------|
| `expressions.md` | 变量、运算符、表达式语法 | `!var` / `!!var` / `$!var` / `$P var` 区别 |
| `commands.md` | 集合查询、控制流、DB 操作命令 | `coll all` / `DO` / `IF` / `CREATE` / `NEW` |
| `functions.md` | 内置函数速查 | `Matchwild()` / `MATCH()` / `TypeOf()` / 字符串方法 |
| `type_conversion.md` | 类型转换 | STRING↔REAL↔BOOLEAN |
| `objects_core.md` | 核心对象参考 | STRING / ARRAY / FILE / DBREF / ALERT 等 |
| `objects_geometry.md` | 几何对象 | POSITION / ORIENTATION / DIRECTION / LINE / PLANE |
| `objects_grid.md` | 网格对象 | PLANTGRID / RADIALGRID / LINEARGRID |
| `objects_system.md` | 系统对象 | SESSION / MDB / FMSYS / PROJECT |
| `objects_ui.md` | UI 对象 | FORM / MENU / MACRO / REPORT |
| `forms.md` | Form 定义详解 | FRAME / TABSET / List / Button / 布局 |
| `tools_index.md` | 33 个真实 PML 工具索引 | **最重要的文件**——按工具找编码模式 |

---

## 二、黄金范式（`knowledge/patterns/`）

每个模式文件都包含可复用的 PML 代码片，来自真实工具验证。

| 文件 | 用途 |
|------|------|
| `collection_query.md` | 集合查询（对象式 `COLLECTION()` 和 `coll all` 两种方式） |
| `query_elements.md` | 按类型/名称/通配符查询元素 |
| `element_navigation.md` | 元素导航：`bran of` / `zone of` / `suppo of` / `owner` / `mem` |
| `element_type_dispatch.md` | 类型分发：`type eq` / `type inset()` / 按类型分支处理 |
| `modify_attributes.md` | 属性读写：`.Dbref().:ATTR` / `PARA[]` / `SPREF` |
| `check_exists.md` | 存在性检查：`EXIST` / `exists` / `FIRST` + `HANDLE` |
| `check_attribute_complete.md` | 属性完整性检查 |
| `check_bore_consistency.md` | 通径一致性检查 |
| `check_distance.md` | 距离检查 |
| `custom_attributes.md` | 自定义属性读写 |
| `geometry_operations.md` | 几何操作：位置/方向/距离 |
| `export_report.md` | 导出和报表 |
| `file_io.md` | 文件读写：CSV / Excel 导入导出 |
| `list_grid_operations.md` | List/Grid 控件操作：`.dtext` / `.setRows()` / `.selection()` |
| `rename_elements.md` | 元素重命名 |
| `virtual_conn.md` | 虚拟连接 |
| `error_handling.md` | 错误处理：`HANDLE` / `ON ERROR` |
| `net_integration.md` | .NET 集成：`import` DLL / `PmlNetCall` |

---

## 三、快速定位指南

### 我要写... → 看这里

| 目标 | 先读此文件 | 再参考范式 |
|------|-----------|-----------|
| 集合查询 | `knowledge/pml/commands.md` → 集合查询节 | `knowledge/patterns/collection_query.md` |
| 遍历元素 | `knowledge/pml/commands.md` → 循环节 | `knowledge/patterns/element_navigation.md` |
| 创建/删除元素 | `knowledge/pml/commands.md` → DB 操作节 | — |
| 读写属性 | `knowledge/pml/objects_core.md` → DBREF | `knowledge/patterns/modify_attributes.md` |
| Form 表单 | `knowledge/pml/forms.md` | `knowledge/patterns/list_grid_operations.md` |
| 文件/CSV 操作 | `knowledge/pml/objects_core.md` → FILE | `knowledge/patterns/file_io.md` |
| .NET DLL 调用 | `knowledge/pml/commands.md` → 外部调用 | `knowledge/patterns/net_integration.md` |
| 字符串处理 | `knowledge/pml/objects_core.md` → STRING | — |
| 几何计算 | `knowledge/pml/objects_geometry.md` | `knowledge/patterns/geometry_operations.md` |
| 看真实工具代码 | `knowledge/pml/tools_index.md` | 按 # 号查对应范式 |

### 常用 PML 语法速查

```pml
-- 变量
!local = 'value'                     -- PML2 局部变量
!!global = 'value'                   -- PML2 全局变量
$!var                                -- 导航到元素（设 CE）
$P var = DB ELEMENT 'NAME'           -- PML1 引用元素

-- 数组
!arr = ARRAY()
!arr.append(!val)                    -- 追加
!arr[1]                              -- 索引（从1开始）
!arr.size()                          -- 长度

-- 集合查询
var !list coll all TYPE for $!scope               -- 基本查询
var !list coll all (A B C) within volume $!x      -- 多类型空间查询
var !list append coll all TYPE for $!scope         -- 追加集合

-- 控制流
DO !val values !array ... ENDDO                   -- 遍历数组
DO !i index !array ... ENDDO                      -- 索引循环
DO !i from 1 to N by 1 ... ENDDO                  -- 计数循环
if !flag eq 'TRUEA' then ... else ... endif       -- 条件
handle (n,m) ... endhandle                        -- 错误捕获

-- DB 操作
!bran = bran of $!one                             -- 获取 BRAN
!zone = zone of $!one                             -- 获取 ZONE
!owner = owner                                    -- 父元素
!pre = pre / !next = next                         -- 兄弟元素
!val.Dbref().:ATTR                                -- 读属性
!ce.Dbref().:ATTR = 'value'                       -- 写属性

-- 元素创建
$P new = NEW TYPE parent                          -- 标准类型
CREATE $P new TYPE FTUB REF DB ELEMENT 'PARENT'   -- 特殊类型

-- 调试输出
Q VAR !xxx                                        -- 快速变量查看
$p '文字'                                          -- 控制台输出
```
