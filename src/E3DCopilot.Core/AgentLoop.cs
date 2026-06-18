using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
    /// Core Agent loop: LLM call → ToolExecutor dispatch → result injection → loop
    /// net48 compatible: callback mode streaming, no IAsyncEnumerable
    /// Corresponds to cline-chinese-main's Task engine + ToolExecutor
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
        /// Run Agent loop
        /// </summary>
        public async Task RunAsync(CopilotSession session, string input,
            CancellationToken ct = default)
        {
            session.AddUserMessage(input);

            _sink.Emit(new CopilotEvent
            {
                Kind = EventKind.TurnStarted,
                Text = $"Processing: {input}"
            });

            for (int step = 0; step < MaxSteps; step++)
            {
                ct.ThrowIfCancellationRequested();

                try
                {
                    // 1. Build request (including available tool definitions)
                    var request = BuildRequest(session);

                    // 2. Call LLM (streaming)
                    var result = await StreamLlmAsync(request, ct);

                    // 3. Save assistant message
                    session.AddAssistantMessage(result.Text, result.ToolCalls);

                    // 4. No tool calls → done
                    if (result.ToolCalls == null || result.ToolCalls.Count == 0)
                    {
                        _sink.Emit(CopilotEvent.TurnDone());
                        return;
                    }

                    // 5. Execute tools via ToolExecutor
                    foreach (var call in result.ToolCalls)
                    {
                        ct.ThrowIfCancellationRequested();
                        
                        // Skip empty tool calls (LLM sometimes returns tool calls with no name/args)
                        if (string.IsNullOrWhiteSpace(call.Name))
                        {
                            _sink.Emit(CopilotEvent.Notice($"Skipped tool call with empty name (args: {call.Arguments})"));
                            continue;
                        }

                        // 5a. Permission check (CommandPermissionController)
                        var access = _permission.CheckTool(call.Name, call.Arguments);
                        if (access == CommandPermissionController.AccessMode.Block)
                        {
                            string msg = $"Tool {call.Name} blocked by policy";
                            session.AddToolResult(call.Id, msg);
                            _sink.Emit(CopilotEvent.Error(msg));
                            continue;
                        }

                        // 5b. Batch operation detection (>5 elements need extra confirmation)
                        bool isBatch = _permission.IsBatchOperation(call.Arguments);

                        // 5c. Needs approval?
                        if (access == CommandPermissionController.AccessMode.Ask || isBatch)
                        {
                            var approval = new PendingApproval
                            {
                                ToolName = call.Name,
                                Args = call.Arguments,
                                Description = $"{call.Name}({call.Arguments})"
                                    + (isBatch ? " [batch]" : "")
                            };

                            // Register approval via Controller (if controller is available)
                            if (_controller != null)
                            {
                                _controller.RegisterApproval(approval);
                            }
                            else
                            {
                                // Fallback: emit event directly (backward compat)
                                _sink.Emit(new CopilotEvent
                                {
                                    Kind = EventKind.ApprovalRequest,
                                    Data = approval
                                });
                            }

                            var approvalResult = await approval.WaitAsync();
                            if (!approvalResult.Allow)
                            {
                                string msg = $"User rejected {call.Name}";
                                session.AddToolResult(call.Id, msg);
                                _sink.Emit(CopilotEvent.Error(msg));
                                continue;
                            }
                        }

                        // 5d. Execute via ToolExecutor
                        var toolResult = await _executor.ExecuteAsync(
                            call.Name, call.Arguments, ct);
                        session.AddToolResult(call.Id,
                            toolResult.Success ? toolResult.Text : toolResult.Error);
                    }

                    // 6. Context compression (optional)
                    MaybeCompact(session);
                }
                catch (OperationCanceledException)
                {
                    _sink.Emit(CopilotEvent.Notice("Cancelled"));
                    return;
                }
                catch (Exception ex)
                {
                    // Minimal exception handling, avoid cascading failures from extra operations
                    try { CopilotLogger.Error(ex, "AgentLoop step {0} failed", step); } catch { }
                    
                    string msg = "Error encountered";
                    try { msg = $"Error: {ex.GetType().Name}"; } catch { }
                    
                    _sink?.Emit(CopilotEvent.Error(msg));
                }
            }

            _sink.Emit(CopilotEvent.Notice($"Reached max steps {MaxSteps}"));
            _sink.Emit(CopilotEvent.TurnDone());
        }

        /// <summary>
        /// Stream LLM call (net48 callback mode)
        /// Supports two tool call methods:
        ///   1. Standard OpenAI tool_calls (streaming fragments)
        ///   2. XML fallback: extract &lt;tool_invocation name="..." arguments={...} /&gt; from text
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

            // ── Fallback: if standard tool_calls not detected, try extracting XML format from text ──
            if (toolCalls.Count == 0 && !string.IsNullOrEmpty(text)
                && Providers.ToolInvocationParser.ContainsToolInvocation(text))
            {
                var xmlCalls = Providers.ToolInvocationParser.ExtractToolCalls(text);
                if (xmlCalls.Count > 0)
                {
                    toolCalls.AddRange(xmlCalls);
                    // Strip XML tags from text, keep plain text response
                    text = Providers.ToolInvocationParser.StripToolInvocationTags(text);

                    _sink.Emit(CopilotEvent.Notice(
                        $"Parsed {xmlCalls.Count} XML format tool calls from text (fallback mode)"));
                }
            }

            return (text, toolCalls);
        }

        /// <summary>
        /// Merge incremental tool calls (vLLM streaming tool_calls may be fragmented)
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
        /// Parse XML format tool calls from text (fallback)
        /// When LLM does not use standard function calling, try to extract from text
        /// </summary>
        private List<ToolCall> ParseTextToolCalls(string text)
        {
            var results = new List<ToolCall>();
            if (string.IsNullOrEmpty(text)) return results;

            int idx = 0;
            while (true)
            {
                // Search for <tool_call> or <function=xxx> tags
                int start = text.IndexOf("<tool_call", idx, StringComparison.OrdinalIgnoreCase);
                int funcStart = text.IndexOf("<function=", idx, StringComparison.OrdinalIgnoreCase);
                
                // Take the first occurrence
                if (funcStart >= 0 && (start < 0 || funcStart < start))
                    start = funcStart;
                
                if (start < 0) break;
                
                int end = text.IndexOf("</tool_call>", start, StringComparison.OrdinalIgnoreCase);
                int funcEnd = text.IndexOf("</function>", start, StringComparison.OrdinalIgnoreCase);
                if (end < 0 && funcEnd < 0) break;
                
                int closeTag = (funcEnd >= 0 && (end < 0 || funcEnd < end)) ? funcEnd + "</function>".Length : end + "</tool_call>".Length;
                
                string block = text.Substring(start, closeTag - start);
                idx = closeTag;
                
                // Parse function name
                string funcName = null;
                var nameMatch = System.Text.RegularExpressions.Regex.Match(block, @"<function=(.+?)>", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (nameMatch.Success)
                    funcName = nameMatch.Groups[1].Value.Trim();
                
                if (string.IsNullOrEmpty(funcName)) continue;
                
                // Parse arguments
                var sb = new StringBuilder("{");
                bool first = true;
                foreach (System.Text.RegularExpressions.Match paramMatch in System.Text.RegularExpressions.Regex.Matches(block, @"<parameter=([^>]+)>(.*?)</parameter>", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                {
                    if (!first) sb.Append(", ");
                    string paramName = paramMatch.Groups[1].Value.Trim();
                    string paramValue = paramMatch.Groups[2].Value.Trim();
                    sb.AppendFormat("\"{0}\": \"{1}\"", 
                        System.Security.SecurityElement.Escape(paramName),
                        System.Security.SecurityElement.Escape(paramValue));
                    first = false;
                }
                sb.Append("}");
                
                string args = sb.ToString();
                if (args == "{}") args = "";
                
                results.Add(new ToolCall
                {
                    Id = Guid.NewGuid().ToString("N"),
                    Name = funcName,
                    Arguments = args
                });
            }
            
            return results;
        }

        /// <summary>
        /// Build LLM request (with tool definitions)
        /// </summary>
        private CopilotRequest BuildRequest(CopilotSession session)
        {
            // Resolve current Provider and model
            var (providerConfig, modelName) = _config.ResolveModel(_config.DefaultModel);
            
            var request = new CopilotRequest
            {
                Model = modelName,
                Temperature = providerConfig.Temperature,
                MaxTokens = providerConfig.MaxTokens,
                Messages = new List<ChatMessage>()
            };

            // System Prompt
            request.Messages.Add(new ChatMessage(MessageRole.System,
                SystemPrompt.Build()));

            // Tool definitions (from ToolExecutor)
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

            // History messages (last 20)
            request.Messages.AddRange(session.GetRecentMessages(20));

            return request;
        }

        private void MaybeCompact(CopilotSession session)
        {
            // Phase 3: context compression
        }
    }
}
