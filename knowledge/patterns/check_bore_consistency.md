# 黄金范式：管径一致性检查

**用途**: 检查管道内各管件的管径是否一致
**验证**: ✅ 来自真实 EquiCheck 工具
**preferred_tool**: `query` + `get_attributes` — query 查管件，get_attributes 读管径

## PML 骨架

```pml
-- 收集管件
VAR !FTUBS COLL ALL FTUB FOR $!this.cename
VAR !BENDS COLL ALL BEND FOR $!this.cename
VAR !TEES COLL ALL TEE FOR $!this.cename

-- 检查管径
DO !FT values !FTUBS
    !BORE = !FT.Dbref().:DIA
    IF !BORE NE <EXPECTED_BORE> THEN
        $P '管径不一致：', !FT.Name, ' DIA=', !BORE
    ENDIF
ENDDO
```

## C# 等价实现

```csharp
using Aveva.Core.Database;

// 获取管道下的所有分支和管件
DbElement pipe = DbElement.GetElement("PIPE-001");
double expectedBore = pipe.GetAsDouble(DbAttributeInstance.Bore);

DbElement child = pipe.FirstMember();
while (child.IsValid)
{
    string type = child.GetAsString(DbAttributeInstance.Type);
    if (type == "FTUB" || type == "BEND" || type == "TEE")
    {
        double bore = child.GetAsDouble(DbAttributeInstance.Bore);
        if (Math.Abs(bore - expectedBore) > 0.01)
        {
            Console.WriteLine($"管径不一致: {child.Name}, 期望={expectedBore}, 实际={bore}");
        }
    }
    child = pipe.NextMember(child);
}
```
