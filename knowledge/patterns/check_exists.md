# 黄金范式：存在检查

**用途**: 检查指定名称的元素是否存在
**验证**: ✅ 24 次命中，7/33 个真实工具使用

## PML 骨架

```pml
-- 检查元素是否存在
VAR !FLAG EXIST $!ELEMENT_NAME
IF !FLAG THEN
    $P '元素存在：', !ELEMENT_NAME
ELSE
    $P '元素不存在：', !ELEMENT_NAME
ENDIF
```

## C# 等价实现

```csharp
using Aveva.Core.Database;

// 按名称查找，检查是否有效
DbElement element = DbElement.GetElement("PIPE-001");
if (element.IsValid)
{
    // 元素存在
}
else
{
    // 元素不存在
}
```

## 组合使用

```pml
-- 先检查存在，再决定是否查询
VAR !FLAG EXIST $!pipeName
IF !FLAG THEN
    VAR !LIST COLL ALL PIPE WITH Matchwild(NAME, $!pipeName, true) FOR CE
    DO !ELE values !LIST
        $P !ELE.Name, ' :WTHK = ', !ELE.Dbref().:WTHK
    ENDDO
ELSE
    $P '管道 ', $!pipeName, ' 不存在'
ENDIF
```
