# Clipboard — 剪贴板操作

**命名空间**: `Aveva.Core.Shared`（⚠️ 非 `Aveva.Pdms.Shared`）
**用途**: 复制/粘贴 E3D 元素

## 关键方法

| 方法 | 签名 | 说明 |
|------|------|------|
| Copy | `static void Copy(DbElement element)` | 复制元素到剪贴板 |
| Cut | `static void Cut(DbElement element)` | 剪切元素到剪贴板 |
| Paste | `static DbElement Paste(DbElement target)` | 粘贴到指定目标 |

## 典型用法

```csharp
using Aveva.Core.Shared;
using Aveva.Core.Database;

// 复制元素
DbElement source = DbElement.GetElement("PIPE-001");
Clipboard.Copy(source);

// 粘贴到目标下
DbElement target = DbElement.GetElement("ZONE-01");
DbElement newElement = Clipboard.Paste(target);

// 剪切
Clipboard.Cut(source);
```
