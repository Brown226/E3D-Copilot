# CommandBarManager — 工具栏管理器

**命名空间**: `Aveva.ApplicationFramework.Presentation`
**用途**: 管理 E3D 工具栏/菜单栏的创建和注册

## 获取实例

```csharp
var cbManager = (CommandBarManager)ServiceManager.Instance
    .GetService(typeof(CommandBarManager));
```

## 关键属性

| 属性 | 说明 |
|------|------|
| `CommandBars` | 工具栏集合 |

## CommandBar 工具栏

```csharp
// 创建工具栏
var toolbar = new CommandBar("MyToolbar", "我的工具栏");
cbManager.CommandBars.Add(toolbar);

// 创建按钮
var button = new ButtonTool("MyButton");
button.Caption = "执行";
button.Tooltip = "点击执行操作";
button.Command = command;  // 关联 Command

// 添加到工具栏
toolbar.Tools.Add(button);
```

## StatusBar — 状态栏

**命名空间**: `Aveva.ApplicationFramework.Presentation`
**用途**: 更新 E3D 底部状态栏信息

```csharp
var statusBar = (StatusBar)ServiceManager.Instance
    .GetService(typeof(StatusBar));

// 创建状态栏面板
var panel = new StatusBarTextPanel();
panel.Text = "就绪";
statusBar.Panels.Add(panel);

// 更新状态
panel.Text = "正在处理...";
```

## ResourceManager — 资源管理

**命名空间**: `Aveva.ApplicationFramework.Presentation`
**用途**: 多语言国际化支持

```csharp
var resourceManager = (ResourceManager)ServiceManager.Instance
    .GetService(typeof(ResourceManager));
string localizedText = resourceManager.GetString("MyResourceKey");
```
