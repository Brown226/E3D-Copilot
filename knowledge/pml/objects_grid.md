# PML Grid/Table 对象参考

> 来源: Software Customisation Reference Manual Chapter 2
> 覆盖: LINEARGRID, PLANTGRID, PLATFORMGRID, RADIALGRID, COLUMN, COLUMNFORMAT, TABLE, PROFILE, BLOCK

---

## LINEARGRID 对象

线性网格。

### 方法

| 方法 | 说明 |
|------|------|
| `OBJECT LINEARGRID()` | 创建网格 |
| `!grid.SetColumns(n)` | 设置列数 |
| `!grid.SetRows(n)` | 设置行数 |
| `!grid.SetColumnWidth(n, w)` | 设置列宽 |
| `!grid.SetRowHeight(n, h)` | 设置行高 |

---

## PLANTGRID 对象

工厂网格。

### 方法

| 方法 | 说明 |
|------|------|
| `OBJECT PLANTGRID()` | 创建工厂网格 |

---

## PLATFORMGRID 对象

平台网格。

### 方法

| 方法 | 说明 |
|------|------|
| `OBJECT PLATFORMGRID()` | 创建平台网格 |
| `!grid.SetOrigin(!pos)` | 设置原点 |
| `!grid.SetDirection(!dir)` | 设置方向 |
| `!grid.SetSpacing(!r)` | 设置间距 |

---

## RADIALGRID 对象

径向网格。

### 方法

| 方法 | 说明 |
|------|------|
| `OBJECT RADIALGRID()` | 创建径向网格 |
| `!grid.SetCenter(!pos)` | 设置中心 |
| `!grid.SetRadius(!r)` | 设置半径 |
| `!grid.SetAngles(!a1, !a2)` | 设置角度范围 |

---

## COLUMN 对象

列定义。

| 方法 | 说明 |
|------|------|
| `!col.Expression('expr')` | 设置表达式 |
| `!col.Sort(!order)` | 设置排序 |
| `!col.Key(bool)` | 设为键列 |
| `!col.Format(!fmt)` | 设置格式 |
| `!col.Width(!r)` | 设置宽度 |
| `!col.Heading('text')` | 设置表头 |

---

## COLUMNFORMAT 对象

列格式。

| 方法 | 说明 |
|------|------|
| `!cf.Format('fmt')` | 设置格式字符串 |
| `!cf.Width(!r)` | 列宽 |
| `!cf.Indent(!r)` | 缩进 |
| `!cf.Alignment('L'/'C'/'R')` | 对齐方式 |

---

## TABLE 对象

表格。

| 方法 | 说明 |
|------|------|
| `OBJECT TABLE()` | 创建表格 |

---

## PROFILE 对象

截面/轮廓。

| 方法 | 说明 |
|------|------|
| `OBJECT PROFILE()` | 创建截面对象 |
| `!prof.AddSegment(!pos)` | 添加线段 |
| `!prof.Close()` | 闭合截面 |

---

## BLOCK 对象

代码块。

| 方法 | 说明 |
|------|------|
| `!block.Block(bool)` | 设置块状态 |
| `!block.Evaluate()` | 执行块 |
