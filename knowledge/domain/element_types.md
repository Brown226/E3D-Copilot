# E3D 元素类型对照表

> 元素类型用于 PML `coll all TYPE` 和 C# `DbElement.GetElement()` 查询。

## 完整元素类型列表

| PML 类型 | 中文 | 说明 | 父类型 | 典型用途 |
|----------|------|------|--------|---------|
| SITE | 项目/场地 | 数据库根节点，最顶层容器 | (none) | 项目入口 |
| ZONE | 区域 | 设计区域容器 | SITE | 按功能分区（管道区/设备区） |
| PIPE | 管道 | 管道主体，含多个 BRAN | ZONE | 流体输送 |
| BRAN | 分支 | 管道的一段，含 FTUB/BEND/TEE | PIPE | 管线分段 |
| FTUB | 管件 | 直管/管段 | BRAN | 直管道 |
| BEND | 弯头 | 弯管/弯头 | BRAN | 管道转向 |
| TEE | 三通 | 三通/分支连接件 | BRAN | 管道分支 |
| EQUI | 设备 | 设备主体 | ZONE | 泵/罐/换热器/塔 |
| COMP | 部件 | 设备的一部分 | EQUI | 设备组件 |
| NOZZ | 管嘴 | 设备/管道的连接点 | EQUI/PIPE | 接入口 |
| VALV | 阀门 | 阀门 | BRAN | 控制流体 |
| STRU | 结构 | 结构件 | ZONE | 钢架/支架/平台 |
| CABLE | 电缆 | 电缆通道 | ZONE | 电气 |
| TRAY | 桥架 | 电缆桥架 | ZONE | 电缆敷设 |
| HOLE | 开孔 | 孔洞/贯穿 | STRU | 结构开孔 |
| SUPP | 支吊架 | 管道支撑 | ZONE | 管道支撑 |

## C# 中获取元素类型

```csharp
using Aveva.Core.Database;

// 按名称查找元素
DbElement element = DbElement.GetElement("PIPE-001");

// 获取元素类型名称
string typeName = element.GetAsString(DbAttributeInstance.Type);

// 判断元素类型
DbElementType type = element.ElementType;
```

## PML 中类型判断

```pml
!ce = CURRENT CE
-- 获取类型名
$P !ce.Name, ' type=', !ce.Dbref().:TYPE

-- 类型比较
IF !ce.Dbref().:TYPE eq 'PIPE' THEN
    $P '这是一个管道'
ENDIF
```
