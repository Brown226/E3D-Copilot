# D2Arc — 二维弧线类

**命名空间**: `Aveva.Core.Geometry`
**用途**: 表示二维平面上的圆弧

## 关键属性

| 属性 | 类型 | 说明 |
|------|:----:|------|
| Centre | D2Position | 圆心 |
| Radius | double | 半径 |
| StartAngle | D2Angle | 起始角度 |
| EndAngle | D2Angle | 结束角度 |
| Start | D2Position | 起点坐标 |
| End | D2Position | 终点坐标 |
| Length | double | 弧长 |
| Height | double | 弧高 |
| Bulge | double | 凸度 |

## 关键方法

| 方法 | 签名 | 说明 |
|------|------|------|
| Create | `static D2Arc Create(D2Position centre, double radius, D2Angle start, D2Angle end)` | 从圆心+角度创建 |
| From3Points | `static D2Arc From3Points(D2Position p1, D2Position p2, D2Position p3)` | 从三点创建弧 |
| Clockwise | `bool Clockwise()` | 是否为顺时针 |
| IsCircle | `bool IsCircle()` | 是否为完整圆 |
| Intersects | `bool Intersects(D2Line line)` | 检测与直线相交 |
| MoveBy | `void MoveBy(D2Vector offset)` | 平移 |

## 典型用法

```csharp
using Aveva.Core.Geometry;

// 从圆心和角度创建
D2Position centre = new D2Position(0, 0);
D2Arc arc = D2Arc.Create(centre, 100, 
    D2Angle.CreateDegrees(0), 
    D2Angle.CreateDegrees(90));

// 查询弧属性
Console.WriteLine($"弧长: {arc.Length}");
Console.WriteLine($"起点: ({arc.Start.X}, {arc.Start.Y})");
Console.WriteLine($"终点: ({arc.End.X}, {arc.End.Y})");

// 从三点创建
D2Position p1 = new D2Position(0, 0);
D2Position p2 = new D2Position(100, 50);
D2Position p3 = new D2Position(200, 0);
D2Arc arcFromPoints = D2Arc.From3Points(p1, p2, p3);
```
