# WindowManager 类

**命名空间**: `Aveva.ApplicationFramework.Presentation`（✅ 已验证）
**程序集**: Aveva.ApplicationFramework.Presentation.dll
**用途**: 管理 E3D 窗口，创建停靠面板

## 关键方法

| 方法 | 签名 | 说明 |
|------|------|------|
| CreateDockedWindow | `DockedWindow CreateDockedWindow(string key, string title, Control control, DockedPosition position)` | ⚠️ **4 个参数**，含 `DockedPosition` 枚举 |

## DockedPosition 枚举

| 值 | 说明 |
|----|------|
| `DockedPosition.Left` | 左侧停靠 |
| `DockedPosition.Right` | 右侧停靠 |
| `DockedPosition.Top` | 顶部停靠 |
| `DockedPosition.Bottom` | 底部停靠 |
| `DockedPosition.Floating` | 浮动窗口 |

## 获取实例

```csharp
var windowManager = (WindowManager)ServiceManager.Instance
    .GetService(typeof(WindowManager));
```

## 典型用法

```csharp
using Aveva.ApplicationFramework.Presentation;

// 获取窗口管理器
var wm = (WindowManager)ServiceManager.Instance
    .GetService(typeof(WindowManager));

// 创建停靠窗口 — ⚠️ 必须传 4 个参数
var dockedWindow = wm.CreateDockedWindow(
    "MyPanel",           // key
    "我的面板",          // title
    myUserControl,       // control
    DockedPosition.Right // position
);

// ⚠️ 必须调用 Show() 才能显示
dockedWindow.Show();
```

## ⚠️ 常见错误

```csharp
// ❌ 错误：3 个参数（缺 DockedPosition）
var win = wm.CreateDockedWindow("key", "title", control);

// ✅ 正确：4 个参数
var win = wm.CreateDockedWindow("key", "title", control, DockedPosition.Right);

// ❌ 错误：创建后忘记 Show()
// win.Show();  // 必须调用
```
