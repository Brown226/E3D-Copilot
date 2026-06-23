# ServiceManager 类

**命名空间**: `Aveva.ApplicationFramework`
**用途**: E3D 服务定位器，获取各种系统服务的统一入口

## 获取实例

```csharp
ServiceManager.Instance  // 单例
```

## 关键方法

| 方法 | 签名 | 说明 |
|------|------|------|
| GetService | `object GetService(Type serviceType)` | 获取服务实例 |
| AddService | `void AddService(Type type, object serviceInstance)` | 注册服务 |

## 常用服务获取

```csharp
using Aveva.ApplicationFramework;
using Aveva.ApplicationFramework.Presentation;

// 窗口管理器
var wm = (WindowManager)ServiceManager.Instance.GetService(typeof(WindowManager));

// 命令管理器
var cmdMgr = (CommandManager)ServiceManager.Instance.GetService(typeof(CommandManager));

// 命令栏管理器
var cbMgr = (CommandBarManager)ServiceManager.Instance.GetService(typeof(CommandBarManager));

// 设置管理器
var settings = SettingsManager.Instance;  // 单例，不通过 ServiceManager

// 状态栏
var statusBar = (StatusBar)ServiceManager.Instance.GetService(typeof(StatusBar));
```
