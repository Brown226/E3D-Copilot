# Direction — 方向向量类

**命名空间**: `Aveva.Core.Geometry`（⚠️ 非 `D3Vector`）
**程序集**: Aveva.Core.Geometry.dll
**用途**: 表示三维空间中的方向向量

## 关键属性

| 属性 | 类型 | 说明 |
|------|:----:|------|
| X | double | X 分量 |
| Y | double | Y 分量 |
| Z | double | Z 分量 |
| Length | double | 向量长度 |

## 关键方法

| 方法 | 签名 | 说明 |
|------|------|------|
| Normalize | `Direction Normalize()` | 归一化 |
| Dot | `double Dot(Direction other)` | 点积 |
| Cross | `Direction Cross(Direction other)` | 叉积 |
| Length | `double Length()` | 向量长度 |

## 典型用法

```csharp
using Aveva.Core.Geometry;

// 创建方向向量
Direction dir = new Direction(1, 0, 0);  // X 方向

// 向量运算
Direction dir2 = new Direction(0, 1, 0);
double dot = dir.Dot(dir2);        // 点积
Direction cross = dir.Cross(dir2); // 叉积
double len = dir.Length();         // 长度

// 归一化
Direction normalized = dir.Normalize();

// Position 和 Direction 的关系
Position p1 = element1.GetPosition(DbAttributeInstance.WorldPosition);
Position p2 = element2.GetPosition(DbAttributeInstance.WorldPosition);

// 从两点计算方向
double dx = p2.East - p1.East;
double dy = p2.North - p1.North;
double dz = p2.Up - p1.Up;
Direction dir3 = new Direction(dx, dy, dz);
```

## ⚠️ 注意事项

- 真实类名是 `Direction`，不是 `D3Vector` 或 `Vector3`
- 命名空间是 `Aveva.Core.Geometry`，不是 `Aveva.Pdms.Maths.Geometry`
