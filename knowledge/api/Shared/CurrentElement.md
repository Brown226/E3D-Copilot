# CurrentElement 当前元素

**命名空间**: `Aveva.Core.Database`（⚠️ 已核实）
**用途**: 获取和监听 E3D 当前选中元素（CE）

## 关键成员

| 成员 | 签名 | 说明 | 已验证 |
|------|------|------|:------:|
| Element | `static DbElement Element { get; set; }` | 获取/设置当前元素 | ✅ |

## 事件

| 事件 | 签名 | 说明 |
|------|------|------|
| CurrentElementChanged | `static event EventHandler CurrentElementChanged` | 当前元素变化时触发 |

## ✅ 正确用法

```csharp
using Aveva.Core.Database;

// 获取当前选中元素
DbElement ce = CurrentElement.Element;

// 设置当前元素
CurrentElement.Element = someElement;

// 监听 CE 变化
CurrentElement.CurrentElementChanged += (sender, args) =>
{
    DbElement newCe = CurrentElement.Element;
    Console.WriteLine($"当前元素已切换为: {newCe.Name}");
};
```

## ❌ 常见错误

```csharp
// ❌ 错误方法（文档写但不存在）
// Db.CurrentSession.CurrentElement

// ✅ 正确方式
DbElement ce = CurrentElement.Element;
```
