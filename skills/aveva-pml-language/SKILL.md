---
name: aveva-pml-language
description: AVEVA E3D/PDMS PML 编程语言完整语法参考。基于工业标准 .pmlfrm/.pmlfnc 文件提取的正确写法。写 PML 前先调用此 skill。
runAs: inline
tags: [PML, E3D, 语法, 参考]
---

# PML 语言完整参考

> 基于 CNPE 工业级 PML 工具文件（CableLayTools/conntray/EquiCheck/DrawDisplace/ExploreTreeOrder 等）提取的正确写法。

---

## 零、顶层结构

PML 文件有两种顶层结构，不能混用：

### 0.1 Form 定义（.pmlfrm 文件）

```pml
-- Kill 防重复定义（每个 Form 开头必须有）
Kill !!ToolName

-- 定义 Form
Setup Form !!ToolName
    title '工具标题'
    !this.initcall = '!this.init()'

    FRAME .Setting at x0
        ...
    exit

    member .varName is ARRAY
    member .flag is BOOLEAN
exit

-- 方法定义
define method .init()
    ...
endmethod
```

### 0.2 独立函数定义（.pmlfnc 文件）

```pml
define function !!FunctionName()
import |\\server\path\To\DotNet.dll|    -- 加载 .NET DLL
handle any
endhandle
using namespace |Namespace.Name|
!obj = Object ClassName()
!obj.MethodName()
endfunction
```

---

## 一、变量系统

### 1.1 变量类型

| 类型 | 声明 | 示例 |
|------|------|------|
| PML2 局部变量 | `!name` | `!count = 0` |
| PML2 全局变量 | `!!name` | `!!headsString = '列1,列2'` |
| PML2 系统变量 | `$!name` | `$!var` 导航到元素 |
| PML1 变量 | `$P name` | `$P var = DB ELEMENT 'name'` |
| C# DLL 对象 | `!obj = Object ClassName()` | `!obj.Start()` |
| 文件对象 | `!file = object file('path')` | |

### 1.2 数组操作

```pml
!arr = ARRAY()                    -- 创建空数组
!arr.append(!value)               -- 追加元素
!arr[1]                           -- 索引访问（从1开始）
!arr.size()                       -- 数组长度
!arr.Unique()                     -- 去重
!arr.split(',')                   -- 按分隔符拆成数组
```

### 1.3 调试输出

```pml
Q VAR !xxx                         -- 快速变量查看（调试输出到控制台）
$p '输出文字'                      -- 输出到控制台
!xxx.string()                      -- 转字符串
```

---

## 二、控制逻辑

### 2.1 条件判断

```pml
if !flag eq 'TRUEA' then
    ...
else
    ...
endif

if type eq 'ZONE' then              -- 用 eq 比较类型
    ...
endif

if type inset('FTUB','TEE','BEND') then    -- 类型是否属于集合
    ...
endif

if !findFlag eq FALSE then          -- 布尔比较
    ...
endif

if match(!zoneName,'SUP') gt 0 then -- match 函数（gt=greater than）
    ...
endif
```

### 2.2 循环

```pml
-- 遍历数组（最常用）
DO !val values !array
    ...
ENDDO

-- 遍历索引
DO !i index !array
    !array[$!i].Dbref()
ENDDO

-- 计数循环（推荐）
DO !index from 1 to !array.size() by 1
    ...
ENDDO

-- 计数循环（简写）
DO !a to !cnt
    ...
ENDDO
```

### 2.3 错误处理

```pml
handle any                              -- 捕获所有错误
    break
endhandle

handle (2,752)(2,754)                   -- 捕获指定错误号
    !message = !suppo.Dbref().flnn + '异常'
    $p $!message
    skip                                -- 跳过继续
endhandle

handle (41,322)                          -- 文件 IO 错误
    !!alert.warning('写入失败')
    return
endhandle
```

### 2.4 跳过与中断

```pml
skip                                    -- 跳过当前迭代
break                                   -- 跳出循环
return                                  -- 退出方法
```

---

## 三、DB 元素操作

### 3.1 元素引用

```pml
$P var = DB ELEMENT 'NAME'             -- PML1 语法：按名称引用
$!NAME                                  -- PML2 语法：导航到元素（设置 CE）
```

### 3.2 元素创建

```pml
-- 标准类型（PIPE/BRAN/EQUI/STRU/SUPPO/GENSEC 等）
$P parent = DB ELEMENT 'PARENT'
$P new = NEW PIPE parent

-- 特殊类型（FTUB / FMTG / SPCON 等 NEW 不支持）
CREATE $P new TYPE FTUB REF DB ELEMENT 'PARENT'
```

### 3.3 元素删除

```pml
$P var = DB ELEMENT 'NAME'
DELETE $P var
```

### 3.4 导航关系

```pml
!bran = bran of $!one                  -- 获取所属 BRAN
!zone = zone of $!one                  -- 获取所属 ZONE
!suppo = suppo of $!one                -- 获取所属 SUPPO
!owner = owner                          -- 获取父元素
!pre = pre                              -- 获取前一个兄弟元素
!next = next                            -- 获取后一个兄弟元素
!member = !!ce.mem                      -- 获取所有子元素
```

### 3.5 属性读写

```pml
-- 读属性（格式：!var.Dbref().:属性名）
!val = !ce.Dbref().:ATTR
!val = !suppo.Dbref().:conntray
!val = !gensecs[1].Dbref().spref.name    -- 链式：索引 → Dbref → 关联元素 → 属性

-- 写属性
!ce.Dbref().:ATTR = 'value'
!suppo.Dbref().:conntray = !new

-- 获取完整路径名
!fullName = fullname of $!ca
```

### 3.6 存在性检查

```pml
var !flag exists $!name                -- 检查元素是否存在
if !flag eq 'TRUEA' then               -- 返回字符串 'TRUEA'/'FALSEA'
    ...
endif
```

---

## 四、集合查询

### 4.1 基本查询

```pml
-- 按类型查询（范围内所有）
var !coll coll all PIPE for $!SITE
var !coll coll all TYPE with Matchwild(name,'*PAT*') for $!SCOPE

-- 多类型集合
var !all coll all (FTUB ELBO BEND TEE REDU CROS) within volume $!suppo

-- 追加集合（扩展已有集合）
var !allEqui append coll all EQUI FOR $!SITE
var !allEqui append coll all VALV FOR $!SITE
var !allEqui append coll all PCOM FOR $!SITE

-- 遍历 site（多种匹配模式）
var !sites coll all site with Matchwild(name,'*ELECHB*')
var !sites append coll all site with Matchwild(name,'*HVACHB*')
```

### 4.2 OO 方式

```pml
!coll = COLLECTION()
!coll.Type('PIPE')
!coll.Scope(!!ce)
!coll.Filter(!expr)
!results = !coll.Results()
```

### 4.3 冲突检查

```pml
define method .Roclash(!name is string, !ObName is string) is string
    DESCLASH
    OVERRIDE ON
    REM EXCL ALL
    REP HEADER OFF
    REP MAIN OFF
    REP SUMMARY OFF
    REM OBST ALL
    OBST $!ObName
    CHECK $!name
    VAR !COUNT CLASH COUNT CLASHES
    !cnt = !COUNT.real()
    if !cnt ne 0 then
        do !a to !cnt
            var !ca clash $!a second
            !CaName = fullname of $!ca
        enddo
    endif
    return !CaName
endmethod
```

---

## 五、UI / Form 操作

### 5.1 Form 定义

```pml
Kill !!FormName                         -- 防重复定义

Setup Form !!FormName
    title '工具标题'
    !this.initcall = '!this.init()'     -- 初始化回调

    FRAME .Setting at x0
        para .lab1 at x0.2 ymax text '标签' width 8
        list .list1 at x.5 ymax-0.2 width 45 heig 18
        button .but1 '操作' at xmax.list1+0.2 ymin.list1 callback '!this.method()' width 6
        path down                       -- 强制换行
        path right                      -- 同行排列
        button .but2 '清除' callback '!this.clear()' width 6
    exit

    member .varName is ARRAY            -- 成员变量
    member .flag is BOOLEAN
exit

-- 底部确认/取消按钮
button .Cancel |Cancel| background 2 at xmax form-size ymax+0.5 CANCEL
button .Ok     |Apply | background 5 at x0 ymin OK
```

### 5.2 输入控件

```pml
-- 文本输入框
text .Site | SITE | at x0 ymax + 0.5 tagwid 6 width 15 is string

-- 下拉列表
list .ExploreTree | 标题 | width 30 height 12

-- 用数组填充列表
!this.ExploreTree.dtext = !zoneNames

-- 获取选中项
!select = !this.list1.selection()               -- 获取选中值
!select = !this.ExploreTree.selection('Dtext')  -- 指定返回模式

-- 设置表头
!heads1 = '列1,列2,列3'
!this.list1.setHeadings(!heads1)

-- 设置多行数据
!newSetrows = ARRAY()
!newSetrows.append(!newlist)
!this.list1.setRows(!newSetrows)

-- 程序化选中某项
!this.ExploreTree.Select('Dtext', '$!zoneName')

-- 清空列表
!this.list1.clear()
```

### 5.3 方法定义

```pml
define method .init()
    !heads = '列1,列2,列3'
    !this.list1.setHeadings(!heads)
endmethod

define method .action()
    !select = !this.list1.selection()
    $!select
endmethod

define method .methodName(!param is string) is string
    ...
    return !result
endmethod
```

### 5.4 回调语法

```pml
-- 方法回调（推荐）
callback '!this.method()'

-- 按钮 action call（pipe 包裹）
call '!this.method()'
call |!this.method1()|                  -- pipe 分隔符

-- 进度条
!!fmsys.setProgress( !index / !total * 10000 )
!!fmsys.setProgress(0)                  -- 重置
```

### 5.5 Form 交互命令

```pml
!!Alert.message('提示信息')              -- 消息弹窗
!!Alert.error('错误信息')                -- 错误弹窗
!!Alert.warning('警告信息')              -- 警告弹窗
!!alert.input('提示', '默认值')           -- 输入框

prompt '提示文字'                        -- 状态栏提示
$p '输出文字'                            -- 输出到控制台

id FTUB TEE BEND @                      -- 交互选择（光标选元素）
ADD CE                                  -- 添加到选择集
auto ce                                 -- 自动适配视图
rem all                                 -- 清除选择
mark with 'string' ce                   -- 标记元素
enhance $!braname col red               -- 高亮元素
```

---

## 六、文件与外部调用

### 6.1 文件操作

```pml
!file = object file('$!fileName')        -- 打开文件
!file.Open('READ')
!lines = !file.ReadFile(30000)           -- 读文件（最多30000行）
!file.CLOSE()
!file.writefile('write', !array)         -- 写文件
```

### 6.2 .NET DLL 调用

```pml
import |\\server\path\To\DotNet.dll|    -- 加载 DLL（UNC 路径）
handle any
endhandle
using namespace |Namespace.Name|
!obj = Object ClassName()
!obj.Start()
```

### 6.3 PML 模块加载

```pml
import 'PMLFileBrowser'                 -- 加载内置 PML 模块
handle any
endhandle
using namespace 'Aveva.Core.Presentation'
!browser = object PMLFileBrowser('OPEN')
!browser.show('D:\','','选择文件',true, 'Excel Documents|*.csv',1)
!fileName = !browser.file()
```

---

## 七、字符串操作

```pml
!str.Before('SUP')                       -- 取匹配前部分
!str.replace('-','')                     -- 替换
!str.substring(2)                        -- 截取（从第2字符开始）
!str.split(',')                          -- 分割成数组
!str.string()                            -- 转字符串
Matchwild(!str, '*PAT*')                 -- 通配符匹配
match(!str, 'PAT')                       -- 匹配（返回匹配数）

!ce.Name                                  -- 获取元素名称
!ce.Dbref().flnn                          -- 获取元素完整显示名
!gensecs[1].Dbref().spref.name            -- 链式获取关联元素属性
```

---

## 八、元素排序

```pml
-- 在 DB 树中移动元素位置
reorder $!element before $!pre            -- 移到前一个元素之前
reorder $!element after $!next            -- 移到后一个元素之后
```

---

## 九、SQL 查询（只读）

```pml
-- PML 内嵌 SQL 查询 E3D 数据库视图
SQL SELECT * FROM E3D_DBA.V_ELEMENT WHERE TYPE = 'PIPE' AND ELEMNAME LIKE '%'
-- ⚠️ 必须写完整 SELECT *，漏了会报语法错误
```

---

## 十、执行 PML 脚本

```pml
-- 通过 $m 命令加载并执行临时文件
$m "C:\path\to\script.pml"

-- 或通过 Command.CreateCommand().RunInPdms()（C# API 端用）
```

---

## 常用模式速查

| 操作 | 正确写法 |
|------|----------|
| 防重复定义 | `Kill !!ToolName` |
| 定义 Form | `Setup Form !!Name ... exit` |
| 定义函数 | `define function !!Name() ... endfunction` |
| 定义方法 | `define method .name() ... endmethod` |
| 成员变量 | `member .var is ARRAY`（在 form 内） |
| 遍历数组 | `DO !val values !array ... ENDDO` |
| 索引循环 | `DO !i index !array` |
| 数值循环 | `DO !index from 1 to N by 1` 或 `DO !a to N` |
| 条件判断 | `if !flag eq 'TRUEA' then` |
| 类型判断 | `type eq 'TYPE'` 或 `type inset('A','B')` |
| 比较函数 | `match(!name,'PAT') gt 0` |
| 获取关联 BRAN | `!bran = bran of $!one` |
| 获取关联 ZONE | `!zone = zone of $!one` |
| 获取关联 SUPPO | `!suppo = suppo of $!one` |
| 兄弟导航 | `!pre = pre` / `!next = next` / `!owner = owner` |
| 读属性 | `!val.Dbref().:ATTR` |
| 写属性 | `!ce.Dbref().:ATTR = 'value'` |
| 链式读 | `!arr[$!i].Dbref().spref.name` |
| 创建标准元素 | `NEW TYPE parent`（PIPE/BRAN/EQUI 等） |
| 创建特殊元素 | `CREATE $P new TYPE FTUB REF ...` |
| 集合查询 | `coll all TYPE for $!scope` |
| 多类型集合 | `coll all (A B C) within volume $!x` |
| 追加集合 | `var !x append coll all TYPE for $!y` |
| 遍历 site 集 | `var !sites coll all site with Matchwild(name,'*PAT*')` |
| 存在检查 | `var !flag exists $!name` → `eq 'TRUEA'` |
| 字符串方法 | `.Before('x')` / `.replace('a','b')` / `.substring(n)` / `.split(',')` |
| 冲突检查 | `DESCLASH` / `OVERRIDE ON` / `OBST $!x` / `CHECK $!y` |
| 错误捕获 | `handle any ... endhandle` / `handle (n,m) ... endhandle` |
| 交互选择 | `id TYPE1 TYPE2 @` |
| 列表设置 | `.dtext = !arr` / `.setRows(!arr)` / `.setHeadings(!str)` |
| 列表读取 | `.selection()` / `.selection('Dtext')` / `.dtext` |
| 程序化选中 | `.Select('Dtext', 'value')` |
| Form 回调 | `callback '!this.method()'` / `call |!this.method()|` |
| 进度条 | `!!fmsys.setProgress(n)` |
| 消息弹窗 | `!!Alert.message('')` / `.error()` / `.warning()` / `.input()` |
| 布局方向 | `path down` / `path right` |
| 文本输入 | `text .name at x y tagwid n width n is string` |
| 调试输出 | `Q VAR !xxx` / `$p '文字'` |
| 元素排序 | `reorder $!el before/after $!ref` |
| 高亮元素 | `enhance $!name col red` |
| 标记元素 | `mark with 'str' ce` |
| 选择集 | `ADD CE` / `auto ce` / `rem all` |
| 输出到文件 | `!file.writefile('write', !array)` |
| 模块导入 | `import 'ModuleName'` / `import |UNC\path.dll|` |
