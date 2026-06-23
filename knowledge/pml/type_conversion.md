# C# ↔ PML 类型与转换速查

> E3D 开发中常见的类型转换场景

## C# 类型 → PML 类型

| C# 类型 | PML 类型 | 赋值示例 |
|---------|----------|---------|
| `string` | STRING | `element.SetAttribute(DbAttributeInstance.WTHK, "SCH40")` |
| `double` | REAL | `element.SetAttribute(DbAttributeInstance.Bore, 100.0)` |
| `int` | REAL | `element.SetAttribute(DbAttributeInstance.PositionEast, 1000)` |
| `bool` | BOOLEAN | PML 中用 `TRUE`/`FALSE`，C# 用 `1`/`0` |
| `Position` | POSITION | `element.SetAttribute(DbAttributeInstance.WorldPosition, pos)` |
| `Direction` | DIRECTION | 方向向量 |
| `string[]` | ARRAY | PML 数组，在 C# 中通常通过 PML 执行 |

## PML 类型 → C# 类型

| PML 读取 | C# 方法 | 返回类型 |
|----------|---------|:--------:|
| `!ele.Dbref().:WTHK` | `element.GetAsString(attr)` | `string` |
| `!ele.Dbref().:DIA` | `element.GetAsDouble(attr)` | `double` |
| `!ele.Dbref().:PEAST` | `element.GetAsDouble(attr)` | `double` |
| `!ele.WorldPosition` | `element.GetPosition(attr)` | `Position` |
| `!ele.Name` | `element.Name` | `string` |

## C# 常用属性常量速查

| DbAttributeInstance | 类型 | 说明 |
|--------------------|:----:|------|
| `.Name` | String | 名称 |
| `.Type` | String | 类型 |
| `.Description` | String | 描述 |
| `.WTHK` | String | 壁厚等级 |
| `.Bore` | Real | 公称直径 |
| `.Spec` | String | 管道等级 |
| `.PSpec` | String | 分支规格 |
| `.Fluid` | String | 流体代码 |
| `.WorldPosition` | Position | 世界坐标位置 |
| `.PositionEast` | Real | 东坐标 X |
| `.PositionNorth` | Real | 北坐标 Y |
| `.PositionUp` | Real | 高度 Z |
| `.Orientation` | Real | 朝向角 |
| `.Room` | String | 房间号 |
| `.Insulation` | String | 保温等级 |
| `.ChangeStatus` | String | 修改状态 |

## PML 字符串方法速查

| 方法 | 说明 | 示例 |
|------|------|------|
| `!str.Length()` | 长度 | `!str.Length()` |
| `!str.LowCase()` | 转小写 | `!str.LowCase()` |
| `!str.UpCase()` | 转大写 | `!str.UpCase()` |
| `!str.Trim()` | 去空格 | `!str.Trim()` |
| `!str.After(d)` | 截取 d 后 | `!str.After('/')` |
| `!str.Before(d)` | 截取 d 前 | `!str.Before('@')` |
| `!str.Part(n, d)` | 第 n 段 | `!str.Part(2, ',')` |
| `!str.Replace(a, b)` | 替换 | `!str.Replace('old','new')` |
| `!str.Split(d)` | 分割为数组 | `!str.Split(',')` |
| `!str.Match(p)` | 匹配 | `!str.Match('*DN100*')` |
| `!str.Substring(n, len)` | 子串 | `!str.Substring(2, 5)` |

## 实数方法

| 方法 | 说明 | 示例 |
|------|------|------|
| `!x.Sqrt()` | 开方 | `!x.Sqrt()` |
| `!x.Power(n)` | 乘方 | `!x.Power(2)` |
| `INT(!x)` | 取整 | `INT(!x)` |
| `ABS(!x)` | 绝对值 | `ABS(!x)` |

## 常用特殊判断

```pml
-- 变量是否已定义
IF Defined(!var) THEN ... ENDIF
IF UnDefine(!var) THEN ... ENDIF

-- 变量是否有值
IF Set(!var) THEN ... ENDIF
IF Unset(!var) THEN ... ENDIF

-- 类型判断
!type = typeof(!var)
```

```csharp
// C# 中判断元素是否有效
if (element.IsValid) { }

// 判断属性是否为空
string val = element.GetAsString(DbAttributeInstance.WTHK);
if (!string.IsNullOrEmpty(val)) { }
```
