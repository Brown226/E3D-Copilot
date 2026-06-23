# PML 表达式与语法速查

## 变量

| 规则 | 说明 |
|------|------|
| `!var` | 局部变量 |
| `!!var` | 全局变量 |
| 最大长度 | 16 字符（不含 `!` `!!`） |
| 字符 | 只能字母和数字 |
| 不区分大小写 | 建议大写开头 |

## 数据类型

| 类型 | 说明 |
|------|------|
| STRING | 字符串，用单引号 `'...'` |
| REAL | 实数（含整数） |
| BOOLEAN | 布尔值 TRUE/FALSE |
| ARRAY | 数组 |
| POSITION | 位置 |
| ORIENTATION | 方位 |
| DIRECTION | 方向 |
| REFERENCE | 参考 |

## 运算符

| 类别 | 符号 |
|------|------|
| 算术 | `+ - * /` |
| 合并 | `&`（字符串连接） |
| 比较 | `EQ NE LT LE GT GE`（**非** `== != < <= > >=`） |
| 布尔 | `NOT AND OR` |

⚠️ **比较运算符必须用英文单词形式**，如 `EQ` 而非 `==`。

## 属性访问

```pml
-- 通过 Dbref 访问属性（推荐）
!wthk = !ele.Dbref().:WTHK
!spec = !ele.Dbref().:SPEC

-- 通过对象属性访问
!name = !!CE.Name
!desc = !!CE.Description
!pspec = !!CE.Pspec

-- 位置属性（⚠️ 注意缩写）
!east = !ele.Dbref().:PEAST    -- X 坐标
!north = !ele.Dbref().:PNORT   -- Y 坐标
!up = !ele.Dbref().:PUP         -- Z 坐标
```

## !!CE 特殊全局变量

`!!CE` 自动引用当前选中元素（Current Element）：

```pml
!Name = !!CE.Name
!Desc = !!CE.Description
!Pspec = !!CE.Pspec
!Temp = !!CE.Temp
!Pos = !!CE.Position        -- 位置对象
!Rating = !!CE.cref.pspec.rating  -- 参考属性链
```

## 注释

```pml
-- 单行注释（两个减号）
-- 注释以 -- 开头
```

## 输出

```pml
$P '输出文本'              -- 打印到命令行
$P !var.Name, ' = ', !var.Dbref().:WTHK  -- 组合输出
```
