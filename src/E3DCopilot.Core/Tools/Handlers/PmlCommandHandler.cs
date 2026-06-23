using System;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Tools;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// Execute PML script (universal fallback executor)
    /// Corresponds to cline-chinese-main's ExecuteCommand ToolHandler
    /// </summary>
    public class PmlCommandHandler : IToolHandler
    {
        private readonly IToolDispatcher _dispatcher;

        public PmlCommandHandler(IToolDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public string Name => "execute_pml";
        public string Description => "Execute PML script (universal fallback tool). Complex queries, batch operations, special business logic can all be executed via PML";
        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""script"": { ""type"": ""string"", ""description"": ""PML script content"" }
  },
  ""required"": [""script""]
}";
        public bool IsReadOnly => false;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                // 提取 script 参数
                string script = ExtractScript(args);
                if (!string.IsNullOrEmpty(script) && IsReadOnlyAttributeQuery(script))
                {
                    return ToolResult.Fail("检测到纯属性读取操作，请使用 get_attributes 工具读取属性，它更快且更稳定。");
                }

                var result = await _dispatcher.ExecuteAsync("execute_pml", args);
                // 最小安全方案：Text 不变，Data 放结构化 meta 供前端渲染
                var meta = new JObject
                {
                    ["tool"] = "execute_pml",
                    ["summary"] = "PML 脚本执行完成",
                    ["pmlScript"] = script ?? "",
                };
                return ToolResult.Ok(result, meta);
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"PML execution failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 从工具调用的 JSON 参数中提取 script 字段内容
        /// </summary>
        private static string ExtractScript(string args)
        {
            if (string.IsNullOrWhiteSpace(args)) return null;
            try
            {
                var json = JObject.Parse(args);
                return json["script"]?.ToString();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 判断 PML 脚本是否仅为属性读取操作
        /// 若命中则拦截并提示使用 get_attributes 工具
        /// </summary>
        private static bool IsReadOnlyAttributeQuery(string script)
        {
            if (string.IsNullOrWhiteSpace(script))
                return false;

            var s = script.Trim();
            var sUpper = s.ToUpperInvariant();

            // ── 规则 1：直接读当前/primary/current 元素的单个属性 ──
            //   !!CE.NAME   $!CE.TYPE   !!primary.WTHK   $!current.DESC
            if (Regex.IsMatch(s, @"^[!$]+(CE|PRIMARY|CURRENT)\.\w+\s*$", RegexOptions.IgnoreCase))
                return true;

            // ── 规则 2：属性比较（本质仍是读属性）──
            //   !!CE.NAME == 'PIPE'   !!primary.TYPE != 'ELBOW'
            if (Regex.IsMatch(s, @"^[!$]+(CE|PRIMARY|CURRENT)\.\w+\s*(==|!=|EQ|NE)\s*.+$", RegexOptions.IgnoreCase))
                return true;

            // ── 规则 3：ce.attributes() 或 output ce.attributes() ──
            if (Regex.IsMatch(s, @"^(OUTPUT\s+)?CE\.ATTRIBUTES\s*\(\s*\)\s*$", RegexOptions.IgnoreCase))
                return true;

            // ── 规则 4：ce.attribute('NAME') 或 output ce.attribute('NAME') ──
            if (Regex.IsMatch(s, @"^(OUTPUT\s+)?CE\.ATTRIBUTE\s*\(\s*['""][\w*]+['""]\s*\)\s*$", RegexOptions.IgnoreCase))
                return true;

            // ── 规则 5：object.eval 中包含 attributes（典型 AI 生成模式）──
            //   object.eval('collect !!primary.attributes()')
            if (sUpper.Contains("OBJECT.EVAL") && sUpper.Contains("ATTRIBUTES"))
                return true;

            // ── 规则 6：collect !!primary.attributes() 或 !!primary.attributes() 独立语句 ──
            if (Regex.IsMatch(s, @"^(COLLECT\s+)?[!$]+PRIMARY\.ATTRIBUTES\s*\(\s*\)\s*$", RegexOptions.IgnoreCase))
                return true;

            // ── 规则 7：简单 !!ce.xxx / $!ce.xxx 等大小写变体 ──
            //   已经由规则 1 覆盖，但显式列出以增强可读性
            return false;
        }
    }
}
