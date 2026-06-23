# Addin 类

**命名空间**: `Aveva.ApplicationFramework`（✅ 已验证）
**程序集**: Aveva.ApplicationFramework.dll
**类型**: abstract class
**用途**: 插件基类，所有 E3D/PDMS 插件必须继承此类

## 必须实现的成员

| 成员 | 签名 | 说明 |
|------|------|------|
| Name | `string Name { get; }` | 插件名称 |
| Description | `string Description { get; }` | 插件描述 |
| Start | `void Start()` | 插件启动时调用，注册服务和 UI |
| Stop | `void Stop()` | 插件停止时调用，清理资源 |
| Assembly | `Assembly Assembly { get; }` | 插件程序集 |
| IsStarted | `bool IsStarted { get; }` | 是否已启动 |
| IAddinInterfaceObject | `object IAddinInterfaceObject { get; }` | 接口对象 |

## 典型用法

```csharp
using Aveva.ApplicationFramework;
using Aveva.ApplicationFramework.Presentation;

namespace MyE3DPlugin
{
    public class MyPlugin : Addin
    {
        private CommandManager _commandManager;

        public override string Name => "MyPlugin";
        public override string Description => "我的 E3D 插件";

        public override void Start()
        {
            _commandManager = (CommandManager)ServiceManager.Instance
                .GetService(typeof(CommandManager));
            RegisterCommands();
            CreateToolbar();
        }

        public override void Stop()
        {
            base.Stop();
        }

        private void RegisterCommands() { /* ... */ }
        private void CreateToolbar() { /* ... */ }
    }
}
```

## 生命周期

```
Load → Start → Running → Stop → Unload
```

## 注意事项

- `Addin` 是抽象类，必须继承，**不能直接实例化**
- 插件注册方式见 `开发引领手册.md`（E3D 2.1 用 `IAddin` 接口或配置文件）
