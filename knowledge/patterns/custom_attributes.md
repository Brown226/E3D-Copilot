# 黄金范式：自定义属性读写

**用途**: 读取和写入 E3D 自定义属性（如 :ROOM_NO、:conntray、:3D_SJRY 等）
**验证**: ✅ 来自 RoomCheck / SearchDif / conntray 等真实工具

## 读取自定义属性

```pml
-- 方式1：通过 Dbref（推荐）
!roomNo = !item.Dbref().:ROOM_NO
!conntray = !item.Dbref().:conntray

-- 方式2：通过 var 声明
VAR !chekName :3D_SJRY
$P '设计人员: ', !chekName

-- 方式3：导航到元素后直接读
$!itemName
!desc = :descr
```

## 写入自定义属性

```pml
-- 方式1：通过 Dbref（推荐）
!item.Dbref().:ROOM_NO = 'A101'
!item.Dbref().:conntray = !connectionValue

-- 方式2：导航后直接写
$!itemName
:ROOM_NO 'A101'
:conntray '$!value'

-- 方式3：通过 Attribute 方法
!dbItem.Attribute(':conntray') = !out
```

## 实用模式：属性值读取 + 存在性判断

```pml
-- 读取并判断是否已设置
!roomNo = !element.Dbref().:ROOM_NO
IF Unset(!roomNo) OR !roomNo EQ '' THEN
    $P !element.Name, ' 缺少房间号'
ELSE
    $P !element.Name, ' 房间号: ', !roomNo
ENDIF
```

## C# 等价实现

```csharp
using Aveva.Core.Database;

// 读取自定义属性
DbElement element = DbElement.GetElement("PIPE-001");
string roomNo = element.GetAsString(DbAttributeInstance.Room);
string conntray = element.GetAsString(DbAttributeInstance.GetCustom("conntray"));

// 写自定义属性
element.SetAttribute(DbAttributeInstance.Room, "A101");
```
