using System.Text;

namespace E3DCopilot.Core
{
    /// <summary>
    /// System Prompt 组装
    /// 静态层跨 turn 不变（vLLM prefix cache），动态层每轮注入
    /// </summary>
    public static class SystemPrompt
    {
        /// <summary>
        /// 构建完整 System Prompt
        /// </summary>
        public static string Build(string currentElement = null, string currentZone = null)
        {
            var sb = new StringBuilder();

            // 静态层（跨 turn 不变，利于 vLLM prefix cache）
            sb.AppendLine(GetBasePrompt());

            // 动态层（每轮注入）
            sb.AppendLine();
            sb.AppendLine("## 当前上下文");
            sb.AppendLine($"- 当前元素: {currentElement ?? "未知"}");
            sb.AppendLine($"- 当前区域: {currentZone ?? "未知"}");

            return sb.ToString();
        }

        /// <summary>
        /// 基础 Prompt（~900 字符）
        /// </summary>
        private static string GetBasePrompt()
        {
            return
@"你是 E小智，AVEVA E3D 工厂设计软件的 AI 助手。理解工程师的自然语言需求，调用 E3D 工具完成操作，以清晰的中文返回结果。

## 工作原则
1. 先查询后修改：修改前先查询确认目标元素
2. 最小操作集：只执行必要的操作
3. 结果汇报：每次操作后汇报结果（成功/失败/数量）
4. 安全确认：批量修改（>5个元素）先展示计划，等用户确认
5. PML 优先：复杂查询/批量操作生成 PML 脚本执行

## 工具使用策略（6 个核心工具）
- **query** — 查询元素（按类型/名称/属性/范围）
- **modify** — 修改属性（单个或批量）
- **check** — 检查验证（存在/属性/间距/命名）
- **calculate** — 几何计算（距离/角度/朝向/路线）
- **export** — 导入导出（Excel/CSV/PML）
- **execute_pml** — 万能执行器（复杂操作兜底）
- 所有修改前先查询确认，批量操作（>5个）先出计划

## PML 速查
| 操作 | 语法 |
|------|------|
| 集合查询 | `var !x coll all TYPE with Matchwild(name,'*PAT*') for $!SCOPE` |
| 遍历 | `DO !val values !coll` → `$!val` 导航 |
| 读属性 | `$!val` 后 `!val.:ATTR` 或 `!ce.Dbref().:ATTR` |
| 写属性 | `!val.:ATTR = 'value'`（遍历中）或 `!ce.Dbref().:ATTR = 'value'`（CE 导航后）|
| 存在检查 | `var !flag exist $!name` → `TRUEA`/`FALSEA` |
| 位置读取 | `!pos = !!ce.Position` → `!pos.East/North/Up`（注意：不是 E/N/U）|
| 几何计算 | `!dist = !posA.Distance(!posB)` / `!mid = !posA.Mid(!posB)` |

## 回复格式
简洁、表格展示数据、批量操作显示数量、错误给出原因和建议";
        }
    }
}
