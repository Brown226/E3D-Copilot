# Orientation — 朝向/姿态类

**命名空间**: `Aveva.Core.Geometry`
**程序集**: Aveva.Core.Geometry.dll
**用途**: 表示三维空间中的朝向/旋转（旋转矩阵）

## 关键方法

| 方法 | 签名 | 说明 |
|------|------|------|
| CreateFromEulerAngles | `static Orientation CreateFromEulerAngles(double roll, double pitch, double yaw)` | 从欧拉角创建 |
| RotateVector | `Direction RotateVector(Direction vector)` | 旋转向量 |
| Identity | `static Orientation Identity { get; }` | 单位朝向 |

## 运算符

| 运算符 | 说明 |
|--------|------|
| `*` | 组合两个朝向 |

## 典型用法

```csharp
using Aveva.Core.Geometry;

// 默认朝向
Orientation orient = new Orientation();

// 从欧拉角创建
Orientation orient2 = Orientation.CreateFromEulerAngles(
    Math.PI / 2,  // roll
    0,            // pitch
    0             // yaw
);

// 旋转向量
Direction dir = new Direction(1, 0, 0);
Direction rotated = orient2.RotateVector(dir);

// 组合朝向
Orientation combined = orient1 * orient2;
```

## 坐标变换

```csharp
// 局部坐标 → 全局坐标
Position localPos = new Position(x, y, z);
Orientation localOrient = new Orientation();

Direction localDir = new Direction(localPos.East, localPos.North, localPos.Up);
Direction globalDir = localOrient.RotateVector(localDir);

Position globalPos = new Position(
    origin.East + globalDir.X,
    origin.North + globalDir.Y,
    origin.Up + globalDir.Z
);
```
