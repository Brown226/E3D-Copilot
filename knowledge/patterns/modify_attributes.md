# 黄金范式：修改属性

**用途**: 批量修改 E3D 元素属性值
**验证**: ✅ 198 次命中，22/33 个真实工具使用

## PML 骨架

```pml
-- 先收集需要修改的元素
VAR !LIST COLL ALL <TYPE> WITH <CONDITION> FOR <SCOPE>

-- 遍历修改属性
DO !ELE values !LIST
    !ELE.Dbref().:<ATTR> = '<NEW_VALUE>'
ENDDO
```

## 真实示例

```pml
-- 修改壁厚
VAR !PIPES COLL ALL PIPE FOR CE
DO !PP values !PIPES
    !PP.Dbref().:WTHK = 'SCH40'
ENDDO

-- 修改规格
DO !ELE values !LIST
    !ELE.Dbref().:SPEC = 'CS150'
ENDDO
```

## C# 等价实现

```csharp
using Aveva.Core.Database;

// 单个修改
DbElement element = DbElement.GetElement("PIPE-001");
element.SetAttribute(DbAttributeInstance.WTHK, "SCH40");

// 批量修改（配合 PML 查询后再用 C# 修改）
var ce = CurrentElement.Element;
var cmd = Command.CreateCommand("VAR !LIST COLL ALL PIPE FOR $!ce");
cmd.RunInPdms();
// 然后在 C# 中遍历 !LIST 执行 SetAttribute
```

## 注意事项

- 批量修改前务必用 `ask_user(risk_level="write_batch")` 获取用户确认
- 考虑使用 `DbTransaction.Begin()/Commit()` 保证原子性
