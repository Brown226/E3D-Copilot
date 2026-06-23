# DrawList — 绘图列表类

**命名空间**: `Aveva.Core.Graphics`（⚠️ 非 `Aveva.Pdms.Graphics`）
**程序集**: Aveva.Core.Graphics.dll
**用途**: 管理一组待绘制的图形元素，用于批量渲染和视图更新

## 关键方法

| 方法 | 签名 | 说明 |
|------|------|------|
| Add | `void Add(DbElement element)` | 添加单个元素到绘图列表 |
| AddOnly | `void AddOnly(params DbElement[] elements)` | 批量添加（不立即渲染，性能更好） |
| AddReferences | `void AddReferences(DbElement[] elements)` | 添加引用 |
| Clear | `void Clear()` | 清空列表 |
| Refresh | `void Refresh()` | 刷新显示 |
| Exists | `bool Exists(DbElement element)` | 检查元素是否在列表中 |
| RemoveAll | `void RemoveAll()` | 移除所有元素 |
| GetVisualProperties | `VisualProperties GetVisualProperties(DbElement element)` | 获取元素的视觉属性 |

## 典型用法

```csharp
using Aveva.Core.Graphics;
using Aveva.Core.Database;

// 创建绘图列表
DrawList drawList = new DrawList();

// 添加单个元素
DbElement element = DbElement.GetElement("PIPE-001");
drawList.Add(element);

// 批量添加
var elements = new List<DbElement> { el1, el2, el3 };
drawList.AddOnly(elements.ToArray());

// 刷新显示
drawList.Refresh();

// 清空
drawList.Clear();
```

## 应用：高亮显示元素

```csharp
using Aveva.Core.Graphics;
using Aveva.Core.Database;

public class ElementHighlighter
{
    private DrawList _highlightList;

    public ElementHighlighter()
    {
        _highlightList = new DrawList();
    }

    public void HighlightElement(DbElement element)
    {
        _highlightList.Clear();
        _highlightList.Add(element);
        _highlightList.Refresh();
    }

    public void ClearHighlight()
    {
        _highlightList.Clear();
        _highlightList.Refresh();
    }
}
```

## ⚠️ 注意事项

- `DrawList` 的 `Add` 方法会立即触发渲染，批量操作时建议用 `AddOnly` + `Refresh`
- 高亮元素通常通过改变元素颜色属性 + `DrawList.Refresh()` 实现
