# Db 类

**命名空间**: `Aveva.Core.Database`（⚠️ 非 `Aveva.Pdms.Database`）
**用途**: 数据库会话，提供当前数据库状态信息

## 关键属性

| 属性 | 签名 | 说明 |
|------|------|------|
| CurrentSession | `static Db CurrentSession { get; }` | 当前数据库会话 |
| IsValid | `bool IsValid { get; }` | 数据库是否有效（已打开） |
| CurrentElement | `DbElement CurrentElement { get; }` | 当前选中元素（CE） |

## 关键方法

| 方法 | 签名 | 说明 |
|------|------|------|
| SaveWork | `void SaveWork()` | 保存更改 |
| Undo | `void Undo()` | 撤销操作 |
| Redo | `void Redo()` | 重做操作 |

## 典型用法

```csharp
using Aveva.Core.Database;

// 检查数据库状态
Db db = Db.CurrentSession;
if (db == null || !db.IsValid)
{
    // 数据库未打开
    return;
}

// 获取当前元素
DbElement ce = db.CurrentElement;

// 保存更改
db.SaveWork();
```

## ⚠️ 注意事项

- **获取当前元素**：推荐用 `CurrentElement.Element` 而非 `Db.CurrentSession.CurrentElement`
- `CurrentElement` 事件监听：用 `CurrentElementChanged` 事件响应 CE 变化
