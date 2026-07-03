---
name: aveva-pml-language
description: AVEVA E3D/PDMS PML 编程语言完整语法参考。基于工业标准 .pmlfrm/.pmlfnc 文件提取的正确写法。写 PML 前先调用此 skill。
runAs: inline
tags: [PML, E3D, 语法, 参考]
---

# PML 语言完整参考

> 基于 CNPE 工业级 PML 工具文件（conntray/EquiCheck/ExploreTreeOrder 等）提取的正确写法。

---

## 一、变量系统

### 1.1 变量类型

| 类型 | 声明 | 示例 |
|------|------|------|
| 局部变量 | `!name` | `!count = 0` |
| 全局变量 | `!!name` | `!!headsString = '列1,列2'` |
| 系统变量 | `$!name` | `$!var` 导航到元素 |
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

---

## 二、控制逻辑

### 2.1 条件判断

```pml
if !flag eq 'TRUEA' then
    ...
else
    ...
endif

if type inset('FTUB','TEE','BEND') then    -- 类型判断
    ...
endif
```

### 2.2 循环

```pml
-- 遍历数组
DO !val values !array
    ...
ENDDO

-- 遍历索引
DO !i index !array
    !array[$!i].Dbref()
ENDDO

-- 计数循环
DO !index from 1 to !array.size() by 1
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
    skip
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
$P var = DB ELEMENT 'NAME'             -- 按名称引用
$!NAME                                  -- 导航到元素（设置 CE）
```

### 3.2 元素创建

```pml
-- 标准类型
$P parent = DB ELEMENT 'PARENT'        -- 引用父元素
$P new = NEW PIPE parent               -- NEW + 类型名（PIPE/BRAN/EQUI/STRU/SUPPO/GENSEC 等）

-- 特殊类型（FTUB/FMTG/SPCON 等 NEW 不支持）
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
-- 读属性
!val = !ce.Dbref().:ATTR               -- 格式：!var.Dbref().:属性名
!val = !suppo.Dbref().:conntray

-- 写属性
!ce.Dbref().:ATTR = 'value'
!suppo.Dbref().:conntray = !new

-- 获取完整路径名
!fullName = fullname of $!ca
```

### 3.6 存在性检查

```pml
var !flag exists $!name                -- 检查元素是否存在
if !flag eq 'TRUEA' then               -- 返回 'TRUEA'/'FALSEA'（注意是字符串）
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

-- 多类型查询
var !all coll all (FTUB ELBO BEND TEE REDU CROS) within volume $!suppo

-- 追加集合（扩展已有集合）
var !allEqui append coll all EQUI FOR $!SITE
var !allEqui append coll all VALV FOR $!SITE
var !allEqui append coll all PCOM FOR $!SITE
```

### 4.2 OO 方式

```pml
!coll = COLLECTION()
!coll.Type('PIPE')
!coll.Scope(!!ce)
!coll.Filter(!expr)
!results = !coll.Results()
```

---

## 五、UI / Form 操作

### 5.1 Form 定义

```pml
setup form !!FormName                  -- 定义 Form
    title '工具标题'
    !this.initcall = '!this.init()'     -- 初始化回调

    FRAME .frame1 '标签' at x0 ymax
        list .list1 at x1 ymin+0.5 width 20 length 15 callback '!this.method()'
        button .btn1 '按钮' at xmax+1 ymin width 6 call '!this.action()'
        path down
        button .btn2 '清除' callback '!this.clear()' width 6
    exit

    member .varName is ARRAY            -- 成员变量
    member .flag is BOOLEAN
exit
```

### 5.2 Tabset 多页签

```pml
setup form !!FormName RESIZE
    frame .Tabset TABSET 'tabset' at x0.3 y0.15 width 35 anchor all
        frame .tab1 '页签1' at x0 ymax
            ...
        exit
        frame .tab2 '页签2' at x0 ymax
            ...
        exit
    exit
exit
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

### 5.4 Form 交互

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

handle (41,322)                          -- 文件操作错误
    !!alert.warning('写入失败')
    return
endhandle
```

### 6.2 .NET DLL 调用

```pml
import |\\server\path\To\DotNet.dll|    -- 加载 DLL
handle any
endhandle
using namespace |Namespace.Name|
!obj = Object ClassName()
!obj.Start()
```

### 6.3 Excel/CSV 导入

```pml
import 'PMLFileBrowser'
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
!str.substring(2)                        -- 截取
!str.split(',')                          -- 分割
Matchwild(!str, '*PAT*')                 -- 通配符匹配

!ce.Name                                  -- 获取元素名称
!ce.Dbref().flnn                          -- 获取元素完整显示名
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

-- 或通过 Command.CreateCommand().RunInPdms()（C# 端用）
```

---

## 常用模式速查

| 操作 | 正确写法 |
|------|----------|
| 遍历数组 | `DO !val values !array ... ENDDO` |
| 索引循环 | `DO !i index !array` |
| 数值循环 | `DO !index from 1 to N by 1` |
| 获取关联 BRAN | `!bran = bran of $!one` |
| 获取关联 ZONE | `!zone = zone of $!one` |
| 读属性 | `!val.Dbref().:ATTR` |
| 写属性 | `!ce.Dbref().:ATTR = 'value'` |
| 创建标准元素 | `NEW TYPE parent` |
| 创建特殊元素 | `CREATE $P new TYPE FTUB REF ...` |
| 集合查询 | `coll all TYPE for $!scope` |
| 多类型集合 | `coll all (A B C) within volume $!x` |
| 追加集合 | `var !x append coll all TYPE for $!y` |
| 存在检查 | `var !flag exists $!name` |
| 类型判断 | `type inset('FTUB','ELBO')` |
| 错误捕获 | `handle (n,m) ... endhandle` |
| 交互选择 | `id TYPE1 TYPE2 @` |
| Form 消息 | `!!Alert.message('文字')` |
| 控制台输出 | `$p '文字'` |
| 元素排序 | `reorder $!el before/after $!ref` |
| 高亮元素 | `enhance $!name col red` |
