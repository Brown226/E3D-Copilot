# 黄金范式：虚拟管嘴连接检查

**用途**: 检查虚拟管嘴连接状态
**验证**: ✅ 来自 VirtualConn 真实工具（完整 Form + Function 实现）

## PML 骨架

```pml
-- 收集指定类型的元素
VAR !LIST COLL ALL <TYPE> WITH MATCH(NAME,|<PATTERN>|) neq 0 FOR $!this.cename

-- 遍历检查连接状态
DO !ELE values !LIST
    !STATUS = !ELE.Dbref().:CONNTRAY  -- 连接状态
    IF !STATUS EQ 'UNCONNECTED' THEN
        $P !ELE.Name, ' 未连接'
    ENDIF
ENDDO

-- 设置连接
!ELE.Dbref().:CONNTRAY = 'CONNECTED'
```

## 真实示例

```pml
-- 来自 VirtualConn.pmlfrm 的查询模式
VAR !MLIST COLL ALL HOLE WITH MATCH(NAME,|$!this.param1txt|) neq 0 FOR $!this.cename
DO !HOLE values !MLIST
    !ST = !HOLE.Dbref().:CONNTRAY
    $P '孔 ', !HOLE.Name, ' 连接状态=', !ST
ENDDO
```
