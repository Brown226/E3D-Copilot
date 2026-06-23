# Command 命令系统

> E3D 采用命令模式分离 UI 和业务逻辑

## 关键类

| 类 | 类型 | 说明 |
|----|------|------|
| `Command` | abstract | 命令基类 |
| `CommandExecutor` | abstract | 命令执行器 |
| `CommandManager` | 服务 | 命令注册和管理 |
| `ButtonTool` | class | 按钮 UI 组件 |

## Command 类

**命名空间**: `Aveva.ApplicationFramework.Presentation`

```csharp
public class MyCommand : Command
{
    public MyCommand() : base("MyCommand")
    {
        this.Caption = "我的命令";
        this.Description = "执行操作";
    }

    public override bool CanExecute(object parameter) => true;
}

public class MyExecutor : CommandExecutor
{
    public MyExecutor(Command cmd) : base(cmd) { }

    public override void Execute(object parameter)
    {
        // 业务逻辑
    }
}
```

## ButtonTool 类

⚠️ **`ButtonTool` 是抽象类，不能直接 new**。正确用法：

```csharp
var button = new ButtonTool("MyButton");
button.Caption = "按钮文字";
button.Tooltip = "提示文字";
button.Command = _commandManager.Commands["MyCommand"];

// 添加到工具栏
toolbar.Tools.Add(button);
```

## CommandBar 工具栏

```csharp
var cbManager = (CommandBarManager)ServiceManager.Instance
    .GetService(typeof(CommandBarManager));

var toolbar = new CommandBar("MyToolbar", "我的工具栏");
cbManager.CommandBars.Add(toolbar);
```
