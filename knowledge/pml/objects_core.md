# PML 核心对象完整参考

> 来源: Software Customisation Reference Manual Chapter 2
> 覆盖: STRING, REAL, BOOLEAN, ARRAY, FILE, DBREF, DB, DBSESS, MDB, ALERT

---

## ALERT 对象

系统警告/确认/输入对话框。通过 `!!Alert` 访问。

### 无返回值

| 方法 | 说明 |
|------|------|
| `!!Alert.Error('msg' [, x, y])` | 错误警告 |
| `!!Alert.Message('msg' [, x, y])` | 信息提示 |
| `!!Alert.Warning('msg' [, x, y])` | 警告提示 |

### 带返回值

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!!Alert.Confirm('msg')` | `'YES'`/`'NO'` | 确认对话框 |
| `!!Alert.Question('msg')` | `'YES'`/`'NO'`/`'CANCEL'` | 询问对话框 |
| `!!Alert.Input('prompt', 'default')` | STRING | 输入对话框 |

---

## STRING 对象

### 构造

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `STRING('text')` | STRING | 创建字符串 |

### 查询

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!s.Length()` | REAL | 长度 |
| `!s.Count('sub')` | REAL | 子串出现次数 |
| `!s.Occurs('ch')` | REAL | 字符出现次数 |
| `!s.Find('sub')` | REAL | 查找子串位置 |
| `!s.FindFirst('sub')` | REAL | 首次出现位置 |
| `!s.Match('pattern')` | BOOLEAN | 通配符匹配 |
| `!s.MatchWild('pattern')` | BOOLEAN | 通配符匹配(忽略大小写) |

### 转换

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!s.Real()` | REAL | 转实数 |
| `!s.Position()` | POSITION | 转位置 |
| `!s.Boolean()` | BOOLEAN | 转布尔 |
| `!s.String(fmt)` | STRING | 带格式转字符串 |
| `!s.Integer()` | REAL | 转整数 |
| `!s.LowCase()` | STRING | 转小写 |
| `!s.UpCase()` | STRING | 转大写 |

### 截取/分割

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!s.After('delim')` | STRING | 取分隔符之后 |
| `!s.Before('delim')` | STRING | 取分隔符之前 |
| `!s.Substring(start)` | STRING | 从 start 开始截取 |
| `!s.Substring(start, n)` | STRING | 截取 n 个字符 |
| `!s.Part(n)` | STRING | 按空格取第 n 段 |
| `!s.Part(n, 'delim')` | STRING | 按分隔符取第 n 段 |
| `!s.Split('delim')` | ARRAY | 分割为数组 |
| `!s.Left(n)` | STRING | 取左边 n 个字符 |
| `!s.Right(n)` | STRING | 取右边 n 个字符 |
| `!s.Trim()` | STRING | 去两端空格 |
| `!s.Trim('L')` | STRING | 去左边空格 |
| `!s.Trim('R')` | STRING | 去右边空格 |

### 修改

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!s.Replace(a, b)` | STRING | 替换 |
| `!s.Insert(pos, 'str')` | STRING | 插入 |
| `!s.Delete(start, n)` | STRING | 删除 |
| `!s.Set(start, n, 'str')` | STRING | 替换指定位置 |

---

## REAL 对象

### 构造

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `REAL(n)` | REAL | 创建实数 |
| `Real()` | REAL | 创建未设置实数 |

### 数学

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!r.Sqrt()` | REAL | 开平方 |
| `!r.Power(n)` | REAL | 乘方 |
| `!r.Sin()` | REAL | 正弦（度） |
| `!r.Cos()` | REAL | 余弦 |
| `!r.Tan()` | REAL | 正切 |
| `!r.ASin()` | REAL | 反正弦 |
| `!r.ACos()` | REAL | 反余弦 |
| `!r.ATan()` | REAL | 反正切 |
| `!r.Log()` | REAL | 自然对数 |
| `!r.Log10()` | REAL | 常用对数 |
| `!r.Exp()` | REAL | 指数 |
| `!r.Abs()` | REAL | 绝对值 |
| `!r.Int()` | REAL | 取整 |
| `!r.Round()` | REAL | 四舍五入 |
| `!r.Floor()` | REAL | 向下取整 |
| `!r.Ceiling()` | REAL | 向上取整 |
| `Rnd(n)` | REAL | 随机数（0~n） |
| `Pi` | REAL | 圆周率 3.14159 |

### 转换

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!r.String()` | STRING | 转字符串 |
| `!r.String(fmt)` | STRING | 带格式转字符串 |
| `!r.Boolean()` | BOOLEAN | 转布尔（非0=true） |

---

## BOOLEAN 对象

### 构造

| 方法 | 说明 |
|------|------|
| `TRUE` | 真值 |
| `FALSE` | 假值 |
| `Boolean()` | 创建布尔 |
| `Boolean(REAL)` | 从实数创建 |
| `Boolean(STRING)` | 从字符串创建 |

### 逻辑运算

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!b.AND(!b2)` | BOOLEAN | 与 |
| `!b.OR(!b2)` | BOOLEAN | 或 |
| `!b.NOT()` | BOOLEAN | 非 |
| `!b.Real()` | REAL | 转实数 |
| `!b.String()` | STRING | 转字符串 |

---

## ARRAY 对象

### 构造

| 方法 | 说明 |
|------|:----:|
| `ARRAY()` | 创建空数组 |
| `!a[n] = val` | 索引赋值 |

### 查询

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!a.Size()` | REAL | 元素数量 |
| `!a.Width()` | REAL | 元素最大宽度 |
| `!a.Find(val)` | REAL | 查找值 |
| `!a.FindFirst(val)` | REAL | 首次出现位置 |
| `!a.FindLast(val)` | REAL | 最后出现位置 |
| `!a.Count(val)` | REAL | 值出现次数 |
| `!a.Max()` | REAL | 最大值 |
| `!a.Min()` | REAL | 最小值 |
| `!a.Sum()` | REAL | 求和 |
| `!a.Average()` | REAL | 平均值 |
| `!a.Median()` | REAL | 中位数 |
| `!a.Contains(val)` | BOOLEAN | 是否包含 |
| `!a.IsEmpty()` | BOOLEAN | 是否为空 |

### 修改

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!a.Append(val)` | — | 追加元素 |
| `!a.AppendArray(arr)` | — | 追加数组 |
| `!a.Insert(n, val)` | — | 在位置 n 插入 |
| `!a.Remove(n)` | — | 移除位置 n |
| `!a.Clear()` | — | 清空 |
| `!a[n].Delete()` | — | 删除单个元素 |
| `!a.Delete()` | — | 删除数组 |
| `!a.Compress()` | — | 压缩（删除空元素） |
| `!a.Sort()` | — | 排序 |
| `!a.Sort('REAL')` | — | 按数值排序 |
| `!a.Invert()` | — | 倒序 |
| `!a.Unique()` | — | 去重 |
| `!a.ReIndex(!indices)` | — | 按索引重排 |
| `!a.SortedIndices()` | ARRAY | 返回排序索引 |

### 复制/转换

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!a.Copy()` | ARRAY | 复制 |
| `!a.SubArray(start, n)` | ARRAY | 子数组 |
| `!a.AsString()` | STRING | 转字符串 |
| `!a.Swap(n1, n2)` | — | 交换元素 |

---

## FILE 对象

### 打开/关闭

| 方法 | 说明 |
|------|------|
| `OBJECT FILE('path')` | 创建文件对象 |
| `!f.Open('READ')` | 打开读取 |
| `!f.Open('WRITE')` | 打开写入 |
| `!f.Close()` | 关闭 |

### 读取

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!f.ReadFile()` | ARRAY | 读取全部（返回数组） |
| `!f.ReadFile(maxLines)` | ARRAY | 读取最多 N 行 |
| `!f.ReadLine()` | STRING | 读一行 |
| `!f.ReadRecord()` | STRING | 读一条记录 |

### 写入

| 方法 | 说明 |
|------|------|
| `!f.WriteFile('WRITE', arr)` | 写入（覆盖） |
| `!f.WriteFile('OVERWRITE', arr)` | 写覆盖 |
| `!f.WriteFile('APPEND', arr)` | 追加写入 |
| `!f.WriteRecord(str)` | 写单行 |
| `!f.WriteLine(str)` | 写一行（换行） |

### 文件操作

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!f.Copy('newpath')` | — | 复制文件 |
| `!f.Move('newpath')` | — | 移动文件 |
| `!f.Delete()` | — | 删除文件 |
| `!f.Exists()` | BOOLEAN | 是否存在 |
| `!f.Type()` | STRING | 返回 `'FILE'` 或 `'DIRECTORY'` |
| `!f.Directory()` | FILE | 获取目录 |
| `!f.FullName()` | STRING | 完整路径 |
| `!f.Name()` | STRING | 文件名 |
| `!f.Size()` | REAL | 文件大小 |

---

## DBREF 对象

数据库元素引用。通过 `!ele.Dbref()` 获取。

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!ref.Attribute(':ATTR')` | ANY | 获取/设置属性值 |
| `!ref.BadRef()` | BOOLEAN | 引用是否无效 |
| `!ref.Colour` | REAL | 颜色号 |
| `!ref.Dbref()` | DBREF | 自身引用 |
| `!ref.Flnm` | STRING | 完整名称（含路径） |
| `!ref.Flnn` | STRING | 完整名称（别名） |
| `!ref.MCount` | REAL | 成员数量 |
| `!ref.MName` | STRING | 成员名称 |
| `!ref.Name` | STRING | 元素名称 |
| `!ref.Owner` | DBREF | 父元素引用 |
| `!ref.Position` | POSITION | 位置 |
| `!ref.PPosition[n]` | POSITION | P-Point 位置 |
| `!ref.PDirection[n]` | DIRECTION | P-Point 方向 |
| `!ref.Type` | STRING | 元素类型 |
| `!ref.Worpos` | POSITION | 世界坐标位置 |

---

## DB 对象

数据库会话。

### 成员

| 属性 | 类型 | 说明 |
|------|:----:|------|
| `.IsValid` | BOOLEAN | 是否有效 |
| `.IsOpen` | BOOLEAN | 是否打开 |
| `.Name` | STRING | 数据库名称 |
| `.MDBList` | ARRAY | MDB 列表 |

### 方法

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!db.Size()` | REAL | 数据库大小 |
| `!db.Sessions()` | ARRAY | 当前会话列表 |
| `!db.MDBList()` | ARRAY | MDB 列表 |

---

## DBSESS 对象

数据库会话信息。

### 成员

| 属性 | 类型 | 说明 |
|------|:----:|------|
| `.Name` | STRING | 会话名称 |
| `.User` | STRING | 用户名 |
| `.MDB` | MDB | 关联的 MDB |
| `.IsCurrent` | BOOLEAN | 是否为当前会话 |

---

## MDB 对象

主数据库。

### 方法

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!mdb.Name()` | STRING | MDB 名称 |
| `!mdb.Owner()` | DBREF | 根元素 |
| `!mdb.IsOpen()` | BOOLEAN | 是否打开 |
