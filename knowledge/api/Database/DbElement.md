# DbElement 类

**命名空间**: `Aveva.Core.Database`（⚠️ 非文档写的 `Aveva.Pdms.Database`）
**程序集**: Aveva.Core.Database.dll
**用途**: PDMS/E3D 数据库元素基类，所有模型对象（PIPE/EQUI/STRU 等）的基类

## 关键静态方法

| 方法 | 签名 | 说明 | 已验证 |
|------|------|------|:------:|
| GetElement | `static DbElement GetElement(string name)` | 按名称查找元素 | ✅ |
| Invalid | `static DbElement Invalid { get; }` | 无效元素（判断用） | ✅ |

## 关键实例方法

| 方法 | 签名 | 说明 | 已验证 |
|------|------|------|:------:|
| GetAsString | `string GetAsString(DbAttribute attr)` | 获取属性值（字符串） | ✅ |
| GetAsDouble | `double GetAsDouble(DbAttribute attr)` | 获取属性值（浮点数） | ✅ |
| GetAsInt | `int GetAsInt(DbAttribute attr)` | 获取属性值（整数） | ✅ |
| GetAttribute | `DbAttributeValue GetAttribute(DbAttribute attr)` | 获取属性值对象 | 🟡 签名正确但返回值是对象 |
| SetAttribute | `void SetAttribute(DbAttribute attr, object value)` | 设置属性值 | ✅ |
| GetPosition | `Position GetPosition(DbAttribute attr)` | 获取位置属性 | ✅ |
| FirstMember | `DbElement FirstMember()` | 第一个子元素 | ✅ |
| NextMember | `DbElement NextMember(DbElement current)` | 下一个子元素 | ✅ |
| Owner | `DbElement Owner()` | 父元素 | ✅ |
| AddNew | `DbElement AddNew(string elementType)` | 创建子元素 | ✅ |
| Delete | `void Delete()` | 删除元素 | ✅ |

## 关键属性

| 属性 | 签名 | 说明 |
|------|------|------|
| Name | `string Name { get; }` | 元素名称 |
| IsValid | `bool IsValid { get; }` | 元素是否有效 |
| ElementType | `DbElementType ElementType { get; }` | 元素类型 |

## ✅ 正确用法

```csharp
using Aveva.Core.Database;  // 注意命名空间！

// 按名称查找
DbElement element = DbElement.GetElement("PIPE-001");

// 读取属性 — ✅ 推荐方式
string name = element.Name;
string wthk = element.GetAsString(DbAttributeInstance.WTHK);
double bore = element.GetAsDouble(DbAttributeInstance.Bore);
string spec = element.GetAsString(DbAttributeInstance.Spec);

// 设置属性
element.SetAttribute(DbAttributeInstance.WTHK, "SCH40");

// 遍历子元素
DbElement child = element.FirstMember();
while (child.IsValid)
{
    Console.WriteLine(child.Name);
    child = element.NextMember(child);
}

// 获取父元素
DbElement parent = element.Owner();

// 获取位置
Position pos = element.GetPosition(DbAttributeInstance.WorldPosition);
double east = pos.East;
double north = pos.North;
double up = pos.Up;
```

## ❌ 常见错误写法

```csharp
// ❌ 命名空间错了
using Aveva.Pdms.Database;  // 不存在！

// ❌ GetAttribute 参数用字符串
element.GetAttribute("WTHK");  // 参数应为 DbAttribute 对象

// ❌ 属性直接在元素上遍历
Db.CurrentSession.CurrentElement;  // 应该用 CurrentElement.Element
```

## 注意事项

- **命名空间**：实际是 `Aveva.Core.Database`，文档写的是 `Aveva.Pdms.Database`
- **属性访问**：用 `GetAsString(DbAttributeInstance.XXX)`，不是 `GetAttribute("XXX").ToString()`
- **当前元素**：用 `CurrentElement.Element` 而非 `Db.CurrentSession.CurrentElement`
