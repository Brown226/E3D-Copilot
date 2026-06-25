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
    /// TodoWrite — 结构化任务追踪工具
    ///
    /// 元能力工具：
    /// AI 在处理复杂多步任务时，创建和维护结构化任务列表。
    /// 每次调用发送完整列表（全量替换），或 merge=true 增量更新。
    ///
    /// 两层结构：level 0 = 阶段/里程碑，level 1 = 具体子步骤。
    /// activeForm = 任务进行时的进行时描述（如 "正在添加解析器"）。
    ///
    /// 前端联动：
    /// 前端 TodoPanel 从消息流中提取最后一次 todo_write 调用的 todos 参数，
    /// 渲染为可折叠任务清单 + 进度条。
    ///
    /// 对齐 Reasonix builtin/todo_write.go 的设计。
    /// </summary>
    public class TodoWriteHandler : IToolHandler
    {
        private readonly IEventSink _sink;

        // 当前会话任务列表（静态，同一进程内共享）
        private static readonly List<TodoItem> _todos = new List<TodoItem>();
        private static readonly object _lock = new object();

        public TodoWriteHandler(IEventSink sink = null)
        {
            _sink = sink;
        }

        public string Name => "todo_write";

        public string Description =>
            "Record and update a structured task list for the current work. " +
            "Send the COMPLETE list every call — it replaces the previous one (or merge=true for incremental update). " +
            "Use it to plan multi-step work and show progress: keep exactly one item in_progress at a time, " +
            "and flip an item to completed the moment it's done (don't batch completions). " +
            "Skip it for trivial single-step tasks. " +
            "The list is two-level: a `level` 0 item is a PHASE (a milestone) and the `level` 1 items after it " +
            "are its concrete sub-steps; omit `level` (0) for a flat list. " +
            "Each item has `content` (imperative, e.g. \"Add the parser\"), `status` (pending|in_progress|completed), " +
            "`activeForm` (present-continuous shown while in progress, e.g. \"Adding the parser\"), " +
            "and optional `level` (0 phase | 1 sub-step).";

        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""merge"": {
      ""type"": ""boolean"",
      ""description"": ""true = merge by id (update existing, add new). false = replace all (default: false). merge=true 时按 id 合并更新，false 时全量替换""
    },
    ""todos"": {
      ""type"": ""array"",
      ""description"": ""The complete task list, in order. Replaces any previous list (unless merge=true)."",
      ""items"": {
        ""type"": ""object"",
        ""properties"": {
          ""id"": {
            ""type"": ""string"",
            ""description"": ""Unique identifier for the todo item. Use a short random string. 任务唯一标识""
          },
          ""content"": {
            ""type"": ""string"",
            ""description"": ""Imperative description of the task (e.g. Add the parser). 任务描述""
          },
          ""status"": {
            ""type"": ""string"",
            ""enum"": [""pending"", ""in_progress"", ""completed""],
            ""description"": ""Task state. Keep at most one in_progress. 任务状态""
          },
          ""activeForm"": {
            ""type"": ""string"",
            ""description"": ""Present-continuous form shown while the task is in progress (e.g. Adding the parser). 进行时描述""
          },
          ""level"": {
            ""type"": ""integer"",
            ""enum"": [0, 1],
            ""description"": ""Nesting level: 0 = phase/milestone, 1 = a sub-step of the phase above it. Omit for a flat list. 层级：0=阶段, 1=子步骤""
          }
        },
        ""required"": [""id"", ""content"", ""status""]
      }
    }
  },
  ""required"": [""merge"", ""todos""]
}";

        // ReadOnly=true：todo_write 只是记录列表（无文件系统或进程副作用），
        // 不需要审批，在 Plan Mode 下也可用。
        public bool IsReadOnly => true;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            await Task.CompletedTask;

            try
            {
                var json = JObject.Parse(args);
                bool merge = json.Value<bool?>("merge") ?? false;

                var todosArray = json["todos"] as JArray;
                if (todosArray == null || todosArray.Count == 0)
                    return ToolResult.Fail("todos array is required and must not be empty");

                // 解析输入
                var inputItems = new List<TodoItem>();
                foreach (var token in todosArray)
                {
                    string id = token.Value<string>("id");
                    string content = token.Value<string>("content");
                    string status = (token.Value<string>("status") ?? "pending").ToLowerInvariant();
                    string activeForm = token.Value<string>("activeForm");
                    int level = token.Value<int?>("level") ?? 0;

                    if (string.IsNullOrWhiteSpace(id))
                        return ToolResult.Fail("Each todo item requires an 'id'");
                    if (string.IsNullOrWhiteSpace(content))
                        return ToolResult.Fail($"Todo item '{id}' is missing 'content'");
                    if (level < 0 || level > 1)
                        return ToolResult.Fail($"Todo item '{id}': invalid level {level} (want 0=phase | 1=sub-step)");

                    // 规范化 status
                    status = NormalizeStatus(status);

                    inputItems.Add(new TodoItem
                    {
                        Id = id,
                        Content = content.Length > 200 ? content.Substring(0, 197) + "..." : content,
                        Status = status,
                        ActiveForm = activeForm,
                        Level = level,
                        CreatedAt = DateTime.UtcNow
                    });
                }

                lock (_lock)
                {
                    if (merge)
                    {
                        // Merge 模式：按 id 合并
                        foreach (var item in inputItems)
                        {
                            var existing = _todos.Find(t => t.Id == item.Id);
                            if (existing != null)
                            {
                                existing.Content = item.Content;
                                existing.Status = item.Status;
                                existing.ActiveForm = item.ActiveForm;
                                existing.Level = item.Level;
                            }
                            else
                            {
                                _todos.Add(item);
                            }
                        }
                    }
                    else
                    {
                        // 全量替换
                        _todos.Clear();
                        _todos.AddRange(inputItems);
                    }
                }

                // 格式化输出
                int completed, inProgress, pending, cancelled;
                string output;
                lock (_lock)
                {
                    completed = _todos.Count(t => t.Status == "completed");
                    inProgress = _todos.Count(t => t.Status == "in_progress");
                    pending = _todos.Count(t => t.Status == "pending");
                    cancelled = _todos.Count(t => t.Status == "cancelled");

                    var sb = new StringBuilder();
                    string modeStr = merge ? "updated (merge)" : "created (replace)";
                    sb.AppendLine($"Todos updated: {_todos.Count} total — {completed} completed, {inProgress} in progress, {pending} pending.");
                    sb.AppendLine();

                    foreach (var t in _todos)
                    {
                        string icon = t.Status == "completed" ? "✓" :
                                      t.Status == "in_progress" ? "→" :
                                      t.Status == "cancelled" ? "✗" : "○";
                        string indent = t.Level == 1 ? "    " : "  ";
                        string levelTag = t.Level == 1 ? "[子] " : "";
                        string activeTag = t.Status == "in_progress" && !string.IsNullOrEmpty(t.ActiveForm)
                            ? $" ({t.ActiveForm})" : "";
                        sb.AppendLine($"{indent}{icon} [{t.Id}] {levelTag}{t.Content}{activeTag}");
                    }

                    output = sb.ToString().TrimEnd();
                }

                _sink?.Emit(CopilotEvent.Notice($"TodoWrite: {completed}/{_todos.Count} done"));

                return ToolResult.Ok(output, new
                {
                    merge,
                    inputCount = inputItems.Count,
                    totalCount = _todos.Count,
                    completed,
                    inProgress,
                    pending,
                    cancelled
                });
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                return ToolResult.Fail($"Invalid JSON arguments: {ex.Message}");
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"TodoWrite failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取当前任务列表（供 CompleteStepHandler 查询）
        /// </summary>
        public static List<TodoItem> GetTodos()
        {
            lock (_lock)
            {
                return _todos.ToList();
            }
        }

        /// <summary>
        /// 自动推进 todo 状态：将匹配的 todo 标记为 completed，下一个 pending 标记为 in_progress
        /// </summary>
        public static bool AdvanceTodo(string stepIdentifier)
        {
            lock (_lock)
            {
                // 查找匹配的 todo（按 id 或 content 模糊匹配）
                var match = _todos.Find(t =>
                    t.Id == stepIdentifier ||
                    t.Content.Equals(stepIdentifier, StringComparison.OrdinalIgnoreCase) ||
                    t.Content.Contains(stepIdentifier));

                if (match == null || match.Status == "completed")
                    return false;

                // 标记当前为 completed
                match.Status = "completed";

                // 找下一个 pending，标记为 in_progress
                var nextPending = _todos.Find(t => t.Status == "pending");
                if (nextPending != null)
                {
                    nextPending.Status = "in_progress";
                }

                return true;
            }
        }

        /// <summary>
        /// 规范化状态字符串，兼容各种输入（大写/小写/别名）
        /// </summary>
        private static string NormalizeStatus(string status)
        {
            if (string.IsNullOrEmpty(status)) return "pending";

            switch (status.ToLowerInvariant())
            {
                case "pending":
                case "p":
                    return "pending";
                case "in_progress":
                case "inprogress":
                case "active":
                case "working":
                    return "in_progress";
                case "completed":
                case "complete":
                case "done":
                case "finished":
                    return "completed";
                case "cancelled":
                case "canceled":
                case "skip":
                case "skipped":
                    return "cancelled";
                default:
                    return "pending";
            }
        }

        public class TodoItem
        {
            public string Id { get; set; }
            public string Content { get; set; }
            public string Status { get; set; }
            public string ActiveForm { get; set; }
            public int Level { get; set; }  // 0=phase, 1=sub-step
            public DateTime CreatedAt { get; set; }
        }
    }
}
