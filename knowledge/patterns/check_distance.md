# 黄金范式：距离计算

**用途**: 计算两个元素之间的距离
**验证**: ✅ 来自 distancecheck 真实工具
**preferred_tool**: `geometry` + `calculate` — 先 geometry 取坐标，再 calculate 算距离

## PML 骨架

```pml
-- 方法1：使用 DISTANCE 函数
!dist = DISTANCE($!ELE1, $!ELE2)
$P '距离 = ', !dist, ' mm'

-- 方法2：使用位置对象计算
!pos1 = !ELE1.WorldPosition
!pos2 = !ELE2.WorldPosition
!dx = !pos1.East - !pos2.East
!dy = !pos1.North - !pos2.North
!dz = !pos1.Up - !pos2.Up
!dist = SQR(!dx * !dx + !dy * !dy + !dz * !dz)
$P '距离 = ', !dist, ' mm'
```

## C# 等价实现

```csharp
using Aveva.Core.Database;
using Aveva.Core.Geometry;

DbElement element1 = DbElement.GetElement("PIPE-001");
DbElement element2 = DbElement.GetElement("STRU-001");

Position pos1 = element1.GetPosition(DbAttributeInstance.WorldPosition);
Position pos2 = element2.GetPosition(DbAttributeInstance.WorldPosition);

double distance = pos1.DistanceTo(pos2);  // 单位 mm
Console.WriteLine($"距离 = {distance} mm");

// 同时获取各轴分量
double dx = Math.Abs(pos1.East - pos2.East);
double dy = Math.Abs(pos1.North - pos2.North);
double dz = Math.Abs(pos1.Up - pos2.Up);
```
