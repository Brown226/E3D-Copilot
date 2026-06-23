# PML 内置函数速查

## 字符串匹配

| 函数 | 用法 | 说明 | 已验证 |
|------|------|------|:------:|
| Matchwild | `Matchwild(NAME, '*PAT*', true)` | 通配符匹配，第三个参数忽略大小写 | ✅ 114次 |
| MATCH | `MATCH(NAME, \|*PAT*\|) neq 0` | 匹配检查（真实工具更常用） | ✅ |
| Match | `!str.Match('PAT')` | 字符串方法形式的匹配 | ✅ |

> ⚠️ `Matchwild` 和 `MATCH` 都是内置函数。`MATCH()` 在真实工具中更常用。

## 距离计算

| 函数 | 用法 | 说明 |
|------|------|------|
| DISTANCE | `DISTANCE($!ELE1, $!ELE2)` | 两元素距离 |

## 存在检查

| 函数 | 用法 | 说明 |
|------|------|------|
| EXIST | `EXIST $!NAME` | 检查元素是否存在 |
| Defined | `Defined(!var)` | 变量已定义 |
| UnDefine | `UnDefine(!var)` | 变量未定义 |
| Set | `Set(!var)` | 变量已赋值 |
| Unset | `Unset(!var)` | 变量未赋值 |

## 类型判断

| 函数 | 用法 | 说明 |
|------|------|------|
| TypeOf | `typeof(!var)` | 获取变量类型 |

## 字符串方法

| 方法 | 说明 |
|------|------|
| `!str.Length()` | 字符串长度 |
| `!str.LowCase()` | 转小写 |
| `!str.UpCase()` | 转大写 |
| `!str.Trim()` | 去除两端空格 |
| `!str.After(str2)` | 截取 str2 之后的内容 |
| `!str.Before(str2)` | 截取 str2 之前的内容 |
| `!str.Substring(n)` | 从 n 开始截取 |
| `!str.Part(n, delim)` | 按分隔符取第 n 部分 |
| `!str.Replace(a, b)` | 替换 |
| `!str.Split(delim)` | 分割为数组 |

## 数学函数

| 函数/方法 | 说明 |
|-----------|------|
| `SQR(!x)` | 开平方 |
| `!x.Sqrt()` | 开平方（方法形式） |
| `!x.Power(n)` | 乘方 |
| `INT(!x)` | 取整 |
| `ABS(!x)` | 绝对值 |
