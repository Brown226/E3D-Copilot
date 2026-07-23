---
name: e3d-piping-design
description: E3D 管道设计操作指南 — 管道创建、修改、查询的标准流程和安全注意事项。涉及管道操作时调用此 skill。
runAs: inline
tags: [管道, PML, 设计, PIPE, 修改]
---

# E3D 管道设计操作指南

> 管道操作属于 L2-L3 安全级别（写入/批量），执行前必须确认。
> 详细 PML 语法参考：`read_file("knowledge/patterns/modify_attributes.md")`

---

## 一、管道查询（L0 只读，自动执行）

### 按名称/类型查询
```pml
-- 查询当前元素下的所有管道
var !pipes coll all PIPE for $!ce

-- 按通配符查询（如所有 DN100 管道）
var !pipes coll all PIPE with Matchwild(name, '*DN100*')

-- 按区域查询
var !pipes coll all PIPE for /MDS/PIPING
```

### 读取管道属性
```pml
-- 正确链路：DbAttribute 工厂 + GetAsString
-- C# 侧：DbElement.GetAsString(DbAttribute.GetDbAttribute("WTHK"))
-- PML 侧：
var !elem ref $!name
var !bore !elem.Bore
var !wthk !elem.Wthk
```

参考：`read_file("knowledge/patterns/query_elements.md")`

---

## 二、管道属性修改（L2 写入，需确认）

### 单个修改
```pml
-- 修改壁厚
var !elem ref $!name
!elem.Wthk = 'SCH40'

-- 修改保温层厚度
!elem.Insu = '50'
```

### 安全规则
1. 修改前必须先读取旧值（供回滚）
2. 单次修改 ≤ 5 个属性可直接执行
3. 修改后验证：重新读取确认生效

参考：`read_file("knowledge/patterns/modify_attributes.md")`

---

## 三、批量修改（L3 批量，需确认 + 预览）

### 流程（必须遵守）
```
1. 查询目标集合 → 展示给用户确认范围
2. 生成修改计划（表格：元素名 | 属性 | 旧值 → 新值）
3. 用户审批
4. 执行修改（带 handle 异常处理）
5. 验证结果
```

### PML 批量修改模板
```pml
var !pipes coll all PIPE with Matchwild(name, '*DN100*')
var !count 0
DO !pipe values !pipes
    handle (code)
        -- 错误处理：记录失败项，继续下一个
    endhandle
    !pipe.Wthk = 'SCH40'
    var !count (!count + 1)
ENDDO
-- 输出: 成功修改 $!count 个管道
```

参考：`read_file("knowledge/patterns/collection_query.md")`

---

## 四、管道创建（L2 写入，需确认）

### 创建管道
```pml
-- 在指定 zone 下创建管道
var !zone ref /MDS/PIPING
$!zone
NEW PIPE
name 'PIPE-NEW-001'
purpose 'PROCESS'
bore '100'
```

### 注意事项
- 创建前检查同名元素是否已存在（`exist $!name`）
- 必须在正确的 zone/branch 下创建
- 创建后设置必要属性（bore/purpose/spec）

参考：`read_file("knowledge/patterns/check_exists.md")`

---

## 五、致命禁区

| 错误 | 正确 | 原因 |
|------|------|------|
| `pos.E / pos.N / pos.U` | `!pos.East / !pos.North / !pos.Up` | PML 2 成员是全称 |
| `Database.GetElement("X")` | `DbElement.GetElement("X")` | 方法在 DbElement 上 |
| `elem.GetAttribute("WTHK")` | `elem.GetAsString(DbAttribute.GetDbAttribute("WTHK"))` | 参数是 DbAttribute 对象 |
| 批量修改不预览 | 先展示计划再执行 | L3 安全要求 |
