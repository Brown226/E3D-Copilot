# E3D 属性映射表

> 属性名用于 PML `!ele.Dbref().:ATTR` 和 C# `element.GetAsString(DbAttributeInstance.ATTR)`。

## 常用属性

| 属性名 | PML 写法 | C# 常量 | 中文 | 类型 | 说明 |
|--------|----------|---------|------|:----:|------|
| 名称 | `!ele.Name` | `DbAttributeInstance.Name` | 元素名称 | String | 元素的唯一名称 |
| 类型 | `!ele.Dbref().:TYPE` | `DbAttributeInstance.Type` | 元素类型 | String | PIPE/EQUI/STRU 等 |
| 描述 | `!ele.Dbref().:DESC` | `DbAttributeInstance.Description` | 描述 | String | 中文/英文描述 |
| 壁厚 | `!ele.Dbref().:WTHK` | `DbAttributeInstance.WTHK` | 壁厚等级 | String | SCH40/SCH80/STD 等 |
| 公称直径 | `!ele.Dbref().:DIA` | `DbAttributeInstance.Bore` | 公称直径 | Real | DN100=100.0 |
| 外径 | `!ele.Dbref().:OD` | `DbAttributeInstance.OD` | 外径 | Real | 管道实际外径(mm) |
| 规格 | `!ele.Dbref().:SPEC` | `DbAttributeInstance.Spec` | 管道等级 | String | CS150/SS304 等 |
| 流体代码 | `!ele.Dbref().:FLUID` | `DbAttributeInstance.Fluid` | 流体代码 | String | WATER/STEAM/OIL |
| 管道等级 | `!ele.Dbref().:PSPEC` | `DbAttributeInstance.PSpec` | 分支规格 | String | BRAN 的规格 |
| 保温等级 | `!ele.Dbref().:INSU` | `DbAttributeInstance.Insulation` | 保温 | String | 保温材料代码 |
| 房间号 | `!ele.Dbref().:ROOM` | `DbAttributeInstance.Room` | 房间 | String | 所在房间编号 |
| 标高 | `!ele.Dbref().:ELVN` | `DbAttributeInstance.Elevation` | 标高 | Real | 相对标高(mm) |
| 压力等级 | `!ele.Dbref().:PRES` | `DbAttributeInstance.Pressure` | 压力等级 | String | PN10/PN16/CLASS150 |
| 温度等级 | `!ele.Dbref().:TEMP` | `DbAttributeInstance.Temperature` | 温度等级 | String | 温度范围 |
| 位置(X) | `!ele.Dbref().:PEAST` | `DbAttributeInstance.PositionEast` | 东坐标 | Real | WCS 下的 X(mm) |
| 位置(Y) | `!ele.Dbref().:PNORT` | `DbAttributeInstance.PositionNorth` | 北坐标 | Real | WCS 下的 Y(mm) |
| 位置(Z) | `!ele.Dbref().:PUP` | `DbAttributeInstance.PositionUp` | 高度 | Real | WCS 下的 Z(mm) |
| 管嘴朝向 | `!ele.Dbref().:ORIE` | `DbAttributeInstance.Orientation` | 朝向角 | Real | 管嘴方向(度) |
| 创建的 | `!ele.Dbref().:CRED` | `DbAttributeInstance.CreatedBy` | 创建者 | String | 用户名 |
| 创建时间 | `!ele.Dbref().:CRET` | `DbAttributeInstance.CreatedTime` | 创建时间 | Date | 创建时间戳 |
| 修改状态 | `!ele.Dbref().:CHST` | `DbAttributeInstance.ChangeStatus` | 修改状态 | String | NEW/MODIFIED/DELETED |

## C# 中读写属性

```csharp
using Aveva.Core.Database;

DbElement element = DbElement.GetElement("PIPE-001");

// ✅ 读属性（推荐）
string wthk = element.GetAsString(DbAttributeInstance.WTHK);
double bore = element.GetAsDouble(DbAttributeInstance.Bore);

// ✅ 写属性
element.SetAttribute(DbAttributeInstance.WTHK, "SCH40");

// ❌ 常见错误写法（不要用）
// element.GetAttribute("WTHK").ToString();  // 参数类型错误
```

## PML 中读写属性

```pml
!ce = CURRENT CE

-- ✅ 读属性
!wthk = !ce.Dbref().:WTHK
$P '壁厚 = ', !wthk

-- ✅ 写属性
!ce.Dbref().:WTHK = 'SCH40'

-- ✅ 通过名称+属性读
$P !ce.Name, ' :WTHK = ', !ce.Dbref().:WTHK
```

## 焊接属性

| 属性名 | PML 写法 | 说明 |
|--------|----------|------|
| 焊缝编号 | `!ele.Dbref().:WELDNO` | 焊缝编号 |
| 焊缝类型 | `!ele.Dbref().:WELDTYPE` | 焊缝类型 |
| 焊口编号 | `!ele.Dbref().:WJOINT` | 焊接接头编号 |
| 焊缝长度 | `!ele.Dbref().:WLENG` | 焊缝长度(mm) |
| 探伤等级 | `!ele.Dbref().:WNDT` | 无损检测等级 |
