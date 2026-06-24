# 黄金范式：查询元素

**用途**: 按类型+名称条件查询 E3D 元素
**验证**: ✅ 263 次命中，21/33 个真实工具使用
**preferred_tool**: `query` — 优先用 query 工具，不要手写 execute_pml 的 coll all

## PML 骨架

```pml
-- 基本查询：收集指定类型的所有元素
VAR !LIST COLL ALL <TYPE> FOR <SCOPE>

-- 带名称匹配查询
VAR !LIST COLL ALL <TYPE> WITH Matchwild(NAME, '<PATTERN>', true) FOR <SCOPE>

-- 或使用 MATCH 函数（真实工具中更常用）
VAR !LIST COLL ALL <TYPE> WITH MATCH(NAME,|<PATTERN>|) neq 0 FOR <SCOPE>

-- 遍历输出
DO !ELE values !LIST
    $P !ELE.Name, ' = ', !ELE.Dbref().:<ATTR>
ENDDO
```

## 真实示例

```pml
-- 来自 VirtualConn.pmlfrm
VAR !MLIST COLL ALL HOLE WITH MATCH(NAME,|$!this.param1txt|) neq 0 FOR $!this.cename

-- 查询当前元素下所有 PIPE
VAR !PIPES COLL ALL PIPE FOR $!this.cename

-- 查询所有 BRAN
VAR !BRANS COLL ALL BRAN FOR $!this.cename

-- 查询 EQUI
VAR !EQUIS COLL ALL EQUI FOR $!this.cename
```

## C# 等价实现

```csharp
// C# 中查询元素通常通过 PML 执行，而非 C# 直查
// 因为 C# 没有便捷的 "coll all" 等价方法
var cmd = Command.CreateCommand("VAR !LIST COLL ALL PIPE WITH Matchwild(NAME, '*DN100*', true) FOR $!ce");
cmd.RunInPdms();
```

## 参数说明

| 占位符 | 说明 |
|--------|------|
| `<TYPE>` | 元素类型：PIPE/BRAN/EQUI/STRU/ZONE/HOLE/VALV/NOZZ |
| `<PATTERN>` | 通配符模式，如 `*DN100*` |
| `<SCOPE>` | 范围：`CE` 或 `$!this.cename` |
| `<ATTR>` | 属性名：WTHK/SPEC/FLUID/DIA 等 |
