using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Providers
{
    /// <summary>
    /// 消息规范化 — 在发送给 LLM 前修复消息历史
    /// 对齐 Reasonix NormalizeMessages:
    ///   - 删除孤立 tool 消息（无对应 assistant tool_calls）
    ///   - 为未应答的 assistant tool_calls 补充占位结果
    ///   - 修复截断的 JSON 参数
    /// </summary>
    public static class MessageNormalizer
    {
        private const string InterruptedToolResult =
            "[no result: the previous turn was interrupted before this tool call completed]";

        /// <summary>
        /// 修复消息列表，使其满足 OpenAI tool-call 契约。
        /// 不修改原始列表，返回新列表。
        /// </summary>
        public static List<ChatMessage> Normalize(List<ChatMessage> msgs)
        {
            if (msgs == null || msgs.Count == 0) return msgs;
            if (IsWellFormed(msgs)) return msgs;

            var output = new List<ChatMessage>(msgs.Count);
            int i = 0;
            while (i < msgs.Count)
            {
                var m = msgs[i];
                if (m.Role == MessageRole.Assistant && m.ToolCalls != null && m.ToolCalls.Count > 0)
                {
                    // Collect following tool messages
                    int j = i + 1;
                    while (j < msgs.Count && msgs[j].Role == MessageRole.Tool)
                        j++;

                    var repairedCalls = RepairToolCallArgs(m.ToolCalls);
                    output.Add(new ChatMessage(MessageRole.Assistant, m.Content)
                    {
                        ToolCalls = repairedCalls,
                        Images = m.Images
                    });

                    var toolResults = new List<ChatMessage>();
                    for (int k = i + 1; k < j; k++)
                        toolResults.Add(msgs[k]);

                    output.AddRange(PairToolResults(repairedCalls, toolResults));
                    i = j;
                    continue;
                }

                if (m.Role == MessageRole.Tool)
                {
                    // Orphan tool message — drop
                    i++;
                    continue;
                }

                output.Add(m);
                i++;
            }
            return output;
        }

        private static bool IsWellFormed(List<ChatMessage> msgs)
        {
            for (int i = 0; i < msgs.Count; )
            {
                var m = msgs[i];
                if (m.Role == MessageRole.Assistant && m.ToolCalls != null && m.ToolCalls.Count > 0)
                {
                    int j = i + 1;
                    while (j < msgs.Count && msgs[j].Role == MessageRole.Tool)
                        j++;

                    int resultCount = j - i - 1;
                    if (m.ToolCalls.Count != resultCount) return false;
                    foreach (var tc in m.ToolCalls)
                        if (string.IsNullOrEmpty(tc.Name)) return false;
                    if (NeedsArgRepair(m.ToolCalls)) return false;
                    for (int k = 0; k < m.ToolCalls.Count; k++)
                    {
                        int idx = i + 1 + k;
                        if (idx >= msgs.Count || msgs[idx].ToolCallId != m.ToolCalls[k].Id)
                            return false;
                    }
                    i = j;
                    continue;
                }
                if (m.Role == MessageRole.Tool) return false;
                i++;
            }
            return true;
        }

        private static bool NeedsArgRepair(List<ToolCall> calls)
        {
            foreach (var tc in calls)
            {
                if (!string.IsNullOrEmpty(tc.Arguments) && !IsValidJson(tc.Arguments))
                    return true;
            }
            return false;
        }

        private static bool IsValidJson(string s)
        {
            try { JObject.Parse(s); return true; }
            catch { return false; }
        }

        private static List<ToolCall> RepairToolCallArgs(List<ToolCall> calls)
        {
            if (!NeedsArgRepair(calls)) return calls;

            var repaired = new List<ToolCall>(calls.Count);
            foreach (var tc in calls)
            {
                if (string.IsNullOrEmpty(tc.Arguments) || IsValidJson(tc.Arguments))
                    repaired.Add(tc);
                else
                    repaired.Add(new ToolCall
                    {
                        Id = tc.Id,
                        Name = tc.Name,
                        Arguments = CloseTruncatedJson(tc.Arguments)
                    });
            }
            return repaired;
        }

        /// <summary>
        /// Best-effort completes a JSON document cut off mid-stream.
        /// </summary>
        private static string CloseTruncatedJson(string s)
        {
            var stack = new Stack<char>();
            bool inStr = false, esc = false;
            foreach (char c in s)
            {
                if (inStr)
                {
                    if (esc) esc = false;
                    else if (c == '\\') esc = true;
                    else if (c == '"') inStr = false;
                    continue;
                }
                switch (c)
                {
                    case '"': inStr = true; break;
                    case '{': stack.Push('}'); break;
                    case '[': stack.Push(']'); break;
                    case '}':
                    case ']':
                        if (stack.Count > 0) stack.Pop();
                        break;
                }
            }

            string result = s;
            if (esc && result.Length > 0)
                result = result.Substring(0, result.Length - 1);
            if (inStr)
                result += "\"";

            result = result.TrimEnd(' ', '\t', '\r', '\n');
            if (result.EndsWith(","))
                result = result.Substring(0, result.Length - 1);
            else if (result.EndsWith(":"))
                result += "null";

            while (stack.Count > 0)
                result += stack.Pop();

            if (!IsValidJson(result))
                return "{}";
            return result;
        }

        private static List<ChatMessage> PairToolResults(List<ToolCall> calls, List<ChatMessage> available)
        {
            var output = new List<ChatMessage>(calls.Count);
            bool distinct = IdsDistinct(calls);

            if (distinct)
            {
                var byId = new Dictionary<string, ChatMessage>();
                foreach (var r in available)
                    if (!string.IsNullOrEmpty(r.ToolCallId))
                        byId[r.ToolCallId] = r;

                foreach (var tc in calls)
                {
                    ChatMessage r;
                    if (byId.TryGetValue(tc.Id, out r))
                        output.Add(r);
                    else
                        output.Add(new ChatMessage
                        {
                            Role = MessageRole.Tool,
                            Content = InterruptedToolResult,
                            ToolCallId = tc.Id
                        });
                }
            }
            else
            {
                for (int k = 0; k < calls.Count; k++)
                {
                    if (k < available.Count)
                    {
                        var r = available[k];
                        r.ToolCallId = calls[k].Id;
                        output.Add(r);
                    }
                    else
                    {
                        output.Add(new ChatMessage
                        {
                            Role = MessageRole.Tool,
                            Content = InterruptedToolResult,
                            ToolCallId = calls[k].Id
                        });
                    }
                }
            }
            return output;
        }

        private static bool IdsDistinct(List<ToolCall> calls)
        {
            var seen = new HashSet<string>();
            foreach (var tc in calls)
            {
                if (string.IsNullOrEmpty(tc.Id)) return false;
                if (!seen.Add(tc.Id)) return false;
            }
            return true;
        }
    }
}
