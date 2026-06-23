# E3D 坐标系统说明

> 用于 PML 位置/距离计算和 C# Geometry API。

## 世界坐标系 (WCS — World Coordinate System)

E3D 使用右手笛卡尔坐标系：

| 轴 | PML 属性 | C# 属性 | 说明 |
|:--:|----------|---------|------|
| X | `:PEAST` / `pos.E` | `Position.East` | 东向 (East) |
| Y | `:PNORT` / `pos.N` | `Position.North` | 北向 (North) |
| Z | `:PUP` / `pos.U` | `Position.Up` | 高度 (Up) |

> ⚠️ **注意**：PML 中坐标轴缩写是 **`E`/`N`/`U`**，不是 `East`/`North`/`Up`。但在 `GetPosition()` 返回的 `Position` 对象中，属性名是 `.East`/`.North`/`.Up`。

## C# 中读取位置

```csharp
using Aveva.Core.Database;
using Aveva.Core.Geometry;

// ✅ 推荐：通过 WorldPosition 属性读取
DbElement element = DbElement.GetElement("PIPE-001");
Position pos = element.GetPosition(DbAttributeInstance.WorldPosition);
double east = pos.East;    // X
double north = pos.North;  // Y
double up = pos.Up;        // Z

// ✅ 通过 DbAttribute 读取坐标值
double peast = element.GetAsDouble(DbAttributeInstance.PositionEast);
double pnorth = element.GetAsDouble(DbAttributeInstance.PositionNorth);
double pup = element.GetAsDouble(DbAttributeInstance.PositionUp);
```

## PML 中读取位置

```pml
!ce = CURRENT CE
!pos = !ce.WorldPosition  -- 返回 Position 对象

$P 'E=', !pos.East, ' N=', !pos.North, ' U=', !pos.Up

-- 或者通过属性读取
$P 'E=', !ce.Dbref().:PEAST
$P 'N=', !ce.Dbref().:PNORT
$P 'U=', !ce.Dbref().:PUP
```

## 关键 Geometry 类

| C# 类 | 命名空间 | 说明 |
|-------|---------|------|
| `Position` | `Aveva.Core.Geometry` | 三维坐标点，属性: East/North/Up |
| `Direction` | `Aveva.Core.Geometry` | 三维方向向量 |
| `Orientation` | `Aveva.Core.Geometry` | 朝向/旋转 |
| `D2Angle` | `Aveva.Core.Geometry` | 角度（弧度/度转换） |

> ⚠️ 文档中写的 `D3Point`/`D3Vector` 类名不存在，真实类是 `Position`/`Direction`/`Orientation`。

## 位置距离计算示例

```csharp
using Aveva.Core.Geometry;

// 计算两点距离
Position p1 = element1.GetPosition(DbAttributeInstance.WorldPosition);
Position p2 = element2.GetPosition(DbAttributeInstance.WorldPosition);
double distance = p1.DistanceTo(p2);  // 返回 mm

// 计算中点
Position midpoint = new Position(
    (p1.East + p2.East) / 2,
    (p1.North + p2.North) / 2,
    (p1.Up + p2.Up) / 2
);
```

```pml
!pos1 = !ele1.WorldPosition
!pos2 = !ele2.WorldPosition

-- PML 中计算距离
!dx = !pos1.East - !pos2.East
!dy = !pos1.North - !pos2.North
!dz = !pos1.Up - !pos2.Up
!dist = SQR(!dx * !dx + !dy * !dy + !dz * !dz)
$P '距离 = ', !dist, ' mm'
```
