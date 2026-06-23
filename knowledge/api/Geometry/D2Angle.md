# D2Angle 角度类

**命名空间**: `Aveva.Core.Geometry`
**用途**: 角度计算，支持弧度和度转换

## 关键方法

| 方法 | 签名 | 说明 |
|------|------|------|
| CreateDegrees | `static D2Angle CreateDegrees(double degrees)` | 从度数创建 |
| CreateRadians | `static D2Angle CreateRadians(double radians)` | 从弧度创建 |
| Degrees | `double Degrees()` | 获取度数 |
| Radians | `double Radians()` | 获取弧度数 |
| Sin | `double Sin()` | 正弦值 |
| Cos | `double Cos()` | 余弦值 |
| Tan | `double Tan()` | 正切值 |
| Normalize | `D2Angle Normalize()` | 标准化（0~2π） |

## 典型用法

```csharp
using Aveva.Core.Geometry;

// 创建角度
D2Angle angle1 = D2Angle.CreateDegrees(90);     // 90度
D2Angle angle2 = D2Angle.CreateRadians(Math.PI); // π弧度

// 转换
double deg = angle1.Degrees();   // 90.0
double rad = angle1.Radians();   // π/2

// 三角函数
double sinVal = angle1.Sin();    // 1.0
double cosVal = angle1.Cos();    // 0.0

// 运算
D2Angle sum = angle1 + angle2;
D2Angle normalized = angle1.Normalize();
```
