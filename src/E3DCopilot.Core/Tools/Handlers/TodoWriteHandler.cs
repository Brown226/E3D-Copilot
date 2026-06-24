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
    /// TodoWrite — 结构化任务追踪工具（替代 task 工具的升级版）
    ///
    /// 元能力工具：
    /// AI 在处理复杂多步任务时，创建和维护结构化任务列表。
    /// 支持 merge 模式（增量更新）和全量替换。
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
            "Create and manage a structured task list to track complex multi-step work. " +
            "Use when: (1) breaking a complex request into steps, (2) tracking progress of multi-step operations, " +
            "(3) updating task status as you work through them. " +
            "创建和管理结构化任务列表。支持 merge（增量更新）和全量替换模式。";

        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""merge"": {
      ""type"": ""boolean"",
      ""description"": ""true = merge by id (update existing, add new). false = replace all (default: false). merge=true 时按 id 合并更新，false 时全量替换""
    },
    ""todos"": {
      ""type"": ""array"",
      ""items"": {
        ""type"": ""object"",
        ""properties"": {
          ""id"": {
            ""type"": ""string"",
            ""description"": ""Unique identifier for the todo item. Use a short random string like 'r9Tg8Kq2pLm7'. 任务唯一标识""
          },
          ""content"": {
            ""type"": ""string"",
            ""description"": ""Task description (max 70 chars recommended). 任务描述""
          },
          ""status"": {
            ""type"": ""string"",
            ""enum"": [""pending"", ""in_progress"", ""completed"", ""cancelled""],
            ""description"": ""Task status: PENDING → IN_PROGRESS → COMPLETE/CANCELLED. 任务状态流转""
          }
        },
        ""required"": [""id"", ""content"", ""status""]
      },
      ""description"": ""Array of todo items (minimum 2 recommended). 任务项数组""
    }
  },
  ""required"": [""merge"", ""todos""]
}";

        public bool IsReadOnly => false; // 有状态写操作，不应并行执行

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
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

                    if (string.IsNullOrWhiteSpace(id))
                        return ToolResult.Fail("Each todo item requires an 'id'");
                    if (string.IsNullOrWhiteSpace(content))
                        return ToolResult.Fail($"Todo item '{id}' is missing 'content'");

                    // 规范化 status
                    status = NormalizeStatus(status);

                    inputItems.Add(new TodoItem
                    {
                        Id = id,
                        Content = content.Length > 200 ? content.Substring(0, 197) + "..." : content,
                        Status = status,
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
                    sb.AppendLine($"Todo list {modeStr}: {inputItems.Count} items, {_todos.Count} total");
                    sb.AppendLine($"Progress: {completed}/{_todos.Count} completed" +
                                  (inProgress > 0 ? $", {inProgress} in progress" : "") +
                                  (pending > 0 ? $", {pending} pending" : "") +
                                  (cancelled > 0 ? $", {cancelled} cancelled" : ""));
                    sb.AppendLine();

                    foreach (var t in _todos)
                    {
                        string icon = t.Status == "completed" ? "✓" :
                                      t.Status == "in_progress" ? "→" :
                                      t.Status == "cancelled" ? "✗" : "○";
                        sb.AppendLine($"  {icon} [{t.Id}] {t.Content}");
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

        private class TodoItem
        {
            public string Id { get; set; }
            public string Content { get; set; }
            public string Status { get; set; }
            public DateTime CreatedAt { get; set; }
        }
    }
}
