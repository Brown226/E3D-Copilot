using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core
{
    /// <summary>
    /// 证据账本 — 记录当前 turn 内的工具执行回执
    /// 对齐 Reasonix evidence.Ledger:
    ///   - 记录每个工具调用的成功/失败
    ///   - 跟踪最新 todo_write 列表
    ///   - Final Readiness Check: 检查未完成的 todo 项
    /// </summary>
    public class EvidenceLedger
    {
        private readonly List<Receipt> _receipts = new List<Receipt>();

        /// <summary>
        /// 每 turn 开始时清空
        /// </summary>
        public void Reset()
        {
            _receipts.Clear();
        }

        /// <summary>
        /// 记录一次工具执行回执
        /// </summary>
        public void Record(string toolName, string args, bool success, bool isReadOnly)
        {
            var receipt = new Receipt
            {
                ToolName = toolName,
                Args = args,
                Success = success,
                IsWrite = !isReadOnly && IsWriteTool(toolName)
            };

            // 从 todo_write 中解析 todo 列表
            if (toolName == "todo_write" && success && !string.IsNullOrEmpty(args))
            {
                try
                {
                    var jobj = JObject.Parse(args);
                    var todosArray = jobj["todos"] as JArray;
                    if (todosArray != null)
                    {
                        receipt.Todos = new List<TodoEntry>();
                        foreach (var t in todosArray)
                        {
                            receipt.Todos.Add(new TodoEntry
                            {
                                Content = t["content"]?.Value<string>() ?? "",
                                Status = t["status"]?.Value<string>() ?? "pending"
                            });
                        }
                    }
                }
                catch { }
            }

            _receipts.Add(receipt);
        }

        /// <summary>
        /// 是否有任何成功的工具调用
        /// </summary>
        public bool HasAnySuccessfulReceipt()
        {
            return _receipts.Any(r => r.Success);
        }

        /// <summary>
        /// 是否有成功的 todo_write
        /// </summary>
        public bool HasSuccessfulTodoWrite()
        {
            return _receipts.Any(r => r.Success && r.ToolName == "todo_write");
        }

        /// <summary>
        /// 是否有任何成功的工具调用（表示有实际执行进度，排除纯 todo_write）
        /// </summary>
        public bool HasSuccessfulTodoProgressReceipt()
        {
            return _receipts.Any(r => r.Success && r.ToolName != "todo_write");
        }

        /// <summary>
        /// 获取最新成功 todo_write 中未完成的项
        /// </summary>
        public List<TodoEntry> IncompleteLatestTodos()
        {
            for (int i = _receipts.Count - 1; i >= 0; i--)
            {
                var r = _receipts[i];
                if (r.Success && r.ToolName == "todo_write" && r.Todos != null)
                {
                    return r.Todos
                        .Where(t => NormalizeStatus(t.Status) != "completed")
                        .Select(t => new TodoEntry
                        {
                            Content = t.Content,
                            Status = NormalizeStatus(t.Status)
                        })
                        .ToList();
                }
            }
            return null;
        }

        /// <summary>
        /// Final Readiness Check — 检查是否有未完成的 todo 项应阻止最终回答
        /// 返回 null 表示通过，返回字符串表示阻塞原因
        /// </summary>
        public string CheckReadiness()
        {
            var incomplete = IncompleteLatestTodos();
            if (incomplete == null || incomplete.Count == 0)
                return null;

            // 只有当模型确实做了实际工作（非纯 todo_write 操作）时才检查
            if (!HasSuccessfulTodoProgressReceipt())
                return null;

            var parts = incomplete.Select(t => $"{t.Content}: {t.Status}").ToList();
            return "latest successful todo_write still has incomplete items: " + string.Join(", ", parts) +
                   ". Complete the remaining items or explain why they are blocked before giving a final answer.";
        }

        private static string NormalizeStatus(string status)
        {
            status = (status ?? "").Trim();
            return string.IsNullOrEmpty(status) ? "pending" : status;
        }

        private static bool IsWriteTool(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            switch (name)
            {
                case "write_file":
                case "modify":
                case "design":
                case "piping":
                case "execute_pml":
                case "batch":
                case "cad_import":
                case "autocad":
                case "export":
                    return true;
                default:
                    return false;
            }
        }

        private class Receipt
        {
            public string ToolName { get; set; }
            public string Args { get; set; }
            public bool Success { get; set; }
            public bool IsWrite { get; set; }
            public List<TodoEntry> Todos { get; set; }
        }
    }

    /// <summary>
    /// Todo 条目（用于 EvidenceLedger 内部跟踪）
    /// </summary>
    public class TodoEntry
    {
        public string Content { get; set; }
        public string Status { get; set; }
    }
}
