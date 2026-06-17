using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Config;
using E3DCopilot.Core.Events;
using E3DCopilot.Core.Logging;
using E3DCopilot.Core.Providers;
using E3DCopilot.Core.Security;
using E3DCopilot.Core.Tools;

namespace E3DCopilot.Core
{
    /// <summary>
    /// 核心 Agent 循环：LLM 调用 → ToolExecutor 调度 → 结果注入 → 循环
    /// net48 兼容：回调模式流式处理，无 IAsyncEnumerable
    /// 对应 cline-chinese-main 的 Task 引擎 + ToolExecutor
    /// </summary>
    public class AgentLoop
    {
        private readonly ICopilotProvider _provider;
        private readonly IEventSink _sink;
        private readonly ToolExecutor _executor;
        private readonly CommandPermissionController _permission;
        private readonly CopilotConfig _config;
        private readonly CopilotController _controller;

        private const int MaxSteps = 20;

        public AgentLoop(ICopilotProvider provider, IEventSink sink,
            ToolExecutor executor, CommandPermissionController permission,
            CopilotConfig config, CopilotController controller = null)
        {
            _provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _sink = sink;
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _permission = permission ?? CommandPermissionController.CreateDefault();
            _config = config ?? CopilotConfig.Load();
            _controller = controller;
        }

        /// <summary>
        /// 运行 Agent 循环
        /// </summary>
        public async Task RunAsync(CopilotSession session, string input,
            CancellationToken ct = default)
        {
            session.AddUserMessage(input);

            _sink.Emit(new CopilotEvent
            {
                Kind = EventKind.TurnStarted,
                Text = $"处理: {input}"
            });

            for (int step = 0; step < MaxSteps; step++)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    // 1. 组装请求（含可用工具定义）
                    var request = BuildRequest(session);

                    // 2. 调用 LLM（流式）
                    var result = await StreamLlmAsync(request, ct);

                    // 3. 保存助手消息
                    session.AddAssistantMessage(result.Text, result.ToolCalls);

                    // 4. 无工具调用 → 完成
                    if (result.ToolCalls == null || result.ToolCalls.Count == 0)
                    {
                        _sink.Emit(CopilotEvent.TurnDone());
                        return;
                    }

                    // 5. 通过 ToolExecutor 执行工具
                    foreach (var call in result.ToolCalls)
                    {
                        ct.ThrowIfCancellationRequested();

                        // 5a. 权限检查（CommandPermissionController）
                        var access = _permission.CheckTool(call.Name, call.Arguments);
                        if (access == CommandPermissionController.AccessMode.Block)
                        {
                            string msg = $"工具 {call.Name} 被策略阻止";
                            session.AddToolResult(call.Id, msg);
                            _sink.Emit(CopilotEvent.Error(msg));
                            continue;
                        }

                        // 5b. 批量操作检测（>5个元素需额外确认）
                        bool isBatch = _permission.IsBatchOperation(call.Arguments);

                        // 5c. 需要审批？
                        if (access == CommandPermissionController.AccessMode.Ask || isBatch)
                        {
                            var approval = new PendingApproval
                            {
                                ToolName = call.Name,
                                Args = call.Arguments,
                                Description = $"{call.Name}({call.Arguments})"
                                    + (isBatch ? " [批量操作]" : "")
                            };

                            // 通过 Controller 注册审批请求（如果有 Controller）
                            if (_controller != null)
                            {
                                _controller.RegisterApproval(approval);
                            }
                            else
                            {
                                // 降级：直接 emit 事件（兼容旧代码）
                                _sink.Emit(new CopilotEvent
                                {
                                    Kind = EventKind.ApprovalRequest,
                                    Data = approval
                                });
                            }

                            var approvalResult = await approval.WaitAsync();
                            if (!approvalResult.Allow)
                            {
                                string msg = $"用户拒绝了 {call.Name}";
                                session.AddToolResult(call.Id, msg);
                                _sink.Emit(CopilotEvent.Error(msg));
                                continue;
                            }
                        }

                        // 5d. 通过 ToolExecutor 执行
                        var toolResult = await _executor.ExecuteAsync(
                            call.Name, call.Arguments, ct);
                        session.AddToolResult(call.Id,
                            toolResult.Success ? toolResult.Text : toolResult.Error);
                    }

                    // 6. 上下文压缩（可选）
                    MaybeCompact(session);
                }
                catch (OperationCanceledException)
                {
                    _sink.Emit(CopilotEvent.Notice("已取消"));
                    return;
                }
                catch (Exception ex)
                {
                    CopilotLogger.Error(ex, "AgentLoop 步骤 {0} 失败", step);
                    _sink.Emit(CopilotEvent.Error($"遇到错误: {ex.Message}"));
                }
            }

            _sink.Emit(CopilotEvent.Notice($"已达最大步骤数 {MaxSteps}"));
            _sink.Emit(CopilotEvent.TurnDone());
        }

        /// <summary>
        /// 流式调用 LLM（net48 回调模式）
        /// </summary>
        private async Task<(string Text, List<ToolCall> ToolCalls)> StreamLlmAsync(
            CopilotRequest request, CancellationToken ct)
        {
            string text = "";
            var toolCalls = new List<ToolCall>();

            await _provider.StreamAsync(request, chunk =>
            {
                switch (chunk.Type)
                {
                    case ChunkType.Reasoning:
                        text += chunk.Content;
                        _sink.Emit(CopilotEvent.Reasoning(chunk.Content));
                        break;
                    case ChunkType.Text:
                        text += chunk.Content;
                        _sink.Emit(CopilotEvent.TextEvent(chunk.Content));
                        break;
                    case ChunkType.ToolCall:
                        MergeToolCall(toolCalls, chunk.ToolCall);
                        break;
                    case ChunkType.Usage:
                        _sink.Emit(new CopilotEvent
                        {
                            Kind = EventKind.Usage,
                            Text = $"Token: {chunk.UsageData.TotalTokens}"
                        });
                        break;
                }
            }, ct);

            return (text, toolCalls);
        }

        /// <summary>
        /// 合并增量工具调用（vLLM 流式 tool_calls 可能分片）
        /// </summary>
        private void MergeToolCall(List<ToolCall> existing, ToolCall incoming)
        {
            var match = existing.FirstOrDefault(t => t.Id == incoming.Id);
            if (match != null)
            {
                if (!string.IsNullOrEmpty(incoming.Name))
                    match.Name += incoming.Name;
                if (!string.IsNullOrEmpty(incoming.Arguments))
                    match.Arguments += incoming.Arguments;
            }
            else
            {
                existing.Add(incoming);
            }
        }

        /// <summary>
        /// 构建 LLM 请求（含工具定义）
        /// </summary>
        private CopilotRequest BuildRequest(CopilotSession session)
        {
            var request = new CopilotRequest
            {
                Model = _config.Llm.Model,
                Temperature = _config.Llm.Temperature,
                MaxTokens = _config.Llm.MaxTokens,
                Messages = new List<ChatMessage>()
            };

            // System Prompt
            request.Messages.Add(new ChatMessage(MessageRole.System,
                SystemPrompt.Build()));

            // 工具定义（从 ToolExecutor 获取）
            var toolSchemas = new List<ToolSchema>();
            foreach (var handler in _executor.GetAllHandlers())
            {
                toolSchemas.Add(new ToolSchema
                {
                    Name = handler.Name,
                    Description = handler.Description,
                    ParametersJson = handler.ParameterSchema
                });
            }
            request.Tools = toolSchemas;

            // 历史消息（最近 20 条）
            request.Messages.AddRange(session.GetRecentMessages(20));

            return request;
        }

        private void MaybeCompact(CopilotSession session)
        {
            // Phase 3: 上下文压缩
        }
    }
}
