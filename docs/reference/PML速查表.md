# E小智 v1.0 — PML 语法速查表

> AI 生成 PML 代码时的语法参考，覆盖 90%+ 的工具场景。

---

## 一、变量与赋值

```pml
-- 字符串
!name = 'PIPE-001'
!desc = |这是一个描述|        -- 竖线语法，支持特殊字符

-- 数字
!count = 0
!size = !arr.size()

-- 数组
!arr = ARRAY()
!arr.append('item1')
!arr.append('item2')
$p {!arr.size()}              -- 输出: 2

-- 布尔
!flag = TRUE
!flag = FALSE
```

---

## 二、元素导航

```pml
-- 导航到元素（设为当前 CE）
$!PIPE-001                    -- 导航到 PIPE-001
$!ZONE-01                     -- 导航到 ZONE-01

-- 获取当前元素
!ce                           -- 当前元素
fullname                      -- 完整路径
name                          -- 名称
type                          -- 类型

-- 父元素
owner                         -- 所有者
zone of $!elem                -- 所属 Zone
site of $!elem                -- 所属 Site

-- 子元素
!!ce.mem                      -- CE 的所有成员
!elem.mem                     -- 指定元素的成员
!elem.member.size()           -- 成员数量
```

---

## 三、集合查询（最高频）

```pml
-- 基本查询
var !pipes coll all PIPE                              -- 所有管道
var !pipes coll all PIPE for $!ZONE-01                -- ZONE-01 下的管道
var !pipes coll all PIPE for CE                       -- CE 下的管道

-- 名称匹配（Matchwild 支持 * 通配符）
var !pipes coll all PIPE with Matchwild(name,'*DN100*')
var !equips coll all EQUI with Matchwild(name,'PUMP*')

-- 属性匹配
var !pipes coll all PIPE with (:DIA = '100')
var !pipes coll all PIPE with (:SPEC = 'CS300' AND :DIA = '100')

-- 组合查询
var !fitments coll all (FTUB BEND TEE CROS ELBO) for $!BRAN-01

-- 追加查询（在已有集合上添加）
var !sites coll all site with Matchwild(name,'*PIPE*')
var !sites append coll all site with Matchwild(name,'*HVAC*')
var !sites append coll all site with Matchwild(name,'*ELEC*')
```

---

## 四、遍历与条件

```pml
-- 遍历集合
DO !p values !pipes
    $!p                         -- 导航到每个元素
    !p.name                     -- 访问属性
    !p.type                     -- 类型
enddo

-- 条件判断
if !flag eq 'TRUEA' then
    -- 存在
endif

if !spec eq 'FALSEA' then
    -- 不存在
endif

if !count gt 0 then
    -- 大于 0
endif

if !type inset ('PIPE','BRAN') then
    -- 类型在列表中
endif

-- 循环计数
!count = 0
DO !p values !pipes
    !count = !count + 1
enddo
$p 共 {!count} 个元素
```

---

## 五、属性操作

```pml
-- 导航并读取属性
$!PIPE-001
!dia = !ce.Dbref().:DIA          -- 通过 CE 读取
!spec = !ce.Dbref().:SPEC

-- 通过变量读取（在遍历中更常用）
var !pipes coll all PIPE for CE
DO !p values !pipes
    !dia = !p.:DIA               -- 变量直接读取
    !spec = !p.:SPEC
enddo

-- 写入属性（通过 CE）
$!PIPE-001
!ce.Dbref().:DIA = '150'
!ce.Dbref().:SPEC = 'CS300'

-- 写入属性（通过变量，遍历中更常用）
DO !p values !pipes
    !p.:WTHK = 'SCH40'
enddo

-- 自定义属性（冒号前缀）
!elem.:ROOM_NO = 'RM-101'
!elem.:conntray = 'CT-001'

-- 检查属性是否存在
var !spec exist :SPEC
if !spec eq 'TRUEA' then
    $p SPEC 存在
endif
```

---

## 六、位置与几何（POSITION / DIRECTION / ORIENTATION）

### POSITION 对象

```pml
-- 创建位置
!pos = POSITION('1000 2000 3000')           -- 从字符串创建
!pos = POSITION('1000 2000 3000', !fmt)     -- 指定格式

-- 成员（注意：是 East/North/Up，不是 E/N/U）
!pos.East = 1000                             -- 东坐标
!pos.North = 2000                            -- 北坐标
!pos.Up = 3000                               -- 高坐标
!pos.Origin = !!ce                           -- 原点参考元素

-- 读取元素位置
!pos = !!ce.Position                         -- 当前元素位置
!pos = !elem.Pposition[3]                    -- 第 3 个 P-point 位置
!dir = !elem.Pdirection[1]                   -- 第 1 个 P-point 方向

-- 坐标转换
!newPos = !pos.WRT(!!ce)                     -- 相对于某元素转换坐标
!angle = !pos.Angle(!posA, !posB)            -- 两点夹角
!comp = !pos.Component(!dir)                 -- 在指定方向上的分量

-- 字符串输出
!str = !pos.String(!fmt)                     -- 按格式输出坐标字符串
```

### DIRECTION 对象

```pml
!dir = DIRECTION('N')                        -- 从字符串创建方向
!dir.East = 1.0                              -- 东分量
!dir.North = 0.0                             -- 北分量
!dir.Up = 0.0                                -- 高分量
```

### ORIENTATION 对象

```pml
!orient = ORIENTATION('N 0 0 0')             -- 从字符串创建朝向
```

### 几何计算常用方法

```pml
-- 两点距离
!dist = !posA.Distance(!posB)                -- 返回 REAL

-- 中点
!mid = !posA.Mid(!posB)                      -- 返回 POSITION

-- 方向向量
!dir = !posA.Direction(!posB)                -- 返回 DIRECTION

-- 圆弧构造
!arc = !posX.ArcCentre(!posA, !posB, !dir, !radius)   -- 圆心法
!arc = !posX.ArcFillet(!posA, !posB, !dir, !radius)   -- 圆角法
!arc = !posX.ArcRadius(!posA, !posB, !dir, !radius, FALSE)  -- 半径法
!arc = !posX.ArcThru(!posA, !posB, !dir)               -- 三点法

-- 线与平面
!line = LINE(!posA, !posB)                   -- 创建线
!plane = PLANE(!posA, !posB, !posC)          -- 创建平面
!intersect = !line1.Intersect(!line2)        -- 线线交点
!proj = !pos.Project(!plane)                 -- 点在平面上的投影
```

---

## 七、存在性检查

```pml
-- 检查元素是否存在
var !flag exist $!PIPE-001
if !flag eq 'TRUEA' then
    $p PIPE-001 存在
else
    $p PIPE-001 不存在
endif

-- 检查属性是否存在
var !attr exist :ATTR_NAME
```

---

## 八、集合查询进阶（COLLECTION 对象）

除了简写语法 `var !x coll all TYPE`，PML 2 提供完整的 COLLECTION 对象 API：

```pml
-- 构造
!coll = COLLECTION()

-- 设置查询类型
!coll.Type('PIPE')               -- 单类型
!coll.AddType('BRAN')            -- 追加类型
!coll.ClearTypes()               -- 清空类型

-- 设置范围
!coll.Scope(!!ce)                -- 以 CE 为范围
!coll.Scope(!dbref)              -- 以 DBREF 为范围
!coll.AddScope(!dbref)           -- 追加范围
!coll.ClearScope()               -- 清空范围

-- 设置过滤器（EXPRESSION 对象）
!expr = EXPRESSION(":DIA > '100' AND :SPEC = 'CS300'")
!coll.Filter(!expr)
!coll.ClearFilter()

-- 执行查询
!coll.Initialise()               -- 重新求值
!results = !coll.Results()       -- 获取全部结果（DBREF 数组）
!count = !coll.Size()            -- 结果数量
!next = !coll.Next(10)           -- 分批获取 10 个

-- 简写语法（等价，更简洁）
var !pipes coll all PIPE with (:DIA > '100') for $!ZONE-01
```

---

## 九、报表输出（REPORT 对象）

PML 原生报表引擎，无需外部库：

```pml
-- 创建表
!table = TABLE()
!col1 = COLUMN(!expr1, TRUE, FALSE, 'Name')
!table.AddColumn(!col1)

-- 创建报表
!report = REPORT(!table)
!report.AddColumn('Name', !colFormat, '名称')

-- 执行报表
!dtext = ARRAY()
!rtext = ARRAY()
!ok = !report.Results(!dtext, !rtext)

-- 输出到文件
!file = FILE('report.txt')
!file.Open('WRITE')
!file.WriteLine('报表内容')
!file.Close()
```

---

## 十、输出与调试

```pml
-- 输出到消息窗口
$p Hello World
$p 管道数量: {!pipes.size()}

-- 输出元素信息
$p {!p.name} | type={!p.type} | dia={!p.:DIA}

-- 输出数组内容
DO !item values !arr
    $p   - {!item}
enddo
```

---

## 十一、文件操作

```pml
-- 写入文件
writefile 'output.txt' '内容'

-- 读取文件（通过 PMLFileBrowser）
import 'PMLFileBrowser'
!!filebrowser('d:\','*xls*','',true,'!filePath = !!filebrowser.file.name()')
```

---

## 十二、异常处理

```pml
-- 捕获所有异常
handle any
    $p 错误: {!error}
endhandle

-- 捕获特定错误码
handle (2,113)
    $p 元素不存在
endhandle

-- 忽略错误
handle any
endhandle
```

---

## 十三、常用元素类型

| 缩写 | 全称 | 说明 |
|------|------|------|
| SITE | Site | 站点 |
| ZONE | Zone | 区域 |
| EQUI | Equipment | 设备 |
| PIPE | Pipe | 管道 |
| BRAN | Branch | 分支 |
| VALV | Valve | 阀门 |
| STRU | Structure | 结构 |
| FTUB | Straight Tubing | 直管段 |
| BEND | Bend | 弯头 |
| TEE | Tee | 三通 |
| CROS | Cross | 四通 |
| ELBO | Elbow | 弯管 |
| NOZZ | Nozzle | 管嘴 |
| PCOM | Piping Component | 管道组件 |
| INST | Instrument | 仪表 |
| DAMP | Damper | 风阀 |
| BATT | Battery | 电池（电气） |
| TEXT | Text | 文本注释 |

---

## 十四、常用属性名

| 属性 | 说明 | 示例值 |
|------|------|--------|
| DIA | 管径 | "100", "DN100" |
| WTHK | 壁厚 | "SCH30", "SCH40" |
| SPEC | 等级 | "CS300", "SS316" |
| FLUID | 介质 | "WATER", "STEAM" |
| ROOM_NO | 房间号 | "RM-101" |
| descr | 描述 | 自由文本 |
| pos | 位置 | 坐标字符串（PML 中用 POSITION 对象操作） |

---

## 十五、PML 黄金模板

### 模板 1：查询并输出

```pml
var !results coll all {TYPE} with Matchwild(name,'{PATTERN}') for $!{SCOPE}
DO !r values !results
    $p {!r.name} | type={!r.type} | {ATTR1}={!r.:{ATTR1}} | {ATTR2}={!r.:{ATTR2}}
enddo
$p 共 {!results.size()} 个元素
```

### 模板 2：批量修改

```pml
var !items coll all {TYPE} with Matchwild(name,'{PATTERN}') for $!{SCOPE}
!count = 0
DO !item values !items
    $!item
    !item.:{ATTR} = '{VALUE}'
    !count = !count + 1
enddo
$p 已修改 {!count} 个元素
```

### 模板 3：属性检查

```pml
var !items coll all {TYPE} for $!{SCOPE}
var !issues = ARRAY()
DO !item values !items
    $!item
    var !attr exist :{ATTR}
    if !attr eq 'FALSEA' then
        !issues.append(!item.name)
    endif
enddo
$p 有问题的元素: {!issues.size()} 个
DO !i values !issues
    $p   - {!i}
enddo
```

### 模板 4：存在性检查

```pml
var !flag exist $!{ELEMENT_NAME}
if !flag eq 'TRUEA' then
    $p {ELEMENT_NAME} 存在
else
    $p {ELEMENT_NAME} 不存在
endif
```
