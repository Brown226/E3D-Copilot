# 黄金范式：批量重命名

**用途**: 批量修改元素名称
**验证**: ✅ 来自 mrename.pmlfrm 真实工具

## PML 骨架

```pml
-- 收集元素
VAR !LIST COLL ALL <TYPE> WITH MATCH(NAME,|<OLD_PATTERN>|) neq 0 FOR <SCOPE>

-- 遍历重命名
DO !ELE values !LIST
    !OLDNAME = !ELE.Name
    !NEWNAME = !OLDNAME  -- 执行名称变换逻辑
    !ELE.Dbref().:NAME = '!NEWNAME'
ENDDO
```

## 真实示例

```pml
-- 来自 mrename.pmlfrm
-- 将所有匹配的元素加上前缀
VAR !LIST COLL ALL EQUI FOR CE
DO !ELE values !LIST
    !OLD = !ELE.Name
    !NEW = 'NEW-' + !OLD
    !ELE.Dbref().:NAME = $!NEW
ENDDO
```
