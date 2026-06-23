# PML 系统对象完整参考

> 来源: Software Customisation Reference Manual Chapter 2
> 覆盖: FMSYS, COLLECTION, REPORT, UNDOABLE, PROJECT, SESSION, USER, EXPORT, MACRO, POSTEVENTS, BANNER, TEAM, EXPRESSION, NUMERICINPUT, DATEFORMAT, DATETIME

---

## FMSYS 对象

系统功能对象。通过 `!!fmsys` 访问。

### 方法

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!!fmsys.SetMain(!form)` | FORM | 设置主窗体 |
| `!!fmsys.Main()` | FORM | 获取主窗体 |
| `!!fmsys.Refresh()` | — | 刷新所有 VIEW 控件 |
| `!!fmsys.Checkrefs` | BOOLEAN | 引用检查开关 |
| `!!fmsys.SetInterrupt(!gadget)` | — | 设置中断控件 |
| `!!fmsys.Interrupt()` | BOOLEAN | 是否触发中断 |
| `!!fmsys.Splashscreen(bool)` | — | 移除闪屏 |
| `!!fmsys.Fminfo()` | ARRAY | ⭐ 获取系统信息数组（含 E3D/PDMS 版本） |
| `!!fmsys.SetProgress(!pct)` | — | ⭐ 设置进度条(0-100) |
| `!!fmsys.Progress()` | REAL | ⭐ 获取进度条值 |
| `!!fmsys.CurrentDocument()` | FORM | 当前文档窗体 |
| `!!fmsys.LoadForm('name')` | FORM | 强制加载窗体 |
| `!!fmsys.SetHelpFileAlias('a')` | — | 设置帮助文件别名 |
| `!!fmsys.HelpFileAlias()` | STRING | 获取帮助文件别名 |
| `!!fmsys.OKCurfnView('G3D')` | BOOLEAN | 检查 3D 视图是否显示 |
| `!!fmsys.OKCurfnView('G2D', 'NORMAL')` | BOOLEAN | 检查 2D 视图 |
| `!!fmsys.SetDefaultFormat(!fmt)` | — | 设置默认格式 |
| `!!fmsys.DefaultFormat()` | FORMAT | 获取默认格式 |
| `!!fmsys.DocsAtMaxScreen(bool)` | — | 设置文档位置靠右 |

### FMINFO() 返回值（常用于 E3D/PDMS 版本判断）

```pml
-- 判断是否为 E3D
!INFO = !!FMSYS.FMINFO()
IF !INFO[0].Matchwild('*E3D*') THEN
    $P '当前运行在 E3D'
ELSE
    $P '当前运行在 PDMS'
ENDIF
```

---

## COLLECTION 对象

元素收集器。

### 方法

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `OBJECT COLLECTION()` | — | 创建收集器 |
| `!col.Type('PIPE')` | — | 设置元素类型 |
| `!col.Scope(!dbref)` | — | 设置搜索范围 |
| `!col.Filter('expr')` | — | 设置过滤表达式 |
| `!col.Results()` | ARRAY | 获取结果 |
| `!col.Next()` | DBREF | 获取下一个 |
| `!col.First()` | DBREF | 获取第一个 |
| `!col.Count()` | REAL | 结果数量 |
| `!col.Clear()` | — | 清空 |
| `!col.Within(!pos1, !pos2)` | — | 空间范围限制 |

---

## REPORT 对象

PML 原生报表引擎。

### 方法

| 方法 | 说明 |
|------|------|
| `OBJECT REPORT()` | 创建报表对象 |
| `!rep.Title('title')` | 设置标题 |
| `!rep.Author('name')` | 设置作者 |
| `!rep.Heading('h')` | 设置表头 |
| `!rep.Line()` | 新建一行 |
| `!rep.Column(n, 'val')` | 设置第 n 列的值 |
| `!rep.Print()` | 打印/输出 |
| `!rep.Page()` | 分页 |
| `!rep.SetWidth(n)` | 设置列宽 |

---

## UNDOABLE 对象

事务撤销支持。

### 用法模式

```pml
!THIS.UNDO = OBJECT UNDOABLE()
!THIS.UNDO.description('修改壁厚')
!THIS.UNDO.add()          -- 标记开始（加撤销栈）
    -- 执行修改操作
    !ELE.Dbref().:WTHK = 'SCH40'
!THIS.UNDO.endundoable()  -- 标记结束
```

### 方法

| 方法 | 说明 |
|------|------|
| `OBJECT UNDOABLE()` | 创建撤销对象 |
| `!u.description('text')` | 设置描述文本 |
| `!u.add()` | 标记数据库并加入撤销栈 |
| `!u.endundoable()` | 标记修改结束 |
| `!u.undoAction('cmd')` | 撤销时执行的命令 |
| `!u.redoAction('cmd')` | 重做时执行的命令 |
| `!u.clearAction('cmd')` | 清除时执行的命令 |

---

## PROJECT 对象

项目信息。

### 成员

| 属性 | 类型 | 说明 |
|------|:----:|------|
| `.Name` | STRING | 项目名 |
| `.Description` | STRING | 项目描述 |
| `.RootElement` | DBREF | 根元素 |
| `.IsOpen` | BOOLEAN | 是否打开 |

### 方法

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!proj.MDBList()` | ARRAY | MDB 列表 |
| `!proj.Users()` | ARRAY | 用户列表 |
| `!proj.Teams()` | ARRAY | 团队列表 |

---

## SESSION 对象

用户会话。

### 方法

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!sess.UserName()` | STRING | 用户名 |
| `!sess.Project()` | PROJECT | 当前项目 |
| `!sess.MDB()` | MDB | 当前 MDB |

---

## USER 对象

用户信息。

### 方法

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!user.Name()` | STRING | 用户名 |
| `!user.FullName()` | STRING | 全名 |
| `!user.Team()` | TEAM | 所属团队 |

---

## MACRO 对象

宏执行。

| 方法 | 说明 |
|------|------|
| `OBJECT MACRO('file')` | 创建宏对象 |
| `!macro.Run()` | 执行宏 |

---

## POSTEVENTS 对象

事件发布。

### 方法

| 方法 | 说明 |
|------|------|
| `!pe.Post('eventName')` | 发布事件 |
| `!pe.PostTo('eventName', target)` | 向指定目标发布 |

---

## BANNER 对象

横幅/状态栏显示。

| 属性 | 类型 | 说明 |
|------|:----:|------|
| `.Text` | STRING | 文本内容 |
| `.Active` | BOOLEAN | 显示状态 |
| `.Background` | REAL | 背景色 |
| `.Highlight` | REAL | 高亮色 |

---

## TEAM 对象

团队信息。

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!team.Name()` | STRING | 团队名 |
| `!team.Users()` | ARRAY | 用户列表 |

---

## EXPRESSION 对象

表达式求值。

| 方法 | 说明 |
|------|------|
| `OBJECT EXPRESSION()` | 创建表达式对象 |
| `!exp.Expression('expr')` | 设置表达式 |
| `!exp.AttributeExpression(':WTHK')` | 设置属性表达式 |
| `!exp.Evaluate()` | 求值 |

---

## NUMERICINPUT 对象

数值输入。

| 方法 | 说明 |
|------|------|
| `OBJECT NUMERICINPUT()` | 创建数值输入 |

---

## DATEFORMAT 对象

日期格式。

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `DATEFORMAT('YYYY-MM-DD')` | — | 创建日期格式 |
| `!df.Month(!r)` | DATETIME | 月份 |
| `!df.Year(!r)` | DATETIME | 年份 |
| `!df.String()` | STRING | 转字符串 |

---

## DATETIME 对象

日期时间值。

### 构造

| 方法 | 说明 |
|------|------|
| `DATETIME(year, mon, day)` | 从年月日创建 |
| `DATETIME(year, mon, day, hr, min, sec)` | 从完整日期创建 |

### 方法

| 方法 | 返回 | 说明 |
|------|:----:|------|
| `!dt.Year()` | REAL | 年 |
| `!dt.Month()` | REAL | 月 |
| `!dt.Day()` | REAL | 日 |
| `!dt.Hour()` | REAL | 时 |
| `!dt.Minute()` | REAL | 分 |
| `!dt.Second()` | REAL | 秒 |
| `!dt.Date()` | DATETIME | 仅日期部分 |
| `!dt.Time()` | DATETIME | 仅时间部分 |
| `!dt.String()` | STRING | 转字符串 |
| `!dt.String(!fmt)` | STRING | 按格式转字符串 |
| `!dt.Difference(!dt2)` | REAL | 日期差(秒) |
| `!dt.AddSeconds(!r)` | DATETIME | 加秒 |
| `!dt.AddDays(!r)` | DATETIME | 加天 |
| `!dt.AddMonths(!r)` | DATETIME | 加月 |
| `!dt.AddYears(!r)` | DATETIME | 加年 |
