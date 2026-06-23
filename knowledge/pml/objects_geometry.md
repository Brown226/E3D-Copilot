# PML 几何对象完整参考

> 来源: Software Customisation Reference Manual Chapter 2
> 覆盖: POSITION, DIRECTION, ORIENTATION, ARC, LINE, PLANE, POINTVECTOR, BORE, LOCATION, XYPosition, FORMAT

---

## POSITION 对象

三维空间坐标点。

### 构造

| 方法 | 说明 |
|------|------|
| `POSITION(E, N, U)` | 从坐标创建 |
| `POSITION(ARRAY[3])` | 从数组创建 |

### 成员

| 属性 | 类型 | 说明 |
|------|:----:|------|
| `.East` / `.E` | REAL | 东坐标 X |
| `.North` / `.N` | REAL | 北坐标 Y |
| `.Up` / `.U` | REAL | 高度 Z |

### 方法

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!p.Distance(!p2)` | REAL | 到另一点的距离 |
| `!p.Midpoint(!p2)` | POSITION | 中点 |
| `!p.Offset(!dir, !dist)` | POSITION | 沿方向偏移 |
| `!p.WRT(!ref)` | POSITION | 转换到参考坐标系 |
| `!p.WRT(/*)` | POSITION | 转换到世界坐标 |
| `!p.String()` | STRING | 转字符串 |
| `!p.Array()` | ARRAY | 转数组 [E,N,U] |
| `!p.Near(!line)` | POSITION | 到直线最近点 |
| `!p.OnProjected(!line)` | BOOLEAN | 是否在直线投影上 |
| `!p.Vector(!p2)` | DIRECTION | 到另一点的向量 |
| `!p.Polar(!p2)` | ARRAY | 极坐标 [dist,bearing,elev] |

---

## DIRECTION 对象

三维方向向量。

### 构造

| 方法 | 说明 |
|------|------|
| `DIRECTION(E, N, U)` | 从分量创建 |
| `DIRECTION(ARRAY[3])` | 从数组创建 |

### 成员

| 属性 | 类型 | 说明 |
|------|:----:|------|
| `.East` / `.E` | REAL | X 分量 |
| `.North` / `.N` | REAL | Y 分量 |
| `.Up` / `.U` | REAL | Z 分量 |

### 方法

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!d.Angle(!d2)` | REAL | 两向量夹角(度) |
| `!d.Cross(!d2)` | DIRECTION | 叉积 |
| `!d.Dot(!d2)` | REAL | 点积 |
| `!d.Length()` | REAL | 长度 |
| `!d.Normalise()` | DIRECTION | 归一化 |
| `!d.IsParallel(!d2)` | BOOLEAN | 是否平行 |
| `!d.IsPerpendicular(!d2)` | BOOLEAN | 是否垂直 |
| `!d.Multiply(!r)` | DIRECTION | 标量乘法 |
| `!d.Negate()` | DIRECTION | 反向 |
| `!d.Rotate(!orient)` | DIRECTION | 按朝向旋转 |
| `!d.String()` | STRING | 转字符串 |

---

## ORIENTATION 对象

三维朝向/旋转。

### 构造

| 方法 | 说明 |
|------|------|
| `ORIENTATION()` | 默认朝向 |
| `ORIENTATION(ARRAY[9])` | 从旋转矩阵创建 |

### 方法

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!o.Rotate(!dir)` | DIRECTION | 旋转方向向量 |
| `!o.Multiply(!o2)` | ORIENTATION | 组合朝向 |
| `!o.Invert()` | ORIENTATION | 反向 |
| `!o.IsIdentity()` | BOOLEAN | 是否单位朝向 |
| `!o.String()` | STRING | 转字符串 |

---

## ARC 对象

二维圆弧。

### 成员

| 属性 | 说明 |
|------|------|
| `.Center` | 圆心位置 |
| `.Radius` | 半径 |
| `.StartAngle` | 起始角度 |
| `.EndAngle` | 结束角度 |
| `.Start` | 起点 |
| `.End` | 终点 |

### 方法（返回 ARC）

| 方法 | 说明 |
|------|------|
| `!arc.Invert()` | 反转方向 |
| `!arc.MoveBy(!xy)` | 平移 |
| `!arc.MoveTo(!pos)` | 移动到 |
| `!arc.Offset(!r)` | 偏移半径 |

### 方法（返回 POSITION）

| 方法 | 说明 |
|------|------|
| `!arc.Center()` | 圆心 |
| `!arc.Start()` | 起点 |
| `!arc.End()` | 终点 |
| `!arc.PointAt(!t)` | 参数 t 处的点 |
| `!arc.Pole()` | 极点 |

### 方法（返回 REAL）

| 方法 | 说明 |
|------|------|
| `!arc.Length()` | 弧长 |
| `!arc.ChordLength()` | 弦长 |
| `!arc.Height()` | 弧高 |
| `!arc.Bulge()` | 凸度 |
| `!arc.SubtendedAngle()` | 圆心角 |
| `!arc.Radius()` | 半径 |

### 方法（返回 BOOLEAN）

| 方法 | 说明 |
|------|------|
| `!arc.IsCircle()` | 是否为完整圆 |
| `!arc.IsClockwise()` | 是否为顺时针 |
| `!arc.Intersects(!line)` | 是否与直线相交 |
| `!arc.Intersects(!arc)` | 是否与弧相交 |
| `!arc.Touches(!pos)` | 是否经过点 |

---

## LINE 对象（3D 几何直线）

### 构造

| 方法 | 说明 |
|------|------|
| `LINE(!pos1, !pos2)` | 从两点创建 |

### 成员

| 属性 | 类型 | 说明 |
|------|:----:|------|
| `.Start` | POSITION | 起点 |
| `.End` | POSITION | 终点 |
| `.Direction` | DIRECTION | 方向向量 |
| `.Length` | REAL | 长度 |

### 方法

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!line.Intersection(!plane)` | POSITION | 与平面交点 |
| `!line.Near(!pos)` | POSITION | 最近点 |
| `!line.OnProjected(!pos)` | BOOLEAN | 投影是否在线上 |
| `!line.Distance(!pos)` | REAL | 到点的距离 |
| `!line.Midpoint()` | POSITION | 中点 |
| `!line.String()` | STRING | 转字符串 |

---

## PLANE 对象

三维平面。

### 构造

| 方法 | 说明 |
|------|------|
| `PLANE(!pos, !dir)` | 从点和法线创建 |
| `PLANE(!pos1, !pos2, !pos3)` | 从三点创建 |

### 方法

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!plane.Intersection(!line)` | POSITION | 与直线交点 |
| `!plane.Normal()` | DIRECTION | 法线向量 |
| `!plane.Distance(!pos)` | REAL | 到点的距离 |
| `!plane.Offset(!r)` | PLANE | 偏移平面 |
| `!plane.String()` | STRING | 转字符串 |

---

## POINTVECTOR 对象

表示带有向量的点。

### 方法

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!pv.Point()` | POSITION | 获取点 |
| `!pv.Vector()` | DIRECTION | 获取向量 |

---

## BORE 对象

管径值。含公称直径、外径、壁厚等信息。

### 方法

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!bore.Nominal()` | REAL | 公称直径 |
| `!bore.Actual()` | REAL | 实际外径 |
| `!bore.Wall()` | REAL | 壁厚 |
| `!bore.String()` | STRING | 转字符串 |
| `!bore.Real()` | REAL | 转实数 |

---

## LOCATION 对象

位置/地点引用。

### 方法

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!loc.Name()` | STRING | 名称 |
| `!loc.Position()` | POSITION | 位置 |
| `!loc.Description()` | STRING | 描述 |

---

## XYPosition / XYOffset 对象

二维坐标点/偏移量。

### 方法

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!xy.X()` | REAL | X 坐标 |
| `!xy.Y()` | REAL | Y 坐标 |
| `!xy.String()` | STRING | 转字符串 |

---

## FORMAT 对象

格式控制，用于 TEXT/REAL 格式化输出。

### 方法

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `FORMAT('fmt')` | FORMAT | 从格式字符串创建 |
| `!fmt.Width(!r)` | — | 设置宽度 |
| `!fmt.Precision(!r)` | — | 设置精度 |
| `!fmt.String()` | STRING | 转字符串 |
