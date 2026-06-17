using System;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools
{
    /// <summary>
    /// 工具参数验证器（参考 cline-chinese-main 的 ToolValidator）
    /// 在工具执行前校验参数完整性
    /// </summary>
    public class ToolValidator
    {
        /// <summary>
        /// 校验必填参数是否存在
        /// </summary>
        public static ValidationResult Validate(string toolName, string args, string[] requiredParams)
        {
            if (string.IsNullOrWhiteSpace(args))
            {
                if (requiredParams != null && requiredParams.Length > 0)
                    return ValidationResult.Fail($"工具 {toolName} 缺少参数: {string.Join(", ", requiredParams)}");
                return ValidationResult.Ok();
            }

            // 基本 JSON 解析检查
            try
            {
                var json = JObject.Parse(args);
                if (requiredParams != null)
                {
                    foreach (var param in requiredParams)
                    {
                        if (json[param] == null)
                            return ValidationResult.Fail($"工具 {toolName} 缺少必填参数: {param}");
                    }
                }
            }
            catch (Exception ex)
            {
                return ValidationResult.Fail($"工具 {toolName} 参数 JSON 解析失败: {ex.Message}");
            }

            return ValidationResult.Ok();
        }
    }

    /// <summary>
    /// 校验结果
    /// </summary>
    public class ValidationResult
    {
        public bool IsValid { get; set; }
        public string Error { get; set; }

        public static ValidationResult Ok() => new ValidationResult { IsValid = true };
        public static ValidationResult Fail(string error) => new ValidationResult { IsValid = false, Error = error };
    }
}
