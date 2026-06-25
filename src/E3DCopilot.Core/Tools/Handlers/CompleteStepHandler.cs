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
    /// CompleteStep — 证据签收步骤完成
    ///
    /// 元能力工具：
    /// 记录一个经证据支持的步骤完成声明。与 todo_write 配合使用：
    /// - todo_write 管理任务列表（一个 item 翻转为 in_progress）
    /// - complete_step 为完成的步骤签收（必须提供证据）
    ///
    /// 没有证据的完成声明会被拒绝，因此 AI 不能随意标记任务完成。
    /// 签收后自动推进 todo 状态（当前 completed → 下一个 pending 变为 in_progress）。
    ///
    /// 对齐 Reasonix builtin/completestep.go 的设计。
    /// </summary>
    public class CompleteStepHandler : IToolHandler
    {
        private readonly IEventSink _sink;

        private static readonly HashSet<string> ValidEvidenceKinds = new HashSet<string>
        {
            "verification", // 运行了命令/测试，引用命令和结果
            "diff",         // 具体代码变更，引用变更的文件
            "files",        // 创建/编辑/检查的文件，引用路径
            "manual"        // 手动检查，引用确认了什么
        };

        public CompleteStepHandler(IEventSink sink = null)
        {
            _sink = sink;
        }

        public string Name => "complete_step";

        public string Description =>
            "Record the evidence-backed completion of ONE step of an approved plan. " +
            "Call it as you finish each step instead of silently moving on: it signs the step off with PROOF it is done — " +
            "the verification you ran (command + result), the diff/files you changed, or a manual check. " +
            "A completion with no evidence is REJECTED, so don't claim a step is done until you can show why. " +
            "The host advances the task list for you when you sign off — it marks this step completed and moves the next to in_progress, " +
            "so you don't need a separate todo_write to mark completions. " +
            "Fields: `step` (which step — its title or number, matching the task list), " +
            "`result` (what is now true/changed), " +
            "`evidence` (≥1 item, each with `kind` = verification|diff|files|manual and a `summary`, plus optional `command`/`paths`), " +
            "and optional `notes`.";

        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""step"": {
      ""type"": ""string"",
      ""description"": ""Which plan step this completes — its title or number, matching the task list.""
    },
    ""result"": {
      ""type"": ""string"",
      ""description"": ""What is now true or changed as a result of finishing this step.""
    },
    ""evidence"": {
      ""type"": ""array"",
      ""minItems"": 1,
      ""description"": ""Proof the step is done. At least one item is required."",
      ""items"": {
        ""type"": ""object"",
        ""properties"": {
          ""kind"": {
            ""type"": ""string"",
            ""enum"": [""verification"", ""diff"", ""files"", ""manual""],
            ""description"": ""verification = a command/test was run (command REQUIRED); diff = a concrete code change (paths REQUIRED); files = files created/edited/inspected (paths REQUIRED); manual = a manual check.""
          },
          ""summary"": {
            ""type"": ""string"",
            ""description"": ""The evidence itself: the test result, what the diff does, or what was confirmed.""
          },
          ""command"": {
            ""type"": ""string"",
            ""description"": ""REQUIRED for verification evidence: the command as it actually ran (e.g. go test ./...).""
          },
          ""paths"": {
            ""type"": ""array"",
            ""items"": { ""type"": ""string"" },
            ""description"": ""REQUIRED for diff/files evidence: the files this evidence refers to.""
          }
        },
        ""required"": [""kind"", ""summary""]
      }
    },
    ""notes"": {
      ""type"": ""string"",
      ""description"": ""Optional caveats, follow-ups, or anything deferred.""
    }
  },
  ""required"": [""step"", ""result"", ""evidence""]
}";

        // ReadOnly=true：complete_step 只是记录声明（无文件系统或进程副作用），
        // 不需要审批，在 Plan Mode 下也可用。
        public bool IsReadOnly => true;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            await Task.CompletedTask;

            try
            {
                var json = JObject.Parse(args);

                string step = json.Value<string>("step");
                string result = json.Value<string>("result");
                string notes = json.Value<string>("notes");
                var evidenceArray = json["evidence"] as JArray;

                // 参数校验
                if (string.IsNullOrWhiteSpace(step))
                    return ToolResult.Fail("step is required — name the plan step you are completing");
                if (string.IsNullOrWhiteSpace(result))
                    return ToolResult.Fail("result is required — state what is now true after finishing this step");
                if (evidenceArray == null || evidenceArray.Count == 0)
                    return ToolResult.Fail("at least one evidence item is required — don't mark a step complete without showing why it's done");

                // 校验证据
                var evidenceList = new List<StepEvidence>();
                for (int i = 0; i < evidenceArray.Count; i++)
                {
                    var ev = evidenceArray[i];
                    string kind = ev.Value<string>("kind");
                    string summary = ev.Value<string>("summary");
                    string command = ev.Value<string>("command");
                    var pathsToken = ev["paths"] as JArray;
                    string[] paths = pathsToken?.Select(p => p.ToString()).ToArray() ?? new string[0];

                    if (string.IsNullOrWhiteSpace(kind) || !ValidEvidenceKinds.Contains(kind))
                        return ToolResult.Fail($"evidence {i + 1}: invalid kind \"{kind}\" (want verification|diff|files|manual)");
                    if (string.IsNullOrWhiteSpace(summary))
                        return ToolResult.Fail($"evidence {i + 1}: summary is required — the evidence is the summary, not just its kind");

                    // 特定类型的额外校验
                    if (kind == "verification" && string.IsNullOrWhiteSpace(command))
                        return ToolResult.Fail($"evidence {i + 1}: verification command is required — cite the command you ran, or use kind \"manual\"");
                    if ((kind == "diff" || kind == "files") && paths.Length == 0)
                        return ToolResult.Fail($"evidence {i + 1}: {kind} evidence requires paths — cite the files you changed/touched");

                    evidenceList.Add(new StepEvidence
                    {
                        Kind = kind,
                        Summary = summary,
                        Command = command,
                        Paths = paths
                    });
                }

                // 尝试自动推进 todo 状态
                bool advanced = TodoWriteHandler.AdvanceTodo(step);

                // 格式化输出
                var kinds = string.Join(", ", evidenceList.Select(e => e.Kind));
                var sb = new StringBuilder();
                sb.Append($"Step \"{step}\" signed off with {evidenceList.Count} evidence item(s) [{kinds}].");

                // 统计证据类型
                int verified = evidenceList.Count(e => e.Kind == "verification" || e.Kind == "diff" || e.Kind == "files");
                int manual = evidenceList.Count(e => e.Kind == "manual");
                sb.Append($" Host evidence: host-verified {verified}, manual/unverified {manual}.");

                if (advanced)
                {
                    sb.Append(" The host advanced the task list; continue with the next step.");
                }
                else
                {
                    sb.Append(" (No matching todo found to advance.)");
                }

                if (!string.IsNullOrWhiteSpace(notes))
                {
                    sb.AppendLine();
                    sb.AppendLine($"Notes: {notes}");
                }

                string output = sb.ToString().TrimEnd();

                _sink?.Emit(CopilotEvent.Notice($"CompleteStep: \"{step}\" signed off"));

                return ToolResult.Ok(output, new
                {
                    step,
                    result,
                    evidenceCount = evidenceList.Count,
                    evidenceKinds = evidenceList.Select(e => e.Kind).ToArray(),
                    advanced,
                    notes
                });
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                return ToolResult.Fail($"Invalid JSON arguments: {ex.Message}");
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"CompleteStep failed: {ex.Message}");
            }
        }

        private class StepEvidence
        {
            public string Kind { get; set; }
            public string Summary { get; set; }
            public string Command { get; set; }
            public string[] Paths { get; set; }
        }
    }
}
