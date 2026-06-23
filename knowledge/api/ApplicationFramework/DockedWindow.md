# DockedWindow — 停靠窗口类

**命名空间**: `Aveva.ApplicationFramework.Presentation`
**用途**: E3D 内嵌的停靠面板，用于托管自定义 UI（如 WebView2 面板）

## 关键属性

| 属性 | 签名 | 说明 |
|------|------|------|
| Key | `string Key { get; set; }` | 窗口唯一标识 |
| Caption | `string Caption { get; set; }` | 窗口标题 |
| Control | `Control Control { get; set; }` | 窗口内容控件 |
| Visible | `bool Visible { get; }` | 窗口是否可见 |

## 关键方法

| 方法 | 签名 | 说明 |
|------|------|------|
| Show | `void Show()` | 显示窗口 |
| Hide | `void Hide()` | 隐藏窗口 |
| Close | `void Close()` | 关闭窗口 |

## 创建 DockedWindow

⚠️ 通过 `WindowManager.CreateDockedWindow()` 创建，不是直接 new：

```csharp
using Aveva.ApplicationFramework.Presentation;

var wm = (WindowManager)ServiceManager.Instance
    .GetService(typeof(WindowManager));

// ⚠️ 4 个参数，不是 3 个
var dockedWindow = wm.CreateDockedWindow(
    "E3DCopilotPanel",           // key
    "E小智 AI 助手",             // title
    webViewControl,              // control (UserControl)
    DockedPosition.Right         // position
);

// ⚠️ 必须调用 Show() 才会显示
dockedWindow.Show();
```

## DockedPosition 枚举

| 值 | 说明 |
|----|------|
| `DockedPosition.Left` | 左侧 |
| `DockedPosition.Right` | 右侧 |
| `DockedPosition.Top` | 顶部 |
| `DockedPosition.Bottom` | 底部 |
| `DockedPosition.Floating` | 浮动 |

## 自定义 DockedWindow 子类

```csharp
public class MyPanel : DockedWindow
{
    private UserControl _content;

    public MyPanel()
    {
        Key = "MyPanel";
        Caption = "我的面板";
        _content = new MyUserControl();
        Control = _content;
    }
}
```

## ⚠️ 常见错误

```csharp
// ❌ 错误：3 个参数
var win = wm.CreateDockedWindow("key", "title", control);

// ✅ 正确：4 个参数
var win = wm.CreateDockedWindow("key", "title", control, DockedPosition.Right);

// ❌ 错误：忘记 Show()
// win 不会自动显示
```
