using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Events;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// Ask — 向用户提出结构化多选题（对齐 Reasonix agent/ask.go AskTool）
    ///
    /// 元能力工具：
    /// 当 AI 遇到真正的决策分叉——无法从请求、代码或合理默认值中解决的问题时，
    /// 调用此工具向用户提问。前端渲染可选选项，用户选择后返回。
    /// 不应用于有明确默认值的决策——选择合理选项并继续。
    /// YOLO 等工具审批模式不会代替用户回答此问题。
    ///
    /// 支持 1-4 个问题批量提问，每个问题有 header（Tab 标签）、question、2-4 个 options（label+description）、multiSelect。
    /// </summary>
    public class AskUserHandler : IToolHandler
    {
        /// <summary>
        /// IAsker 接口 — 由 Controller 实现，Handler 通过此接口触达用户
        /// 对齐 Reasonix agent.Asker interface
        /// </summary>
        public interface IAsker
        {
            /// <summary>
            /// 向用户提问并阻塞等待回答。返回 answers 或超时抛异常。
            /// </summary>
            Task<List<AskAnswer>> AskAsync(List<AskQuestion> questions, CancellationToken ct);
        }

        private readonly IAsker _asker;

        /// <summary>
        /// 创建 AskUserHandler。asker 由 Controller 注入。
        /// headless（_asker == null）时返回 model-assumption fallback。
        /// </summary>
        public AskUserHandler(IAsker asker = null)
        {
            _asker = asker;
        }

        /// <summary>
        /// 延迟注入 Asker（Controller 创建晚于 ToolExecutor 时的回填）
        /// 对齐 Reasonix agent.SetAsker(as Asker)
        /// </summary>
        public void SetAsker(IAsker asker)
        {
            // C# doesn't allow reassigning readonly fields, but we can use a backing field trick.
            // Instead, store in a mutable field:
            _askerField = asker;
        }

        private IAsker _askerField;
        private IAsker EffectiveAsker => _askerField ?? _asker;

        public string Name => "ask";
        public string Description =>
            "Ask the user one or more multiple-choice questions when you hit a decision that is genuinely theirs to make — one you can't resolve from the request, the code, or sensible defaults. The frontend shows the options for the user to pick; their choices are returned to you. Prefer this over asking in prose for any real fork (which approach, which library, scope). Don't use it for decisions with an obvious default — pick the sensible option and proceed. Tool-approval modes such as YOLO do not answer these questions for the user. Each question has a short `header` (a tab label), the `question` text, 2-4 `options` (each a `label` and optional `description`; put any recommended option first), and `multiSelect` when more than one may apply.";

        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""questions"": {
      ""type"": ""array"",
      ""minItems"": 1,
      ""maxItems"": 4,
      ""description"": ""1-4 questions to ask together."",
      ""items"": {
        ""type"": ""object"",
        ""properties"": {
          ""header"": {
            ""type"": ""string"",
            ""description"": ""Very short label for the question (a tab title), e.g. 'Library'.""
          },
          ""question"": {
            ""type"": ""string"",
            ""description"": ""The full question to ask.""
          },
          ""options"": {
            ""type"": ""array"",
            ""minItems"": 2,
            ""maxItems"": 4,
            ""description"": ""The choices. Put any recommended option first."",
            ""items"": {
              ""type"": ""object"",
              ""properties"": {
                ""label"": {
                  ""type"": ""string"",
                  ""description"": ""The choice text (concise).""
                },
                ""description"": {
                  ""type"": ""string"",
                  ""description"": ""Optional one-line explanation of the choice.""
                }
              },
              ""required"": [""label""]
            }
          },
          ""multiSelect"": {
            ""type"": ""boolean"",
            ""description"": ""Allow selecting more than one option.""
          }
        },
        ""required"": [""header"", ""question"", ""options""]
      }
    }
  },
  ""required"": [""questions""]
}";

        // ReadOnly=true：Ask 无宿主副作用，永远不需要审批，Plan Mode 下也可用
        // 对齐 Reasonix AskTool.ReadOnly() → true
        public bool IsReadOnly => true;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                var json = JObject.Parse(args);
                var questionsArray = json["questions"] as JArray;

                if (questionsArray == null || questionsArray.Count == 0)
                    return ToolResult.Fail("at least one question is required");

                // 解析 — 兼容新旧两种格式
                var qs = new List<AskQuestion>();

                // ── 新格式：questions 数组 ──
                if (questionsArray != null && questionsArray.Count > 0)
                {
                    for (int i = 0; i < questionsArray.Count; i++)
                    {
                        var q = questionsArray[i];
                        string header = q.Value<string>("header")?.Trim();
                        string question = q.Value<string>("question")?.Trim();
                        bool multiSelect = q.Value<bool?>("multiSelect") ?? false;
                        var optionsArray = q["options"] as JArray;

                        if (string.IsNullOrEmpty(header))
                            return ToolResult.Fail($"question {i + 1}: header is required");
                        if (string.IsNullOrEmpty(question))
                            return ToolResult.Fail($"question {i + 1}: question text is required");
                        if (optionsArray == null || optionsArray.Count < 2)
                            return ToolResult.Fail($"question {i + 1}: at least 2 options are required");

                        qs.Add(new AskQuestion
                        {
                            Id = $"q{i + 1}",
                            Header = header,
                            Prompt = question,
                            Options = ParseOptions(optionsArray, i),
                            Multi = multiSelect
                        });
                    }
                }
                // ── 旧格式兼容：单个 question + options ──
                else
                {
                    string question = json.Value<string>("question")?.Trim();
                    var optionsArray = json["options"] as JArray;
                    bool multiSelect = json.Value<bool?>("multiSelect") ?? false;
                    string header = json.Value<string>("header")?.Trim();

                    if (string.IsNullOrEmpty(question))
                        return ToolResult.Fail("at least one question is required (use 'question' for single or 'questions[]' for multiple)");
                    if (optionsArray == null || optionsArray.Count < 2)
                        return ToolResult.Fail("at least 2 options are required");

                    qs.Add(new AskQuestion
                    {
                        Id = "q1",
                        Header = header ?? "选择",
                        Prompt = question,
                        Options = ParseOptions(optionsArray, 0),
                        Multi = multiSelect
                    });
                }

                // Headless / no interactive user
                if (EffectiveAsker == null)
                {
                    return ToolResult.Ok(
                        "No interactive user answered. This is a model-assumption fallback, not a user answer. " +
                        "Proceed with your best judgment, state the assumption you made, " +
                        "and prefer the safest reversible option when choices differ in risk.",
                        new { fallback = true });
                }

                // 阻塞等待用户回答
                List<AskAnswer> answers;
                try
                {
                    answers = await EffectiveAsker.AskAsync(qs, ct);
                }
                catch (OperationCanceledException)
                {
                    return ToolResult.Fail("User did not respond within the time limit. 用户未在时限内回答。请尝试重新提问或换一种方式表达。");
                }

                // 格式化回答给 LLM
                string output = FormatAnswers(qs, answers);
                return ToolResult.Ok(output, new
                {
                    questionCount = qs.Count,
                    answers = answers.Select(a => new { a.QuestionId, a.Selected }).ToList()
                });
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                return ToolResult.Fail($"Invalid JSON arguments: {ex.Message}");
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"Ask failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 解析选项数组（兼容两种格式：字符串数组 / 对象数组 {label, description}）
        /// </summary>
        private static List<AskOption> ParseOptions(JArray optionsArray, int questionIndex)
        {
            var opts = new List<AskOption>();
            var seenLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int j = 0; j < optionsArray.Count; j++)
            {
                var o = optionsArray[j];
                string label;
                string desc;

                if (o.Type == JTokenType.String)
                {
                    // 旧格式：纯字符串数组 ["选项A", "选项B"]
                    label = o.Value<string>()?.Trim();
                    desc = null;
                }
                else
                {
                    // 新格式：对象数组 [{"label": "...", "description": "..."}]
                    label = o.Value<string>("label")?.Trim();
                    desc = o.Value<string>("description")?.Trim();
                }

                if (string.IsNullOrEmpty(label))
                    return new List<AskOption> { new AskOption { Label = $"(option {j + 1}: empty label)" } };
                if (!seenLabels.Add(label))
                    continue; // 跳过重复标签

                opts.Add(new AskOption { Label = label, Description = desc });
            }

            return opts;
        }

        /// <summary>
        /// 格式化用户回答（对齐 Reasonix formatAnswers）
        /// </summary>
        private static string FormatAnswers(List<AskQuestion> qs, List<AskAnswer> answers)
        {
            // 构建 questionId → selected[] 映射
            var pick = new Dictionary<string, List<string>>();
            foreach (var a in answers)
            {
                pick[a.QuestionId] = a.Selected ?? new List<string>();
            }

            int answered = 0;
            foreach (var q in qs)
            {
                if (pick.TryGetValue(q.Id, out var sel) && sel.Count > 0)
                    answered++;
            }

            // 用户 dismiss 了所有问题（没选任何选项）
            if (answered == 0)
            {
                return "The user dismissed the question without choosing — read this as \"don't decide for me, let's just talk.\" " +
                       "Do not pick an option, run a tool, or take any further action toward this; stop and wait for the user's next message.";
            }

            var sb = new StringBuilder();
            sb.AppendLine("The user answered:");
            foreach (var q in qs)
            {
                string label = !string.IsNullOrEmpty(q.Header) ? q.Header : q.Prompt;
                if (!pick.TryGetValue(q.Id, out var sel) || sel.Count == 0)
                {
                    sb.AppendLine($"- {label}: (left unanswered — don't assume a choice)");
                }
                else
                {
                    sb.AppendLine($"- {label}: {string.Join(", ", sel)}");
                }
            }
            return sb.ToString().TrimEnd();
        }
    }
}
