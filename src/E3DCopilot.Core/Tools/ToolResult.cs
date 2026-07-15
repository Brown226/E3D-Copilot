namespace E3DCopilot.Core.Tools
{
    /// <summary>
    /// 工具执行结果（统一返回类型）
    /// </summary>
    public class ToolResult
    {
        /// <summary>最大输出长度（对齐 Reasonix 32KB head+tail 截断）</summary>
        public const int MaxOutputBytes = 32768;
        /// <summary>截断时保留的首尾字节数</summary>
        private const int HeadTailBytes = 14000;

        /// <summary>执行是否成功</summary>
        public bool Success { get; set; }

        /// <summary>结果文本（呈现给 LLM，已截断）</summary>
        public string Text { get; set; }

        /// <summary>错误信息（失败时）</summary>
        public string Error { get; set; }

        /// <summary>执行耗时（毫秒）</summary>
        public long DurationMs { get; set; }

        /// <summary>附加数据（前端展示用）</summary>
        public object Data { get; set; }

        /// <summary>失败是否可重试（网络错误等可重试，业务错误不可重试）</summary>
        public bool IsRetryable { get; set; }

        public static ToolResult Ok(string text, object data = null) =>
            new ToolResult { Success = true, Text = TruncateOutput(text), Data = data };

        public static ToolResult Fail(string error) =>
            new ToolResult { Success = false, Error = error, Text = error };

        public static ToolResult Fail(string error, bool isRetryable) =>
            new ToolResult { Success = false, Error = error, Text = error, IsRetryable = isRetryable };

        public static ToolResult RetryableFail(string error) =>
            new ToolResult { Success = false, Error = error, Text = error, IsRetryable = true };

        /// <summary>
        /// 截断工具输出：超过 MaxOutputBytes 时保留头尾，中间用截断标记。
        /// 行感知边界：在换行符处截断，避免切碎文本行。
        /// </summary>
        public static string TruncateOutput(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= MaxOutputBytes)
                return text;

            int head = HeadTailBytes;
            // 回退到最近的换行符
            while (head > 0 && text[head] != '\n') head--;
            if (head <= 0) head = HeadTailBytes; // 找不到换行则用硬截断

            int tailStart = text.Length - HeadTailBytes;
            int tail = tailStart;
            while (tail < text.Length && text[tail] != '\n') tail++;
            if (tail >= text.Length) tail = tailStart;

            string marker = $"\n\n... [truncated {text.Length - head - (text.Length - tail)} bytes] ...\n\n";
            return text.Substring(0, head) + marker + text.Substring(tail);
        }
    }
}
