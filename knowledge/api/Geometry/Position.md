# Position / Direction / Orientation 几何类

**命名空间**: `Aveva.Core.Geometry`（⚠️ 非文档写 `Aveva.Pdms.Maths.Geometry`）
**程序集**: Aveva.Core.Geometry.dll
**用途**: 三维几何计算（坐标点、方向、朝向）

## ⚠️ 类名说明

| 文档写（幻觉） | 真实类名 | 说明 |
|---------------|---------|------|
| `D3Point` | `Position` | 三维坐标点 |
| `D3Vector` | `Direction` | 三维方向向量 |
| `D3Orientation` | `Orientation` | 朝向/旋转 |

## Position 类

三维坐标点。

| 属性 | 类型 | 说明 |
|------|:----:|------|
| East | double | X 坐标（东向） |
| North | double | Y 坐标（北向） |
| Up | double | Z 坐标（高度） |

| 方法 | 签名 | 说明 |
|------|------|------|
| DistanceTo | `double DistanceTo(Position other)` | 到另一点的距离(mm) |
| Midpoint | `Position Midpoint(Position other)` | 与另一点的中点 |

## Direction 类

三维方向向量。

## 典型用法

```csharp
using Aveva.Core.Database;
using Aveva.Core.Geometry;

// 从元素获取位置
DbElement element = DbElement.GetElement("PIPE-001");
Position pos = element.GetPosition(DbAttributeInstance.WorldPosition);
double east = pos.East;
double north = pos.North;
double up = pos.Up;

// 计算两点距离
Position p1 = element1.GetPosition(DbAttributeInstance.WorldPosition);
Position p2 = element2.GetPosition(DbAttributeInstance.WorldPosition);
double dist = p1.DistanceTo(p2);

// 计算中点
Position mid = p1.Midpoint(p2);

// 创建坐标点（用于 SetAttribute）
Position newPos = new Position(1000, 2000, 500);
element.SetAttribute(DbAttributeInstance.WorldPosition, newPos);
```

## PML 中位置计算

```pml
!ce = CURRENT CE
!pos = !ce.WorldPosition
$P 'E=', !pos.East, ' N=', !pos.North, ' U=', !pos.Up

-- 坐标属性（PML 用 E/N/U，不是 East/North/Up）
!east = !ce.Dbref().:PEAST
!north = !ce.Dbref().:PNORT
!up = !ce.Dbref().:PUP
```
