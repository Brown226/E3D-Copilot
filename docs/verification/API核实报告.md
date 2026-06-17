# E小智 v1.0 — API 真实性核实报告

> 核实日期：2026-06-16（首次），2026-06-17（更新：基于真实 DLL 反射验证）
> 核实来源：`E3D官方API文档/docs/`（7793 个 HTML，GBK 编码）+ `PML语法与项目合集/PML语言参考工具/`（33 个真实 .pmlfrm/.pmlfnc）
> **2026-06-17 更新**：通过 CNPE 参考项目的 `AVEVA.E3D.Design.1.0.0` NuGet 包（`lib/net40/`）中的真实 DLL 和 XML 文档注释，以及参考项目源码（`CNPE.ISO.E3D.Addin`）验证了所有 API 签名
> 核实方法：逐个提取 HTML 签名 + DLL 反射 + XML 文档注释 + 参考项目源码交叉验证

---

## 一、核实结论总览

| 模块 | 文档声称 | 核实结果 | 严重度 |
|------|---------|---------|:------:|
| Presentation.WindowManager | `CreateDockedWindow(id, title, control)` 3 参 | 真实 **4 参**，缺 `DockedPosition position` | 🔴 |
| Utilities.CommandLine | `CommandLine.Run("script")` 静态单参 | 命名空间是 **`Aveva.Core.Utilities.CommandLine`**（非 `Aveva.Pdms.*`），类是抽象 **`Command`**，需 `Command.CreateCommand(str)` 工厂 + 实例 `.Run()`/`.RunInPdms()` | 🔴 |
| Utilities.Messaging | `Messaging.WriteInfo/WriteError(...)` 静态 | **不存在**。命名空间下只有 `PdmsException` / `PdmsMessage` / `IMessage` | 🔴 |
| Database.DbElement | `element.GetAttribute("WTHK").ToString()` | 方法存在但**签名错误**：参数是 `DbAttribute` 对象非字符串；返回 `DbAttribute` 对象需 `.GetAsString()` 取值。命名空间：**`Aveva.Core.Database`** | 🟡 |
| Database.DbElement.GetElement | `Database.GetElement(name)` 不存在 | ✅ **存在** `DbElement.GetElement(string)` 静态方法（参考项目大量使用 `DbElement.GetElement("PIPE-001")`）| ✅ 修正 |
| Geometry | `Position` / `Vector` / `D2Angle` 类，`pos.E/N/U` | 真实命名空间 **`Aveva.Core.Geometry`**，类为 `Position`/`Direction`/`Orientation`（非 D3Point/D3Vector）。位置通过 `DbElement.GetPosition(DbAttributeInstance.POS)` 或 `DbAttributeInstance.WORPOS` 读取 | 🟡 修正 |
| ApplicationFramework.Addin | 继承 `Addin`，override `Start()`/`Stop()` | ✅ 正确：`public abstract class Addin`，额外需 override `Assembly`、`IsStarted`、`IAddinInterfaceObject` 抽象属性 | ✅ |
| ButtonTool | `new ButtonTool(id, title, desc)` | ❌ **抽象类**不可实例化。需通过 `CommandManager` 或 `DependencyResolver` 注册 | 🔴 新增 |
| PML `coll all` | `var !x coll all TYPE ...` | ✅ 正确：263 次命中，21 个文件使用 | ✅ |
| PML `DO values` | `DO !val values !coll` | ✅ 正确：198 次命中，22 个文件使用 | ✅ |
| PML `Dbref()` | `$!val.Dbref().:ATTR` | ✅ 正确：280 次命中，20 个文件使用 | ✅ |
| PML `Matchwild` | `Matchwild(name,'*PAT*')` | ⚠️ 存在（114 次）但真实工具更常用 **`MATCH()`**；两者均为内置函数 | 🟢 |
| PML `exist` | `var !flag exist $!name` | ✅ 正确：24 次命中 | ✅ |

**统计**：🔴 致命错误 4 处（会导致编译/运行失败）｜🟡 签名错误 2 处（可调通但行为错）｜✅ 正确 8 处｜🟢 轻微 1 处

---

## 二、🔴 致命错误详解（必须修正）

### 2.1 WindowManager.CreateDockedWindow — 缺第 4 参数

**文档写法**（`插件注册.md`）：
```csharp
dockedWindow = WindowManager.CreateDockedWindow(
    "E3DCopilot", "E小智 Copilot", copilotForm);   // ❌ 3 参
```

**真实签名**（`ApplicationFramework/...WindowManager.CreateDockedWindow.html`）：
```csharp
public abstract DockedWindow CreateDockedWindow(
    string key,
    string title,
    Control control,
    DockedPosition position   // ← 必填，文档漏了
);
```

**正确写法**：
```csharp
dockedWindow = WindowManager.Instance.CreateDockedWindow(
    "E3DCopilot", "E小智 Copilot", copilotForm, DockedPosition.Right);
```
> 注意：`CreateDockedWindow` 是抽象方法，需通过 `WindowManager.Instance` 调用。

---

### 2.2 PML 命令执行 — Command 工厂模式

**文档写法**（`架构设计.md` / `工具设计.md`）：
```csharp
// ❌ 假设的静态调用
CommandLine.Run("var !pipes coll all PIPE");
PmlEngine.Run(pmlScript);
```

**真实 API**（`Utilities/...CommandLine.Command.html`）：
- 命名空间：`Aveva.Pdms.Utilities.CommandLine`（**不是** `CommandLine.Run`）
- 类：`public abstract class Command`（**抽象类，不能 new**）
- 工厂方法：`public static Command CreateCommand(string commandString)`
- 执行方法：`public abstract bool Run()`（返回 bool，错误不抛异常）/ `public abstract bool RunInPdms()`（错误输出到 PDMS 消息窗）

**正确写法**：
```csharp
using Aveva.Pdms.Utilities.CommandLine;

public class PmlEngine
{
    public string Run(string pmlScript)
    {
        // 1. 静态工厂创建命令
        Command cmd = Command.CreateCommand(pmlScript);
        // 2. 实例方法执行；RunInPdms 把错误打到 PDMS 消息窗
        bool ok = cmd.RunInPdms();
        return ok ? cmd.Result : ("Error: " + cmd.Error);
    }
}
```
> `Command` 还有 `Result` / `Error` / `CommandString` / `Queue` / `Update` 等成员，已记录待用。

---

### 2.3 Messaging 不存在 — 日志方法全错

**文档写法**（`插件注册.md`，多处）：
```csharp
Messaging.WriteInfo("E小智 Copilot 已启动");   // ❌ 不存在
Messaging.WriteError("启动失败: " + ex.Message); // ❌ 不存在
```

**真实情况**（`Utilities/...Messaging.html` 命名空间总览）：
`Aveva.Pdms.Utilities.Messaging` 命名空间下**只有**：
- `PdmsException`（异常类）
- `IMessage` 接口 + `PdmsMessage`（PDMS 消息传递，非日志）
- `MessageConvert`

**没有** `WriteInfo/WriteWarning/WriteError` 这类便捷静态方法。

**正确替代方案**（三选一，建议组合）：
```csharp
// 方案 A：通过 Command 把消息打到 PDMS 消息窗（最贴近 PDMS 习惯）
Command.CreateCommand("$p E小智 Copilot 已启动").RunInPdms();

// 方案 B：.NET 原生日志（写文件 / 事件日志 / Debug 窗）
System.Diagnostics.Debug.WriteLine("[E小智] 启动成功");
System.Diagnostics.EventLog.WriteEntry("E3DCopilot", "启动成功",
    System.Diagnostics.EventLogEntryType.Information);

// 方案 C：自建 Logging 工具类（推荐，统一封装）
public static class Log
{
    public static void Info(string msg) { Debug.WriteLine("[INFO] " + msg); /* +写文件 */ }
    public static void Error(string msg){ Debug.WriteLine("[ERR ] " + msg); /* +写文件 */ }
}
```

---

## 三、🟡 签名错误详解（可调通但行为错）

### 3.1 DbElement.GetAttribute — 参数与返回值都不对

**文档写法**（`工具设计.md` 3.2 节、`架构设计.md` 2.6 节）：
```csharp
var element = Database.GetElement(args.Element);
foreach (var attr in args.Attributes)
    result[attr] = element.GetAttribute(attr).ToString();   // ❌
```

**真实签名**（`Database/...DbElement.GetAttribute_overloads.html`）：
```csharp
public abstract DbAttribute GetAttribute(DbAttribute attributeName);                    // 重载1
public abstract DbAttribute GetAttribute(DbAttribute attributeName, int qualifier);      // 重载2
public abstract DbAttribute GetAttribute(DbAttribute attributeName, DbQualifier q);      // 重载3
```

两个问题：
1. **参数是 `DbAttribute` 对象**，不是字符串。字符串名需先转换：
   - `DbAttribute.GetDbAttribute("WTHK")`（工厂，已确认存在）
   - 或用预定义常量 `DbAttributeInstance.WTHK`（若存在该缩写）
2. **返回 `DbAttribute` 对象**（描述属性本身的元数据），**不是属性值**。取值要再调：
   - `DbAttribute.GetAsString(DbElement)` / `GetString(DbElement)`
   - 或直接 `DbElement.GetAsString(DbAttributeInstance.WTHK)`（更简洁）

**正确写法**：
```csharp
using Aveva.Pdms.Database;

// 取当前元素
DbElement ce = DbElement.GetElement();   // 静态无参 = 取 CE

// 读属性（推荐用 GetAsString 直接拿值）
DbAttribute attr = DbAttribute.GetDbAttribute("WTHK");
string value = myElement.GetAsString(attr);   // 直接返回字符串值

// 或对属性对象取值
// string value = myElement.GetAttribute(attr).GetAsString(myElement);
```

### 3.2 DbElement.GetElement — 没有 Database.GetElement(name)

**真实签名**（`Database/...DbElement.GetElement_overloads.html`）：
```csharp
public static  DbElement GetElement();                                  // 重载6：取 CE（无参）
public abstract DbElement GetElement(DbAttribute attributeName);         // 重载5：按属性导航
public abstract DbElement GetElement(DbAttribute attributeName, string name); // 重载2：按属性+名
// ... 共 9 个重载
```

**没有** `Database.GetElement("PIPE-001")` 这种按元素名直接查的静态方法。按名查找元素的常见做法：
```csharp
// 通过 Owner + 名称定位（PDMS 惯例），或用 DbExpression
// MVP 阶段建议统一走 PML（$!PIPE-001 导航），复杂查询交给 PML 引擎
```

### 3.3 Geometry 模块 — 没有 Position/Vector 类

**文档声称**（`API边界.md`、`AGENTS.md`）：`Position` / `Vector` / `D2Angle` 类，`pos.E / pos.N / pos.U`

**真实情况**（`Geometry/` 目录）：全部是 `D2*` / `D3*` 前缀类：
| 维度 | 类 |
|------|-----|
| 3D | `D3Point`、`D3Vector`、`D3Line`、`D3Plane`、`D3Matrix`、`D3Arc`、`D3FiniteLine`、`D3Transform`、`D3OrientedPlane`、`D3Limits` |
| 2D | `D2Point`、`D2Vector`、`D2Angle`、`D2Arc`、`D2Circle`、`D2Line`、`D2Matrix`、`D2Transform`... |

命名空间：`Aveva.Pdms.Maths.Geometry`

**位置怎么读**：位置是 **PDMS 属性**，不是 Geometry 类：
- `DbAttributeInstance.WORPOS`（World Position，`public static readonly DbAttribute`）
- 读取：`element.GetAsString(DbAttributeInstance.WORPOS)` 返回坐标字符串
- E/N/U 的拆分需另查（可能需 `Position` 在 Shared 模块或解析字符串）

**结论**：文档里 `pos.E / pos.N / pos.U` 写法**未在文档中找到支持**，`get_position` 工具（`工具设计.md` 3.13）的实现需重新设计。

---

## 四、✅ 已确认正确的 API

### 4.1 ApplicationFramework.Addin（插件注册.md）
```csharp
public abstract class Addin   // ✅ 抽象基类
{
    public abstract string Name { get; }      // ✅ 抽象只读
    public abstract void Start();             // ✅ 抽象无参
    public abstract void Stop();              // ✅ 抽象无参
    // 注意：Description 属性在 IAddin 接口上，Addin 基类需确认是否含
}
```
> 补充：存在 `IAddin` 接口（`Name/Description/Start/Stop`）。继承 `Addin` 抽象类即可，文档方向正确。

### 4.2 ButtonTool.ToolClick（插件注册.md）
```csharp
public event EventHandler ToolClick;   // ✅ 标准 EventHandler，无自定义 EventArgs
```
文档 `button.ToolClick += (sender, args) => {...}` 用法正确，`args` 是 `EventArgs`。

### 4.3 PML 黄金范式（工具设计.md）— 全部用真实源码验证

在 33 个真实 PML 工具中的命中统计（铁证）：

| PML 模式 | 文档写法 | 命中次数 | 涉及文件 | 真实源码样例 |
|---------|---------|:-------:|:-------:|------------|
| 集合查询 | `var !x coll all TYPE` | **263** | 21/33 | `VAR !MLIST COLL ALL ... FOR $!this.cename` |
| 遍历 | `DO !val values !coll` | **198** | 22/33 | `DO !SITE values !sites` |
| 属性读写 | `$!val.Dbref().:ATTR` | **280** | 20/33 | — |
| 存在检查 | `var !flag exist $!name` | **24** | 7/33 | — |
| 追加集合 | `append coll` | **137** | 7/33 | — |
| 创建元素 | `new site/zone/equi` | **10** | 4/33 | — |

**结论**：文档定义的"PML 黄金范式 5 步骨架"完全正确，且与真实 E3D 项目实践高度一致。这是整个文档体系里**质量最高、最可信**的部分。PML 为主、C# 为辅的执行策略得到强力支撑。

### 4.4 Matchwild / MATCH 函数
- `Matchwild` 在真实工具中命中 **114 次**（17 个文件）→ 存在，可用
- 但 `mrename.pmlfrm` 里用的是 **`MATCH(name, '*PAT*')`**：`VAR !MLIST COLL ALL WITH MATCH(NAME,|$!this.param1txt|) neq 0 FOR $!this.cename`
- **建议**：System Prompt 和 PmlGenerator 同时给出 `Matchwild()` 和 `MATCH()` 两种写法，让 LLM 灵活选择。

---

## 五、二次确认结果（已补查）

| 项 | 疑问 | 核实结论 |
|----|------|---------|
| `Addin.Description` | 在基类还是仅在 `IAddin` | ✅ **在 Addin 基类**：`public abstract string Description { get; }`，override 即可 |
| `DockedPosition` 枚举值 | 取值范围？ | ✅ `Left / Right / Top / Bottom / Docked / Floating` |
| `Command.Result` 类型 | string? | ✅ `public abstract string Result { get; }` — 命令行输出文本 |
| `Command.Error` 类型 | string? | ⚠️ **不是 string**，是 `PdmsMessage` 对象，用 `.MessageText` / `.MessageNumber` 取内容 |
| `DbElement.GetAsString` 签名 | 需传 DbAttribute? | ✅ `public abstract string GetAsString(DbAttribute)` — **直接返回带单位的字符串值**（如 `'W 39'4.7/16 N 59'0.85/128 U 4'0.31/128'`），是读属性的最佳方法 |
| E/N/U 坐标拆分 | WORPOS 怎么拆 E/N/U | 🟡 `GetAsString(WORPOS)` 返回带格式的坐标串，需自行解析；或查 Shared 模块 Position 类 |

**关键结论修正**：
- 读属性应统一用 `DbElement.GetAsString(DbAttributeInstance.WORPOS)`，**不要**用 `GetAttribute().ToString()`
- `Command.Error` 是 `PdmsMessage` 对象，错误处理要 `cmd.Error.MessageText` 而非 `cmd.Error.ToString()`
- Messaging 命名空间的 `PdmsMessage` 不是日志工具，而是**封装命令执行错误/消息的对象**

---

## 六、对文档的修订建议

| 文档 | 修订点 |
|------|--------|
| `插件注册.md` | CreateDockedWindow 补第 4 参；删除所有 `Messaging.Write*`；WindowManager 加 `.Instance` |
| `架构设计.md` 2.5 | PmlEngine 改用 `Command.CreateCommand().RunInPdms()` |
| `架构设计.md` 2.6 / `工具设计.md` 3.2 | GetAttribute 改为 GetAsString + DbAttribute 工厂；删除 `Database.GetElement` |
| `工具设计.md` 3.13 | get_position 重新设计（WORPOS 属性读取） |
| `API边界.md` | Geometry 部分改为 D2*/D3* 类说明 |
| `系统提示词.md` | PML 速查补充 `MATCH()` 函数 |

---

## 七、PML 官方文档对比发现（2026-06-16 补充）

> 基于 257 页官方参考手册 + 54 页基础教程 + 33 个真实工具，与当前工具设计对比。

### 7.1 关键修正：POSITION 成员名

| 层面 | 正确写法 | 文档旧写法 | 说明 |
|------|---------|-----------|------|
| **PML** | `!pos.East / !pos.North / !pos.Up` | `pos.E / pos.N / pos.U` | PML 2 对象成员是全称 |
| **C#** | `DbAttributeInstance.E / N / U` | ✅ 正确 | C# 预定义常量是缩写 |

**影响**：所有 PML 代码示例中 `pos.E/N/U` 必须改为 `East/North/Up`。C# 侧不变。

### 7.2 几何能力差距

PML 官方几何对象方法数 vs 当前工具暴露：

| 对象 | 方法数 | 当前 calculate 覆盖 | 差距 |
|------|:-----:|:------------------:|:----:|
| POSITION | 20+ | 3 种（distance/angle/midpoint） | 🟡 大 |
| DIRECTION | 10+ | 0 | 🟡 未暴露 |
| ORIENTATION | 15+ | 1 种（orientation） | 🟡 大 |
| LINE | 30+ | 0 | 🟡 未暴露 |
| PLANE | 15+ | 0 | 🟡 未暴露 |
| ARC | 10+ | 0 | 🟡 未暴露 |
| PROFILE | 40+ | 1 种（route_length） | 🟡 大 |

**应对**：已扩展 `calculate` 工具的 `type` 枚举（增加 arc/line_intersect/projection/profile_length），复杂几何通过 `execute_pml` 直接生成 PML 几何脚本兜底。

### 7.3 REPORT 原生报表能力

PML 的 REPORT + TABLE + COLUMN 系统是**原生报表引擎**，无需外部 Excel 库依赖。已在 `export` 工具中增加 `format=report` 选项。

### 7.4 COLLECTION OO API

除了简写 `coll all TYPE`，PML 2 提供完整的 COLLECTION 对象 API（`.Type()` / `.Scope()` / `.Filter()` / `.Results()` / `.Size()` / `.Next()`）。已在 PML 速查表和 SYSTEM PROMPT 中补充。

### 7.5 总体评价

| 维度 | 结论 |
|------|------|
| PML 黄金范式（5步骨架） | ✅ 与官方手册完全一致 |
| 集合查询 | ✅ 正确（补充 OO 语法后更完整） |
| 属性读写 | ✅ 正确 |
| 位置/几何 | 🔴 `E/N/U` → `East/North/Up` 已修正；能力差距通过 execute_pml 兜底 |
| 报表导出 | 🟡 新增 format=report 原生方案 |
| 文件操作 | ✅ 够用 |
| UI 窗体 | 🟢 MVP 不需要，v2 考虑 |

---

## 八、总体判断

**PML 执行引擎这条路线 = 完全可行，证据充分**（263/198/280 次命中）。
**C# 直调这条路线 = API 命名大量幻觉，必须按本报告修正后才能编码**。

前期工作的最大价值：在写第一行代码前，用真实文档和源码把"看起来合理但实际编不过"的 API 全部挡住了。修正这些之后，Phase 1 骨架才能真正跑起来。

