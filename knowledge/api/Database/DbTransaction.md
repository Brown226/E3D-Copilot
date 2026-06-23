# DbTransaction 事务管理

**命名空间**: `Aveva.Core.Database`
**用途**: 管理数据库事务，确保批量操作的原子性

## 关键方法

| 方法 | 签名 | 说明 |
|------|------|------|
| Begin | `static void Begin()` | 开始事务 |
| Commit | `static void Commit()` | 提交事务 |
| Rollback | `static void Rollback()` | 回滚事务 |
| InProgress | `static bool InProgress { get; }` | 是否有事务进行中 |

## 典型用法

```csharp
using Aveva.Core.Database;

try
{
    DbTransaction.Begin();

    // 批量修改
    foreach (var element in elementsToModify)
    {
        element.SetAttribute(DbAttributeInstance.WTHK, "SCH40");
        element.SetAttribute(DbAttributeInstance.Spec, "CS150");
    }

    DbTransaction.Commit();
    Db.CurrentSession.SaveWork();
}
catch (Exception ex)
{
    DbTransaction.Rollback();
    // 处理错误
}
```

## 注意事项

- 批量修改时务使用事务，失败可回滚
- 提交事务后调用 `SaveWork()` 持久化
