# DbFilter 过滤器

**命名空间**: `Aveva.Core.Database.Filters`（子命名空间）
**用途**: 创建复杂过滤条件筛选元素

## 关键类

| 类 | 说明 |
|----|------|
| `AndFilter` | 与条件组合 |
| `OrFilter` | 或条件组合 |
| `NotFilter` | 非条件 |
| `AttributeFilter` | 属性条件过滤 |
| `TypeFilter` | 类型过滤 |

## 典型用法

```csharp
using Aveva.Core.Database;
using Aveva.Core.Database.Filters;

// 创建过滤条件：类型为 PIPE 且壁厚为 SCH40
var typeFilter = new TypeFilter("PIPE");
var attrFilter = new AttributeFilter(DbAttributeInstance.WTHK, "SCH40");
var andFilter = new AndFilter(typeFilter, attrFilter);

// 在指定范围内查询
DbElement zone = DbElement.GetElement("ZONE-01");
// 使用过滤器需要结合具体查询方法
```

## 注意事项

- 过滤器通常与特定查询方法配合使用
- 简单查询优先使用 PML 而非 C# 过滤器
