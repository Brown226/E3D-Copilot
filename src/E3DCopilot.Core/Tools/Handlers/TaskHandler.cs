using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Events;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// Task — 子任务追踪工具
    /// 
    /// 元能力工具：
    /// AI 在处理复杂多步任务时，可以创建子任务列表来追踪进度。
    /// 类似 cline-chinese-main 的 task tool。
    /// 
    /// 操作模式：
    /// - create: 创建新任务列表
    /// - update: 更新某个任务的状态
    /// - list: 列出所有任务
    /// </summary>
    public class TaskHandler : IToolHandler
    {
        private readonly IEventSink _sink;

        // 当前会话任务列表（静态，同一进程内共享）
        private static readonly List<TaskItem> _tasks = new List<TaskItem>();
        private static int _nextId = 1;

        public TaskHandler(IEventSink sink = null)
        {
            _sink = sink;
        }

        public string Name => "task";
        public string Description => "Track and manage sub-tasks for complex multi-step operations. Use when: (1) you need to break down a complex request into steps, (2) you want to show progress of a multi-step task, (3) you need to update task status as you work. 创建和管理子任务列表，适合复杂多步操作时的进度追踪。";

        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""operation"": {
      ""type"": ""string"",
      ""enum"": [""create"", ""update"", ""list""],
      ""description"": ""Operation type: create(new task list), update(update task status), list(list all tasks). 操作类型：create(创建), update(更新), list(列表)""
    },
    ""tasks"": {
      ""type"": ""array"",
      ""items"": { ""type"": ""object"", ""properties"": { ""description"": { ""type"": ""string"" } } },
      ""description"": ""[create] List of sub-task descriptions to create. 创建时的子任务描述列表""
    },
    ""id"": {
      ""type"": ""integer"",
      ""description"": ""[update] Task ID to update. 要更新的任务ID""
    },
    ""status"": {
      ""type"": ""string"",
      ""enum"": [""pending"", ""in_progress"", ""completed"", ""failed""],
      ""description"": ""[update] New status for the task""
    }
  },
  ""required"": [""operation""]
}";

        public bool IsReadOnly => true;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            await Task.CompletedTask; // 同步完成

            try
            {
                var json = Newtonsoft.Json.Linq.JObject.Parse(args);
                string operation = json.Value<string>("operation")?.ToLowerInvariant();

                switch (operation)
                {
                    case "create":
                        return CreateTasks(json);
                    case "update":
                        return UpdateTask(json);
                    case "list":
                        return ListTasks();
                    default:
                        return ToolResult.Fail($"Unknown task operation: {operation}. Supported: create, update, list");
                }
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                return ToolResult.Fail($"Invalid JSON arguments: {ex.Message}");
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"Task failed: {ex.Message}");
            }
        }

        private ToolResult CreateTasks(Newtonsoft.Json.Linq.JObject json)
        {
            // 可选：如果已有任务，可以追加或重置
            bool append = json.Value<bool?>("append") ?? false;
            if (!append)
                _tasks.Clear();

            var taskDescriptions = new List<string>();
            if (json["tasks"] is Newtonsoft.Json.Linq.JArray taskArray)
            {
                foreach (var t in taskArray)
                {
                    string desc = t["description"]?.ToString() ?? t.ToString();
                    if (!string.IsNullOrWhiteSpace(desc))
                        taskDescriptions.Add(desc);
                }
            }

            if (taskDescriptions.Count == 0)
                return ToolResult.Fail("No task descriptions provided");

            int startId = _nextId;
            foreach (var desc in taskDescriptions)
            {
                _tasks.Add(new TaskItem
                {
                    Id = _nextId++,
                    Description = desc,
                    Status = "pending",
                    CreatedAt = DateTime.Now
                });
            }

            var result = new System.Text.StringBuilder();
            result.AppendLine($"Created {taskDescriptions.Count} tasks (IDs {startId}-{_nextId - 1}):");
            for (int i = 0; i < taskDescriptions.Count; i++)
            {
                result.AppendLine($"  [{startId + i}] {taskDescriptions[i]} (pending)");
            }

            _sink?.Emit(CopilotEvent.Notice(result.ToString().TrimEnd()));

            return ToolResult.Ok(result.ToString().TrimEnd(), new
            {
                operation = "create",
                count = taskDescriptions.Count,
                tasks = _tasks.ConvertAll(t => new { t.Id, t.Description, t.Status })
            });
        }

        private ToolResult UpdateTask(Newtonsoft.Json.Linq.JObject json)
        {
            int? id = json.Value<int?>("id");
            string status = json.Value<string>("status")?.ToLowerInvariant();

            if (id == null)
                return ToolResult.Fail("Task id is required for update");

            var task = _tasks.Find(t => t.Id == id.Value);
            if (task == null)
                return ToolResult.Fail($"Task #{id} not found. Use 'task list' to see all tasks.");

            if (!string.IsNullOrEmpty(status))
            {
                var validStatuses = new[] { "pending", "in_progress", "completed", "failed" };
                if (Array.IndexOf(validStatuses, status) < 0)
                    return ToolResult.Fail($"Invalid status '{status}'. Valid: pending, in_progress, completed, failed");

                task.Status = status;
            }

            // 更新描述
            string newDesc = json.Value<string>("description");
            if (!string.IsNullOrEmpty(newDesc))
                task.Description = newDesc;

            string msg = $"Task #{task.Id} [{task.Status}]: {task.Description}";
            _sink?.Emit(CopilotEvent.Notice(msg));

            return ToolResult.Ok(msg, new
            {
                operation = "update",
                task = new { task.Id, task.Description, task.Status }
            });
        }

        private ToolResult ListTasks()
        {
            if (_tasks.Count == 0)
            {
                return ToolResult.Ok("No tasks created yet. Use 'task create' to start tracking.", null);
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Tasks ({_tasks.Count} total):");
            foreach (var t in _tasks)
            {
                string icon = t.Status == "completed" ? "✓" :
                              t.Status == "in_progress" ? "→" :
                              t.Status == "failed" ? "✗" : " ";
                sb.AppendLine($"  {icon} [#{t.Id}] {t.Description} ({t.Status})");
            }

            return ToolResult.Ok(sb.ToString().TrimEnd(), new
            {
                operation = "list",
                count = _tasks.Count,
                tasks = _tasks.ConvertAll(t => new { t.Id, t.Description, t.Status })
            });
        }

        private class TaskItem
        {
            public int Id { get; set; }
            public string Description { get; set; }
            public string Status { get; set; } // pending, in_progress, completed, failed
            public DateTime CreatedAt { get; set; }
        }
    }
}
