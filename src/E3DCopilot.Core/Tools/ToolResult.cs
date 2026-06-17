namespace E3DCopilot.Core.Tools
{
    /// <summary>
    /// 工具执行结果（统一返回类型）
    /// </summary>
    public class ToolResult
    {
        /// <summary>执行是否成功</summary>
        public bool Success { get; set; }

        /// <summary>结果文本（呈现给 LLM）</summary>
        public string Text { get; set; }

        /// <summary>错误信息（失败时）</summary>
        public string Error { get; set; }

        /// <summary>执行耗时（毫秒）</summary>
        public long DurationMs { get; set; }

        /// <summary>附加数据（前端展示用）</summary>
        public object Data { get; set; }

        public static ToolResult Ok(string text, object data = null) =>
            new ToolResult { Success = true, Text = text, Data = data };

        public static ToolResult Fail(string error) =>
            new ToolResult { Success = false, Error = error, Text = error };
    }
}
