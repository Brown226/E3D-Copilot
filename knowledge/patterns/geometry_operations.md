# 黄金范式：几何操作（位置/端口/偏移）

**用途**: 读写元素位置、端口点、坐标偏移和线段绘制
**验证**: ✅ 来自 ddzcablewaysupporteditor / drawConn / RotateElecSuppo 等真实工具
**preferred_tool**: `geometry` + `calculate` — 读坐标用 geometry，数学计算用 calculate

## 位置读取

```pml
-- 绝对坐标
!pos = pos of $!element wrt /*
!east = !pos.East
!north = !pos.North
!up = !pos.Up

-- 起始点 / 终止点
!poss = poss of $!section wrt /*
!pose = pose of $!section wrt /*

-- 端口位置
!p1pos = p1 pos of $!element wrt /*
!p2pos = p2 pos of $!element wrt /*
!p3pos = p3 pos of $!element wrt /*
!p4pos = p4 pos of $!element wrt /*
```

## 坐标偏移

```pml
-- 沿方向偏移
!newPos = !startPos.Offset(!direction, !distance)
poss $!newPos       -- 设置起始位置
pose $!newPos       -- 设置终止位置

-- 移动元素
move $!dir dist $!dist
```

## 距离计算

```pml
-- 两点距离
!dist = !pos1.Distance(!pos2)

-- 最近点匹配
!min = !posList[1].Distance(!targetPos)
DO !i FROM 1 TO !posList.Size() BY 1
    !d = !posList[!i].Distance(!targetPos)
    IF !d LT !min THEN
        !min = !d
        !nearest = !posList[!i]
    ENDIF
ENDDO
```

## 旋转

```pml
-- 绕点旋转
!pos = !element.Dbref().worpos
!dir = !element.Dbref().ori.zdir()
ROTATE THROUGH $!pos ABOUT $!dir BY $!angle
```

## 绘制连接线

```pml
-- 创建并绘制直线
!line = OBJECT LINE(!startPos, !endPos)
!line.draw($!ident, 1, 2)

-- 添加标注文字
AID TEXT NUM $!ident '$!ident' AT $!midPos
```

## 线面相交计算

```pml
!poss = POSS of $!section WRT /*
!pose = POSE of $!section WRT /*
!line = OBJECT Line(!poss, !pose)
!plane = OBJECT PLANE(...)
!intersect = !plane.Intersection(!line)
```

## C# 等价实现

```csharp
using Aveva.Core.Database;
using Aveva.Core.Geometry;

// 获取位置
DbElement element = DbElement.GetElement("PIPE-001");
Position pos = element.GetPosition(DbAttributeInstance.WorldPosition);

// 偏移
Direction dir = new Direction(1, 0, 0);
// Position 没有 Offset 方法，需手动计算
Position newPos = new Position(
    pos.East + dir.X * distance,
    pos.North + dir.Y * distance,
    pos.Up + dir.Z * distance
);

// 距离
double dist = pos1.DistanceTo(pos2);
```
