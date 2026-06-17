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

namespace E3DCopilot.Core
{
    /// <summary>
    /// 核心 Agent 循环：LLM 调用 → 工具执行 → 结果注入 → 循环
    /// net48 兼容：回调模式流式处理，无 IAsyncEnumerable
    /// </summary>
    public class AgentLoop
    {
        private readonly ICopilotProvider _provider;
        private readonly IEventSink _sink;
        private readonly ToolPolicy _policy;
        private readonly PermissionGate _gate;
        private readonly CopilotConfig _config;

        private const int MaxSteps = 20;

        public AgentLoop(ICopilotProvider provider, IEventSink sink,
            ToolPolicy policy, PermissionGate gate, CopilotConfig config)
        {
            _provider = provider;
            _sink = sink;
            _policy = policy;
            _gate = gate;
            _config = config;
        }

        /// <summary>
        /// 运行 Agent 循环
        /// </summary>
        public async Task RunAsync(CopilotSession session, string input,
            CancellationToken ct = default)
        {
            session.IsPlanMode = false;
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
                    // 1. 组装请求
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

                    // 5. 执行工具
                    foreach (var call in result.ToolCalls)
                    {
                        ct.ThrowIfCancellationRequested();

                        _sink.Emit(new CopilotEvent
                        {
                            Kind = EventKind.ToolDispatch,
                            Text = $"{call.Name}({call.Arguments})",
                            Data = call
                        });

                        // 权限检查
                        if (!_policy.IsAllowed(call.Name, session.IsPlanMode))
                        {
                            string msg = $"工具 {call.Name} 在当前模式下不可用";
                            session.AddToolResult(call.Id, msg);
                            _sink.Emit(CopilotEvent.Error(msg));
                            continue;
                        }

                        // 需要审批？
                        if (_gate.NeedsApproval(call.Name))
                        {
                            var approval = new PendingApproval
                            {
                                ToolName = call.Name,
                                Args = call.Arguments,
                                Description = $"{call.Name}({call.Arguments})"
                            };

                            _sink.Emit(new CopilotEvent
                            {
                                Kind = EventKind.ApprovalRequest,
                                Data = approval
                            });

                            var result2 = await approval.WaitAsync();
                            if (!result2.Allow)
                            {
                                string msg = $"用户拒绝了 {call.Name}";
                                session.AddToolResult(call.Id, msg);
                                _sink.Emit(CopilotEvent.Error(msg));
                                continue;
                            }
                        }

                        // 执行（Phase 1b 将接入真实 ToolRegistry）
                        string toolResult = ExecuteTool(call.Name, call.Arguments);
                        session.AddToolResult(call.Id, toolResult);

                        _sink.Emit(new CopilotEvent
                        {
                            Kind = EventKind.ToolResult,
                            Text = toolResult,
                            Data = call
                        });
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
                    // 让 LLM 重试
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
            string reasoning = "";
            var toolCalls = new List<ToolCall>();

            await _provider.StreamAsync(request, chunk =>
            {
                switch (chunk.Type)
                {
                    case ChunkType.Reasoning:
                        reasoning += chunk.Content;
                        _sink.Emit(CopilotEvent.Reasoning(chunk.Content));
                        break;
                    case ChunkType.Text:
                        text += chunk.Content;
                        _sink.Emit(CopilotEvent.TextEvent(chunk.Content));
                        break;
                    case ChunkType.ToolCall:
                        // 合并同名的工具调用（vLLM 可能分多个 chunk 返回）
                        MergeToolCall(toolCalls, chunk.ToolCall);
                        _sink.Emit(new CopilotEvent
                        {
                            Kind = EventKind.ToolDispatch,
                            Text = $"{chunk.ToolCall.Name}(...)",
                            Data = chunk.ToolCall
                        });
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
        /// 构建 LLM 请求
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

            // 历史消息（最近 20 条）
            request.Messages.AddRange(session.GetRecentMessages(20));

            return request;
        }

        /// <summary>
        /// 临时工具执行（Phase 1b 替换为 ToolRegistry）
        /// </summary>
        private string ExecuteTool(string name, string args)
        {
            // Phase 1b: 通过 ToolRegistry 执行
            return $"工具 {name} 已执行，参数: {args}";
        }

        private void MaybeCompact(CopilotSession session)
        {
            // Phase 3: 上下文压缩
        }
    }
}
