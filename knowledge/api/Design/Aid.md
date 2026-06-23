# Design 模块 — Aid 类

**命名空间**: `Aveva.Core.Design`（⚠️ 非 `Aveva.Pdms.Design`）
**用途**: 设计辅助功能

## 关键方法

| 方法 | 签名 | 说明 |
|------|------|------|
| ClearPipeProductionTags | `static void ClearPipeProductionTags()` | 清除管道生产标签 |
| UnenhanceAll | `static void UnenhanceAll()` | 取消所有增强显示 |

## 典型用法

```csharp
using Aveva.Core.Design;

// 清理生产标签
Aid.ClearPipeProductionTags();
```

## 设计元素创建

大部分设计操作通过 `DbElement.AddNew(type)` 完成，而非 Design 模块特有 API：

```csharp
using Aveva.Core.Database;

// 创建设备
DbElement zone = DbElement.GetElement("ZONE-01");
DbElement equip = zone.AddNew("EQUI");
equip.SetAttribute(DbAttributeInstance.Name, "PUMP-001");

// 添加管嘴
DbElement nozzle = equip.AddNew("NOZZ");
nozzle.SetAttribute(DbAttributeInstance.Name, "NOZZ-A1");

// 创建结构
DbElement stru = zone.AddNew("STRU");
stru.SetAttribute(DbAttributeInstance.Name, "STEEL-001");
```

## 常见元素类型

| 类型 | 说明 | 父类型 |
|------|------|--------|
| EQUI | 设备 | ZONE |
| COMP | 组件 | EQUI |
| NOZZ | 管嘴 | EQUI/PIPE |
| STRU | 结构 | ZONE |
| PRIM | 图元（BOX/CYLI/SPHE 等） | COMP |
