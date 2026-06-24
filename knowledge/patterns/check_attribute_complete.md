# 黄金范式：属性完整性检查

**用途**: 检查元素是否缺少必要属性
**验证**: ✅ 来自 modelCheck 等真实工具
**preferred_tool**: `check(type=attribute)` + `get_attributes`

## PML 骨架

```pml
-- 收集元素
VAR !LIST COLL ALL <TYPE> FOR <SCOPE>

-- 检查属性完整性
DO !ELE values !LIST
    !ATTR1 = !ELE.Dbref().:<ATTR1>
    !ATTR2 = !ELE.Dbref().:<ATTR2>
    
    -- 属性为空或未设置则报出
    IF Unset(!ATTR1) OR !ATTR1 EQ '' THEN
        $P '缺少属性 <ATTR1>: ', !ELE.Name
    ENDIF
    IF Unset(!ATTR2) OR !ATTR2 EQ '' THEN
        $P '缺少属性 <ATTR2>: ', !ELE.Name
    ENDIF
ENDDO
```

## C# 等价实现

```csharp
using Aveva.Core.Database;

// 检查元素的指定属性是否为空
bool HasAttribute(DbElement element, DbAttribute attr)
{
    if (!element.IsValid) return false;
    string val = element.GetAsString(attr);
    return !string.IsNullOrEmpty(val);
}

// 批量检查
DbElement ce = CurrentElement.Element;
DbElement child = ce.FirstMember();
while (child.IsValid)
{
    if (!HasAttribute(child, DbAttributeInstance.Spec))
        Console.WriteLine($"缺少属性 SPEC: {child.Name}");
    child = ce.NextMember(child);
}
```
