# Command — PML 命令执行器

**命名空间**: `Aveva.Core.Utilities.CommandLine`（⚠️ 注意：文档说是 `Aveva.Pdms.Utilities`，实际是 `Aveva.Core.Utilities`）
**用途**: 执行 PML 脚本

## ⚠️ 关键修正

| 文档声称 | 真实签名 |
|---------|---------|
| `CommandLine.Run("script")` 静态方法 | `Command` 是抽象类，用 `Command.CreateCommand(str)` 工厂 |
| `Messaging.WriteInfo()` | **不存在**，用 `PdmsMessage.Show()` 替代 |

## 正确用法

```csharp
using Aveva.Core.Utilities.CommandLine;

// ✅ 执行 PML 命令
var cmd = Command.CreateCommand("show all");
cmd.RunInPdms();

// ✅ 复杂 PML 脚本
string script = @"
    VAR !LIST COLL ALL PIPE FOR $!this.cename
    DO !ELE values !LIST
        $P !ELE.Name
    ENDDO
";
var cmd2 = Command.CreateCommand(script);
cmd2.RunInPdms();

// ✅ Run() 与 RunInPdms() 的区别
// - RunInPdms(): 在当前 E3D 会话中运行（推荐）
// - Run(): 在后台运行
```

## PdmsMessage 类

**命名空间**: `Aveva.Core.Utilities`
**用途**: 显示消息对话框（替代不存在的 Messaging 类）

```csharp
using Aveva.Core.Utilities;

// 显示信息
PdmsMessage.Show("操作完成", "提示", PdmsMessageButtons.OK);

// 显示警告
PdmsMessage.Show("确认执行此操作？", "警告",
    PdmsMessageButtons.YesNo);

// 异步显示
PdmsMessage.ShowAsync("正在处理...", "请稍候");
```

## PdmsException 类

**命名空间**: `Aveva.Core.Utilities`
**用途**: E3D 操作异常

```csharp
try
{
    element.SetAttribute(DbAttributeInstance.WTHK, "SCH40");
}
catch (PdmsException ex)
{
    PdmsMessage.Show($"E3D 错误: {ex.Message}", "错误",
        PdmsMessageButtons.OK);
}
```
