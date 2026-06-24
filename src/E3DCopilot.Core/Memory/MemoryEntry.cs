using Newtonsoft.Json;

namespace E3DCopilot.Core.Memory
{
    /// <summary>
    /// 记忆条目 — 对应前端 MemoryEntry 接口
    /// </summary>
    public class MemoryEntry
    {
        [JsonProperty("id")]
        public string Id { get; set; } = "";

        [JsonProperty("title")]
        public string Title { get; set; } = "";

        [JsonProperty("content")]
        public string Content { get; set; } = "";

        [JsonProperty("kind")]
        public string Kind { get; set; } = "project_context";

        [JsonProperty("tags")]
        public string[] Tags { get; set; } = new string[0];

        [JsonProperty("score")]
        public double Score { get; set; }

        [JsonProperty("created_at")]
        public string CreatedAt { get; set; } = "";

        [JsonProperty("updated_at")]
        public string UpdatedAt { get; set; } = "";
    }
}
