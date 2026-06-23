# 黄金范式：元素类型判断与分派

**用途**: 根据元素类型进行不同处理逻辑
**验证**: ✅ 来自 drawConn / CheckName / ReWriteElecWidth 真实工具

## TYPE 判断

```pml
!type = !ele.Dbref().:TYPE

-- 多类型匹配（inset 语法）
IF !type INSET ('EQUI', 'NBOX', 'NCYL') THEN
    $P '这是容器类元素'
ELSEIF !type INSET ('FTUB', 'PFIT', 'SBFI') THEN
    $P '这是管段类元素'
ELSEIF !type EQ 'TEE' THEN
    $P '这是三通'
ELSE
    $P '其他类型: ', !type
ENDIF
```

## 按类型分派获取端口位置

```pml
-- drawConn 中的经典模式
IF !type INSET ('EQUI', 'NBOX', 'NCYL', 'JLDATU', 'FIXING') THEN
    !papos.APPEND(pos of $!elem wrt /*)
ELSEIF !type INSET ('FTUB', 'PFIT', 'SBFI', 'CMPF', 'FITT') THEN
    !papos.APPEND(p1 pos of $!elem wrt /*)
    !papos.APPEND(p2 pos of $!elem wrt /*)
ELSEIF !type EQ 'BEND' THEN
    !papos.APPEND(p1 pos of $!elem wrt /*)
    !papos.APPEND(p2 pos of $!elem wrt /*)
ELSEIF !type INSET ('TEE', 'REDU') THEN
    !papos.APPEND(p1 pos of $!elem wrt /*)
    !papos.APPEND(p2 pos of $!elem wrt /*)
    !papos.APPEND(p3 pos of $!elem wrt /*)
ELSEIF !type INSET ('CROS') THEN
    !papos.APPEND(p1 pos of ...)
    !papos.APPEND(p2 pos of ...)
    !papos.APPEND(p3 pos of ...)
    !papos.APPEND(p4 pos of ...)
ENDIF
```

## C# 等价实现

```csharp
using Aveva.Core.Database;

string type = element.GetAsString(DbAttributeInstance.Type);

switch (type)
{
    case "EQUI":
    case "NBOX":
    case "NCYL":
        // 容器类处理
        break;
    case "FTUB":
    case "PFIT":
        // 管段类处理
        break;
    case "TEE":
        // 三通处理
        break;
}
```
