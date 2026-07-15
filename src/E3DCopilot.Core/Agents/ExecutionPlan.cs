using System.Collections.Generic;
using Newtonsoft.Json;

namespace E3DCopilot.Core.Agents
{
    /// <summary>
    /// 执行计划 — Planner 子代理产出的结构化计划
    /// </summary>
    public class ExecutionPlan
    {
        /// <summary>计划标题</summary>
        [JsonProperty("title")]
        public string Title { get; set; }

        /// <summary>计划步骤列表（按执行顺序）</summary>
        [JsonProperty("steps")]
        public List<PlanStep> Steps { get; set; } = new List<PlanStep>();

        /// <summary>从 JSON 反序列化</summary>
        public static ExecutionPlan FromJson(string json)
        {
            return JsonConvert.DeserializeObject<ExecutionPlan>(json);
        }

        /// <summary>序列化为 JSON</summary>
        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.Indented);
        }

        /// <summary>验证计划有效性</summary>
        public string Validate()
        {
            if (string.IsNullOrWhiteSpace(Title))
                return "计划缺少标题";
            if (Steps == null || Steps.Count == 0)
                return "计划没有步骤";
            for (int i = 0; i < Steps.Count; i++)
            {
                var s = Steps[i];
                if (string.IsNullOrWhiteSpace(s.Name))
                    return $"步骤 {i + 1} 缺少名称";
                if (string.IsNullOrWhiteSpace(s.Task))
                    return $"步骤 {i + 1} ({s.Name}) 缺少任务描述";
                // 验证依赖引用的步骤存在
                if (s.DependsOn != null)
                {
                    foreach (var dep in s.DependsOn)
                    {
                        if (!Steps.Exists(x => x.Id == dep))
                            return $"步骤 {i + 1} ({s.Name}) 依赖不存在的步骤: {dep}";
                    }
                }
            }
            return null; // 有效
        }
    }

    /// <summary>
    /// 计划步骤
    /// </summary>
    public class PlanStep
    {
        /// <summary>步骤唯一 ID（如 "step-1"）</summary>
        [JsonProperty("id")]
        public string Id { get; set; }

        /// <summary>步骤名称（简短描述）</summary>
        [JsonProperty("name")]
        public string Name { get; set; }

        /// <summary>任务描述（传给 Executor 子代理）</summary>
        [JsonProperty("task")]
        public string Task { get; set; }

        /// <summary>依赖的步骤 ID 列表（这些步骤完成后才能执行本步骤）</summary>
        [JsonProperty("depends_on")]
        public List<string> DependsOn { get; set; } = new List<string>();

        /// <summary>并行组编号（同一组的步骤并行执行，0 = 串行）</summary>
        [JsonProperty("parallel_group")]
        public int ParallelGroup { get; set; }

        /// <summary>是否只读步骤（默认 true）</summary>
        [JsonProperty("readonly")]
        public bool ReadOnly { get; set; } = true;

        /// <summary>指定使用的 Provider（可选，handoff 专长切换）</summary>
        [JsonProperty("provider")]
        public string Provider { get; set; }

        /// <summary>执行结果（执行后填充）</summary>
        [JsonIgnore]
        public AgentResult Result { get; set; }
    }
}
