using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Events;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// AskUser — AI 向用户提问并等待回答
    /// 
    /// 元能力工具：
    /// AI 遇到歧义、缺少信息、或需要用户确认时，可以调用此工具提问。
    /// 问题通过 IEventSink 发送到前端，用户回答后通过回调返回。
    /// 
    /// 参考 cline-chinese-main 的 ask_user tool 模式
    /// </summary>
    public class AskUserHandler : IToolHandler
    {
        // 跟踪待处理的问题，key = questionId
        private static readonly Dictionary<string, TaskCompletionSource<string>> _pendingQuestions
            = new Dictionary<string, TaskCompletionSource<string>>();

        private readonly IEventSink _sink;

        // 可选的回调 — 由 CopilotController/UI 层设置，用于接收回答
        public static Action<string, string> OnQuestionAnswered;

        public AskUserHandler(IEventSink sink = null)
        {
            _sink = sink;
        }

        public string Name => "ask_user";
        public string Description => "Ask the user a question and wait for their response. Use when: (1) the request is ambiguous, (2) you need specific values the user didn't provide, (3) you need confirmation before a high-impact operation, (4) you need to clarify intent. 向用户提问并等待回答。用于消除歧义、补充信息、或操作前确认。";

        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""question"": { ""type"": ""string"", ""description"": ""The question to ask the user. Should be specific and directly answerable. 向用户提出的问题，应具体明确，可直接回答。"" },
    ""options"": { ""type"": ""array"", ""items"": { ""type"": ""string"" }, ""description"": ""Optional: list of predefined answer options for the user to choose from. 可选的预定义选项列表"" },
    ""multiSelect"": { ""type"": ""boolean"", ""description"": ""Whether the user can select multiple options (only works with options provided)"" }
  },
  ""required"": [""question""]
}";
        public bool IsReadOnly => true;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                // 解析 JSON 参数
                var json = Newtonsoft.Json.Linq.JObject.Parse(args);
                string question = json.Value<string>("question");

                if (string.IsNullOrWhiteSpace(question))
                    return ToolResult.Fail("Question cannot be empty");

                string questionId = Guid.NewGuid().ToString("N");

                // 解析可选参数
                var optionsList = new List<string>();
                if (json["options"] is Newtonsoft.Json.Linq.JArray optArray)
                {
                    foreach (var opt in optArray)
                        optionsList.Add(opt.ToString());
                }
                bool multiSelect = json.Value<bool?>("multiSelect") ?? false;

                // 发出事件 — 前端需要显示对话框让用户输入
                _sink?.Emit(new CopilotEvent
                {
                    Kind = EventKind.AskUser,
                    ToolId = questionId,
                    Text = "ask_user",
                    Data = new
                    {
                        questionId,
                        question,
                        options = optionsList.Count > 0 ? optionsList : null,
                        multiSelect = multiSelect && optionsList.Count > 0
                    }
                });

                // 创建 TaskCompletionSource 等待用户回答
                var tcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
                lock (_pendingQuestions)
                {
                    _pendingQuestions[questionId] = tcs;
                }

                // 超时：10 分钟后自动取消
                using (var cts = CancellationTokenSource.CreateLinkedTokenSource(ct))
                {
                    cts.CancelAfter(TimeSpan.FromMinutes(10));
                    using (cts.Token.Register(() => tcs.TrySetCanceled(), false))
                    {
                        try
                        {
                            string answer = await tcs.Task;
                            return ToolResult.Ok($"User response: {answer}", new
                            {
                                questionId,
                                question,
                                answer
                            });
                        }
                        catch (OperationCanceledException)
                        {
                            lock (_pendingQuestions)
                                _pendingQuestions.Remove(questionId);

                            return ToolResult.Fail("User did not respond within the time limit. 用户未在时限内回答。请尝试重新提问或换一种方式表达。");
                        }
                    }
                }
            }
            catch (Newtonsoft.Json.JsonException ex)
            {
                return ToolResult.Fail($"Invalid JSON arguments: {ex.Message}");
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"AskUser failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 提交用户回答（由 UI 层/桥接层调用）
        /// </summary>
        public static bool SubmitAnswer(string questionId, string answer)
        {
            lock (_pendingQuestions)
            {
                if (_pendingQuestions.TryGetValue(questionId, out var tcs))
                {
                    _pendingQuestions.Remove(questionId);
                    tcs.TrySetResult(answer ?? "");
                    return true;
                }
            }
            return false; // 问题不存在或已超时
        }
    }
}
