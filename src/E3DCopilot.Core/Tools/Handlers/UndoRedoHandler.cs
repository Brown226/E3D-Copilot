using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Events;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// UndoRedo — 撤销/重做修改操作
    /// 
    /// 元能力工具：
    /// 追踪 modify/design/piping 的修改历史，支持撤销和重做。
    /// 每次修改前自动记录旧值，undo 恢复旧值，redo 重新应用新值。
    /// </summary>
    public class UndoRedoHandler : IToolHandler
    {
        private readonly IToolDispatcher _dispatcher;
        private readonly Events.IEventSink _sink;

        public UndoRedoHandler(IToolDispatcher dispatcher, Events.IEventSink sink = null)
        {
            _dispatcher = dispatcher;
            _sink = sink;
        }

        public string Name => "undo_redo";
        public string Description =>
            "Undo or redo the last modification. 撤销或重做最近一次修改操作。" +
            "Use undo to revert the last change, redo to re-apply it. " +
            "操作历史最多保存 50 条。";
        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""action"": {
      ""type"": ""string"",
      ""enum"": [""undo"", ""redo"", ""status""],
      ""description"": ""undo=撤销最近一次修改, redo=重做, status=查看历史""
    },
    ""count"": {
      ""type"": ""integer"",
      ""description"": ""连续撤销/重做的次数（默认 1，最大 10）""
    }
  },
  ""required"": [""action""]
}";
        public bool IsReadOnly => false;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                var json = Newtonsoft.Json.Linq.JObject.Parse(args);
                string action = json.Value<string>("action") ?? "status";
                int count = json.Value<int?>("count") ?? 1;
                count = Math.Max(1, Math.Min(count, 10));

                var manager = UndoRedoManager.Instance;

                switch (action.ToLower())
                {
                    case "undo":
                        return await UndoAsync(manager, count, ct);

                    case "redo":
                        return await RedoAsync(manager, count, ct);

                    case "status":
                        return GetStatus(manager);

                    default:
                        return ToolResult.Fail($"Unknown action: {action}. Use 'undo', 'redo', or 'status'.");
                }
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"UndoRedo failed: {ex.Message}");
            }
        }

        private async Task<ToolResult> UndoAsync(UndoRedoManager manager, int count, CancellationToken ct)
        {
            var sb = new StringBuilder();
            int undone = 0;

            for (int i = 0; i < count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var entry = manager.PopUndo();
                if (entry == null) break;

                // 恢复旧值
                string modifyArgs = Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    dburi = entry.Element,
                    attribute = entry.Attribute,
                    value = entry.OldValue
                });

                var result = await _dispatcher.ExecuteAsync("modify", modifyArgs);

                // 检查 modify 是否成功
                bool modifyOk = true;
                if (!string.IsNullOrEmpty(result) && result.StartsWith("{"))
                {
                    try
                    {
                        var j = Newtonsoft.Json.Linq.JObject.Parse(result);
                        var successToken = j["success"];
                        if (successToken != null && successToken.Value<bool>() == false)
                            modifyOk = false;
                    }
                    catch { }
                }

                if (!modifyOk)
                {
                    sb.AppendLine($"✗ 撤销失败: {entry.Element}.{entry.Attribute}");
                    break;
                }

                manager.PushRedo(entry);
                undone++;

                sb.AppendLine($"✓ 撤销: {entry.Element}.{entry.Attribute} = {entry.OldValue}");
            }

            if (undone == 0)
                return ToolResult.Ok("没有可撤销的操作。", new { undoAvailable = manager.UndoCount });

            _sink?.Emit(CopilotEvent.Notice($"Undo: {undone} operation(s) reverted"));
            return ToolResult.Ok(sb.ToString().TrimEnd(), new
            {
                undone,
                undoAvailable = manager.UndoCount,
                redoAvailable = manager.RedoCount
            });
        }

        private async Task<ToolResult> RedoAsync(UndoRedoManager manager, int count, CancellationToken ct)
        {
            var sb = new StringBuilder();
            int redone = 0;

            for (int i = 0; i < count; i++)
            {
                ct.ThrowIfCancellationRequested();

                var entry = manager.PopRedo();
                if (entry == null) break;

                // 重新应用新值
                string modifyArgs = Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    dburi = entry.Element,
                    attribute = entry.Attribute,
                    value = entry.NewValue
                });

                var result = await _dispatcher.ExecuteAsync("modify", modifyArgs);

                // 检查 modify 是否成功
                bool modifyOk = true;
                if (!string.IsNullOrEmpty(result) && result.StartsWith("{"))
                {
                    try
                    {
                        var j = Newtonsoft.Json.Linq.JObject.Parse(result);
                        var successToken = j["success"];
                        if (successToken != null && successToken.Value<bool>() == false)
                            modifyOk = false;
                    }
                    catch { }
                }

                if (!modifyOk)
                {
                    sb.AppendLine($"✗ 重做失败: {entry.Element}.{entry.Attribute}");
                    break;
                }

                manager.PushUndo(entry);
                redone++;

                sb.AppendLine($"✓ 重做: {entry.Element}.{entry.Attribute} = {entry.NewValue}");
            }

            if (redone == 0)
                return ToolResult.Ok("没有可重做的操作。", new { redoAvailable = manager.RedoCount });

            _sink?.Emit(CopilotEvent.Notice($"Redo: {redone} operation(s) re-applied"));
            return ToolResult.Ok(sb.ToString().TrimEnd(), new
            {
                redone,
                undoAvailable = manager.UndoCount,
                redoAvailable = manager.RedoCount
            });
        }

        private ToolResult GetStatus(UndoRedoManager manager)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"撤销历史: {manager.UndoCount} 条");
            sb.AppendLine($"重做历史: {manager.RedoCount} 条");

            if (manager.UndoCount > 0)
            {
                sb.AppendLine();
                sb.AppendLine("最近修改:");
                // 注意：PeekUndo 只能看到最近一条
                var last = manager.PeekUndo();
                if (last != null)
                    sb.AppendLine($"  {last.Timestamp:HH:mm:ss} {last.Element}.{last.Attribute}: {last.OldValue} → {last.NewValue}");
            }

            return ToolResult.Ok(sb.ToString().TrimEnd(), new
            {
                undoAvailable = manager.UndoCount,
                redoAvailable = manager.RedoCount
            });
        }
    }
}
