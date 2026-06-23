# DbAttribute / DbAttributeInstance 属性类

**命名空间**: `Aveva.Core.Database`（⚠️ 非 `Aveva.Pdms.Database`）

## DbAttribute 类

表示属性定义（元数据）。

| 成员 | 签名 | 说明 |
|------|------|------|
| Name | `string Name { get; }` | 属性名（如 "WTHK"） |
| Type | `DbAttributeType Type { get; }` | 属性类型 |
| GetDbAttribute | `static DbAttribute GetDbAttribute(string name)` | 按名称查找属性 |

## DbAttributeInstance 静态类

包含所有系统预定义属性实例，**这是访问属性的主要方式**。

### 常用属性

| 常量 | 对应 PML | 类型 | 说明 |
|------|----------|:----:|------|
| `DbAttributeInstance.Name` | `:NAME` | String | 元素名称 |
| `DbAttributeInstance.Type` | `:TYPE` | String | 元素类型 |
| `DbAttributeInstance.Description` | `:DESC` | String | 描述 |
| `DbAttributeInstance.WTHK` | `:WTHK` | String | 壁厚等级 |
| `DbAttributeInstance.Bore` | `:DIA` | Real | 公称直径 |
| `DbAttributeInstance.Spec` | `:SPEC` | String | 管道等级 |
| `DbAttributeInstance.PSpec` | `:PSPEC` | String | 分支规格 |
| `DbAttributeInstance.Fluid` | `:FLUID` | String | 流体代码 |
| `DbAttributeInstance.WorldPosition` | `WORPOS` | Position | 世界坐标位置 |
| `DbAttributeInstance.PositionEast` | `:PEAST` | Real | 东坐标 |
| `DbAttributeInstance.PositionNorth` | `:PNORT` | Real | 北坐标 |
| `DbAttributeInstance.PositionUp` | `:PUP` | Real | 高度 |
| `DbAttributeInstance.Orientation` | `:ORIE` | Real | 朝向角 |
| `DbAttributeInstance.Room` | `:ROOM` | String | 房间号 |
| `DbAttributeInstance.Insulation` | `:INSU` | String | 保温等级 |
| `DbAttributeInstance.ChangeStatus` | `:CHST` | String | 修改状态 |

## ✅ 正确用法

```csharp
using Aveva.Core.Database;

DbElement element = DbElement.GetElement("PIPE-001");

// 通过 DbAttributeInstance 访问属性
string wthk = element.GetAsString(DbAttributeInstance.WTHK);
string spec = element.GetAsString(DbAttributeInstance.Spec);
string fluid = element.GetAsString(DbAttributeInstance.Fluid);
double bore = element.GetAsDouble(DbAttributeInstance.Bore);

// 按名称动态查找属性
DbAttribute attr = DbAttribute.GetDbAttribute("WTHK");
string val = element.GetAsString(attr);
```

## ❌ 常见错误

```csharp
// ❌ 错误：字符串参数
element.GetAttribute("WTHK");  // 应该用 DbAttributeInstance.WTHK
element.GetAttribute("WTHK").ToString();  // 类型不匹配

// ✅ 正确
element.GetAsString(DbAttributeInstance.WTHK);
```
