# PML 命令速查

> PML (Programmable Macro Language) — E3D/PDMS 的可编程宏语言。
> 本文档的每个命令都附有来自**33 个真实 PML 工具**的代码示例。

---

## 集合查询 (Collection)

### 基本语法

```pml
VAR !LIST COLL ALL <TYPE> FOR <SCOPE>
```

### 真实工具示例

```pml
-- 来自 VirtualConn: 按名称匹配收集 HOLE
VAR !MLIST COLL ALL HOLE WITH MATCH(NAME,|$!this.param1txt|) neq 0 FOR $!this.cename

-- 来自 conntray: 收集 ZONE 下所有支架
VAR !suppo COLL ALL SUPPO FOR $!zone

-- 来自 TrayClassifyAdd: 收集 BRAN
VAR !BRANS COLL ALL BRAN FOR $!zoneName

-- 来自 PsecRefresh: 收集 SITE 下所有 ZONE
VAR !ZONES COLL ALL ZONE FOR $!ceName

-- 带 Matchwild 通配符
VAR !LIST COLL ALL PIPE WITH Matchwild(NAME, '*DN100*', true) FOR CE

-- 多重类型收集（conntray: 空间范围内收集多种管件）
VAR !ALL COLL ALL (FTUB ELBO BEND TEE REDU CROS) WITHIN VOLUME $!suppo
```

### EVAL 求值

```pml
-- 来自 TrayClassifyAdd: 提取名称数组
VAR !BRANNAMES EVALUATE NAME FOR ALL BRAN FROM !BRANS

-- 来自 RiserChange: 从收集结果中提取名称
VAR !BRANS APPEND EVAL NAME FOR ALL FROM !BRANS
```

### 对象式查询（Collection 对象）

```pml
-- 来自 CheckName: 使用 Collection 对象替代 coll all
!COLL = OBJECT COLLECTION()
!COLL.type('ZONE')
!COLL.scope(!site.Dbref())
!ZONES = !COLL.results()
```

---

## 遍历循环 (DO values)

### 基本语法

```pml
DO !ELE values !LIST
    $P !ELE.Name
ENDDO
```

### 真实工具示例

```pml
-- 来自 conntray: 遍历支架列表
DO !SUPP values !SUPPOS
    !SUPPNAME = !SUPP.Name
    !SUPP.Dbref().:conntray = !out
ENDDO

-- 来自 CheckName: 带类型判断的遍历
DO !Z values !ZONES
    IF !Z.owner.type EQ 'SITE' THEN
        !Z.Dbref().:ROOM_NO = !autoRoom
    ENDIF
ENDDO

-- 来自 ReWriteElecWidth: 带进度条的批量遍历
DO !INDXI indices !BRANCHES
    !DBBRAN = !BRANCHES[!INDXI].Dbref()
    !!FMSYS.setProgress(!INDXI / !BRANCHES.Size() * 100)
    !DBBRAN.Attribute(':BranWidth') = !width
ENDDO
```

---

## 属性读写

### 基本语法

```pml
-- 读
!WTHK = !ELE.Dbref().:WTHK

-- 写
!ELE.Dbref().:WTHK = 'SCH40'
```

### 真实工具示例

```pml
-- 来自 RoomCheck: 读自定义属性
!ROOM = !ELEMENT.Dbref().:ROOM_NO
IF UNSET(!ROOM) OR !ROOM EQ '' THEN
    $P !ELEMENT.Name, ' 缺房间号'
ENDIF

-- 来自 SearchDif: 通过导航 + 属性简写
$!TEXTNAME
!DESCR = :DESCR          -- 导航后直接读

-- 来自 conntray: 通过 Attribute 方法写
!ITEM.Dbref().Attribute(':conntray') = !connectionValue

-- 来自 CheckName: 通过 var 声明读自定义属性
VAR !CHENAME :3D_SJRY

-- 来自 ReWriteElecWidth: 动态属性名读写
!DBITEM.Attribute(':BranWidth') = !width

-- 来自 PsecRefresh: 引用链式属性访问
!MEMSPEC = !FTUB.Dbref().spref.flnn
!MLIST.Dbref().lstube.flnn
!FTUB.Dbref().owner.pspec
```

---

## 存在检查 (EXISTS)

### 基本语法

```pml
VAR !FLAG EXISTS $!ELEMENT_NAME
IF !FLAG EQ 'TRUEA' THEN
    $P '元素存在'
ENDIF
```

### 真实工具示例

```pml
-- 来自 EquiCheck: 检查设备是否存在
VAR !EQUIEXIST EXISTS $!MODELNAME

-- 来自 SearchDif: 检查 TEXT 是否存在
VAR !DEFINEFLAG EXISTS $!C

-- 来自 conntray: 交互拾取后检查
DO ... ID FTUB TEE BEND CROS GENSEC @
    HANDLE ANY
        BREAK
    ENDHANDLE
ENDDO
```

---

## 条件判断 (IF)

```pml
-- 来自 CheckName: 按项目 ID 分支逻辑
!ID = PROJECT ID
IF !ID EQ '1516' THEN        -- 田湾核电
    ...
ELSEIF !ID EQ '1907' THEN     -- 红沿河核电
    ...
ELSE                           -- 通用规则
    ...
ENDIF

-- 来自 drawConn: 按元素类型分派
IF !TYPE INSET ('EQUI', 'NBOX', 'NCYL') THEN
    !POS = POS OF $!ELEM WRT /*
ELSEIF !TYPE INSET ('FTUB', 'PFIT', 'SBFI') THEN
    !POS = P1 POS OF $!ELEM WRT /*
ENDIF

-- 来自 conntray: 通配符匹配判断
IF Matchwild(DESC OF $!SUPPO[1], '*50*5*') THEN
    !OFFSET = 100 * 2
ENDIF
```

---

## 函数定义 (.pmlfnc)

```pml
-- 来自 ElecInfo: 启动器函数
DEFINE FUNCTION !!ElecInfo()
    IMPORT |\\SERVER\SHARE\DLLPATH\Tool.dll|
    HANDLE ANY
    ENDHANDLE
    USING NAMESPACE |ToolNamespace|
    !OBJ = OBJECT PmlNetCall()
    !OBJ.Start()
ENDFUNCTION

-- 来自 CheckConn: 带参数和返回值的函数
DEFINE FUNCTION !!CheckConn(!textName is STRING) is BOOLEAN
    $!textName
    !TEXDES = :DESCR
    !MODELS = !TEXDES.split(';')
    ...
    RETURN TRUE / FALSE
ENDFUNCTION

-- 来自 drawConn: 带 DBREF 参数的方法
DEFINE METHOD .brJudge(!bran is DBREF) is BOOLEAN
    !TYPE = !bran.Dbref().:TYPE
    ...
ENDMETHOD
```

---

## 错误处理

```pml
-- 来自 EquiCheck: 文件写入错误
HANDLE(41, 322)
    $P '文件写入失败'
ENDHANDLE

-- 来自 ExploreTreeOrder: 元素边界判断
HANDLE (2, 113)      -- 已到达末尾
    $P '没有更多元素'
ENDHANDLE

-- 来自 conntray: 交互拾取错误
DO ... @
    HANDLE ANY
        BREAK
    ENDHANDLE
ENDDO

-- 来自 ElecInfo: DLL 加载错误静默处理
IMPORT |\\PATH\DLL.dll|
HANDLE ANY
ENDHANDLE
```
