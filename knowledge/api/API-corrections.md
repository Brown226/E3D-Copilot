# ⚠️ API 修正清单（防幻觉速查）

> 从 `API核实报告.md` 提取的关键修正，**生成 C# 代码前必须查阅**。

## 🔴 致命错误（会导致编译/运行失败）

| 文档声称 | 真实签名 | 影响 |
|---------|---------|:----:|
| `WindowManager.CreateDockedWindow(id, title, control)` 3参 | `WindowManager.CreateDockedWindow(key, title, control, DockedPosition)` **4参**，需要传入停靠位置枚举 | DockedWindow 创建失败 |
| `CommandLine.Run("script")` 静态单参 | 命名空间 `Aveva.Core.Utilities.CommandLine`，类为抽象 `Command`，需 `Command.CreateCommand(str)` 工厂 + 实例 `.Run()`/`.RunInPdms()` | PML 无法执行 |
| `Messaging.WriteInfo/WriteError(...)` 静态 | **不存在**。用 `PdmsMessage.Show(...)` 或 `PdmsMessage.ShowAsync(...)` 替代 | 编译错误 |
| `ButtonTool` 直接 new | `ButtonTool` 是抽象类，不可实例化。需通过 `CommandManager` 注册或使用 `DependencyResolver` | 编译错误 |

## 🟡 签名错误（可调通但行为错）

| 文档声称 | 真实签名 | 影响 |
|---------|---------|:----:|
| `element.GetAttribute("WTHK").ToString()` | 参数是 `DbAttribute` 对象非字符串；取值用 `element.GetAsString(DbAttributeInstance.WTHK)` | 属性值读取错误 |
| `pos.E/N/U` 或 `pos.East/North/Up` 混用 | PML 中坐标属性是 `pos.E`/`pos.N`/`pos.U`，C# 中是 `Position.East`/`North`/`Up` | 代码逻辑错 |
| `D3Point`/`D3Vector` 类名 | 真实类是 `Position`/`Direction`/`Orientation`，在 `Aveva.Core.Geometry` | 编译错误 |
| 命名空间 `Aveva.Pdms.*` | 实际是 `Aveva.Core.Database`/`Aveva.Core.Geometry` | 命名空间引用错误 |

## ✅ 已验证正确的 API

| API | 签名 | 证据 |
|-----|------|:----:|
| `DbElement.GetElement(name)` | `static DbElement GetElement(string)` | 参考项目大量使用 |
| `DbElement.GetAsString(attr)` | `string GetAsString(DbAttribute)` | DLL 反射确认 |
| `Command.CreateCommand(str).RunInPdms()` | 工厂模式 | 真实项目代码确认 |
| `CurrentElement.Element` | 属性非方法，监听 `CurrentElementChanged` 事件 | 真实代码确认 |
| `DbElement.AddNew(type)` | 在父元素下创建子元素 | SKILL.md 确认 |
| `DbElement.FirstMember/NextMember` | 遍历子元素 | 多来源确认 |

## C# 正确写法速查

```csharp
// ✅ 命名空间
using Aveva.Core.Database;       // 非 Aveva.Pdms.Database
using Aveva.Core.Geometry;        // 非 Aveva.Pdms.Maths.Geometry
using Aveva.Core.Utilities;       // 用于 Command

// ✅ 获取当前元素
DbElement ce = CurrentElement.Element;  // 非 Db.CurrentSession.CurrentElement

// ✅ 按名称查找元素
DbElement el = DbElement.GetElement("PIPE-001");

// ✅ 读取属性
string wthk = el.GetAsString(DbAttributeInstance.WTHK);
double bore = el.GetAsDouble(DbAttributeInstance.Bore);

// ✅ 设置属性
el.SetAttribute(DbAttributeInstance.WTHK, "SCH40");

// ✅ PML 执行
var cmd = Aveva.Core.Utilities.Command.Command.CreateCommand("show all");
cmd.RunInPdms();
```
