# PdmsStandalone — 独立运行环境

**命名空间**: `Aveva.Pdms.Standalone`（✅ 已核实）
**程序集**: Aveva.Pdms.Standalone.dll
**用途**: 在 E3D GUI 之外以独立模式访问数据库（如控制台工具、自动化脚本）

## 生命周期

```
PdmsStandalone.Start()                          // 1. 初始化
PdmsStandalone.Open(project, user, pass, mdb)   // 2. 打开项目
    ↓
  ... 使用 PdmsStandalone.Project / .MDB 操作 ...
    ↓
PdmsStandalone.Close()                          // 3. 关闭项目
PdmsStandalone.Finish()                         // 4. 结束会话
```

## 关键方法

| 方法 | 签名 | 说明 |
|------|------|------|
| Start | `static bool Start()` | 初始化独立应用 |
| Open | `static bool Open(string project, string user, string pass, string mdbName)` | 打开项目（传用户名密码） |
| Close | `static void Close()` | 关闭项目和 MDB |
| Finish | `static void Finish()` | 结束独立应用会话 |
| ExitError | `static void ExitError(string message)` | 显示错误信息并退出 |
| ExitError | `static void ExitError(Exception ex)` | 显示异常详情并退出 |

## 关键属性

| 属性 | 类型 | 说明 |
|------|------|------|
| MDB | `MDB` | 当前打开的 MDB 数据库 |
| Project | `Project` | 当前打开的项目 |

## 典型用法

```csharp
using Aveva.Pdms.Standalone;
using Aveva.Pdms.Database;

class Program
{
    static void Main(string[] args)
    {
        // 初始化
        if (!PdmsStandalone.Start())
        {
            Console.WriteLine("初始化失败");
            return;
        }

        // 打开项目
        if (!PdmsStandalone.Open("MyProject", "user", "pass", "/MyMDB"))
        {
            Console.WriteLine("打开项目失败");
            PdmsStandalone.Finish();
            return;
        }

        // 操作数据库
        DbElement site = PdmsStandalone.Project.RootElement;
        DbElement child = site.FirstMember();
        while (child.IsValid)
        {
            Console.WriteLine(child.Name);
            child = site.NextMember(child);
        }

        // 关闭
        PdmsStandalone.Close();
        PdmsStandalone.Finish();
    }
}
```

## ⚠️ 注意事项

- Standalone 模式不需要 E3D GUI，可以在控制台应用中使用
- 所有成员都是 `static`，不需要实例化
- `Open()` 需要完整凭据（用户/密码/MDB 名称）
- 必须按顺序调用：`Start()` → `Open()` → ... → `Close()` → `Finish()`
