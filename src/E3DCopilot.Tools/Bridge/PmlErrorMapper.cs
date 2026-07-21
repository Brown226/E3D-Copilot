using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace E3DCopilot.Tools.Bridge
{
    /// <summary>
    /// PML 错误码映射表 — 将晦涩的 PML 错误转换为人类可读的描述
    /// 
    /// 问题：PML 错误信息通常是 "Error 123 at line 5" 这种天书，
    /// LLM 拿到这种错误修复成功率很低。
    /// 
    /// 解决：建立错误码到人类可读描述的映射，帮助 LLM 理解错误原因并修复。
    /// </summary>
    public static class PmlErrorMapper
    {
        /// <summary>
        /// PML 错误码 → 人类可读描述
        /// 来源：E3D PML 官方文档 + 实际调试经验
        /// </summary>
        private static readonly Dictionary<int, PmlErrorInfo> ErrorMap = new Dictionary<int, PmlErrorInfo>
        {
            // ── 语法错误 (1-99) ──
            { 1, new PmlErrorInfo("语法错误", "PML 脚本存在语法错误，请检查拼写和格式", "检查关键字拼写、引号配对、语句结构") }
            , { 2, new PmlErrorInfo("未知命令", "使用了不存在的 PML 命令", "检查命令名称是否正确，参考 PML 速查表") }
            , { 3, new PmlErrorInfo("参数错误", "命令参数类型或数量不正确", "检查参数格式，确保类型匹配") }
            , { 4, new PmlErrorInfo("引号不匹配", "字符串引号未正确闭合", "检查单引号是否成对，注意 PML 使用单引号") }
            , { 5, new PmlErrorInfo("括号不匹配", "括号未正确闭合", "检查圆括号、方括号是否成对") }
            , { 6, new PmlErrorInfo("变量未定义", "使用了未声明的变量", "PML 变量需要先 var 声明或直接赋值") }
            , { 7, new PmlErrorInfo("类型不匹配", "操作数类型不兼容", "检查变量类型，必要时进行类型转换") }
            , { 8, new PmlErrorInfo("除零错误", "除数为零", "添加除数为零的检查") }
            , { 9, new PmlErrorInfo("数组越界", "数组索引超出范围", "检查数组大小和索引值") }
            , { 10, new PmlErrorInfo("无限循环", "循环次数超出限制", "检查循环条件，确保有正确的退出条件") }

            // ── 元素/数据库错误 (100-199) ──
            , { 100, new PmlErrorInfo("元素不存在", "指定的元素在数据库中不存在", "检查元素名称拼写，确认元素是否已创建") }
            , { 101, new PmlErrorInfo("元素类型错误", "元素类型与预期不符", "使用 TYPE 属性确认元素类型") }
            , { 102, new PmlErrorInfo("属性不存在", "指定的属性在该元素类型上不存在", "检查属性名是否正确，不同元素类型有不同属性") }
            , { 103, new PmlErrorInfo("属性只读", "尝试修改只读属性", "该属性由系统自动维护，不能直接修改") }
            , { 104, new PmlErrorInfo("属性值无效", "属性值超出允许范围或格式错误", "检查值的格式和范围限制") }
            , { 105, new PmlErrorInfo("元素已锁定", "元素被其他用户或进程锁定", "等待锁定释放或联系管理员") }
            , { 106, new PmlErrorInfo("权限不足", "没有执行此操作的权限", "检查用户权限设置") }
            , { 107, new PmlErrorInfo("数据库错误", "数据库访问失败", "可能是数据库连接问题，重试或联系管理员") }
            , { 108, new PmlErrorInfo("名称冲突", "元素名称已存在", "使用不同的名称或先删除/重命名现有元素") }
            , { 109, new PmlErrorInfo("父元素不存在", "父级元素不存在，无法创建子元素", "先创建父级元素") }
            , { 110, new PmlErrorInfo("元素有子元素", "元素包含子元素，不能直接删除", "先删除或移动所有子元素") }

            // ── 导航/范围错误 (200-299) ──
            , { 200, new PmlErrorInfo("导航失败", "无法导航到指定元素", "检查元素路径是否正确") }
            , { 201, new PmlErrorInfo("CE 未设置", "当前元素 (CE) 未设置", "先使用 $!elementName 设置当前元素") }
            , { 202, new PmlErrorInfo("范围无效", "搜索范围 (scope) 无效", "检查 scope 参数是否为有效元素名") }
            , { 203, new PmlErrorInfo("集合为空", "查询结果为空集合", "调整查询条件，确认目标元素存在") }

            // ── 文件/IO 错误 (300-399) ──
            , { 300, new PmlErrorInfo("文件不存在", "指定的文件不存在", "检查文件路径是否正确") }
            , { 301, new PmlErrorInfo("文件访问被拒绝", "没有文件读写权限", "检查文件权限和是否被其他程序占用") }
            , { 302, new PmlErrorInfo("路径无效", "文件路径格式不正确", "使用绝对路径，检查路径分隔符") }
            , { 303, new PmlErrorInfo("磁盘空间不足", "磁盘空间不足", "清理磁盘空间后重试") }

            // ── 几何/计算错误 (400-499) ──
            , { 400, new PmlErrorInfo("几何计算失败", "几何运算出错", "检查输入坐标/方向是否有效") }
            , { 401, new PmlErrorInfo("无效坐标", "坐标值超出有效范围", "检查坐标值是否合理") }
            , { 402, new PmlErrorInfo("方向无效", "方向向量无效", "方向向量不能为零向量") }
            , { 403, new PmlErrorInfo("距离计算失败", "无法计算两点距离", "确认两个元素都有有效的位置信息") }

            // ── 系统/运行时错误 (500+) ──
            , { 500, new PmlErrorInfo("内部错误", "PML 引擎内部错误", "简化脚本重试，如持续出现请联系支持") }
            , { 501, new PmlErrorInfo("内存不足", "PML 执行内存不足", "减少处理的数据量") }
            , { 502, new PmlErrorInfo("超时", "PML 执行超时", "优化脚本性能或增加超时时间") }
            , { 503, new PmlErrorInfo("中断", "PML 执行被用户中断", "重新执行脚本") }
        };

        /// <summary>
        /// 常见错误消息模式 → 错误码
        /// 用于从错误文本中提取错误码
        /// </summary>
        private static readonly List<(Regex pattern, int code)> MessagePatterns = new List<(Regex, int)>
        {
            (new Regex(@"not\s+exist|does\s+not\s+exist|no\s+such", RegexOptions.IgnoreCase), 100)
            , (new Regex(@"unknown\s+(command|keyword)", RegexOptions.IgnoreCase), 2)
            , (new Regex(@"syntax\s+error", RegexOptions.IgnoreCase), 1)
            , (new Regex(@"type\s+mismatch|incompatible\s+type", RegexOptions.IgnoreCase), 7)
            , (new Regex(@"undefined\s+variable|not\s+declared", RegexOptions.IgnoreCase), 6)
            , (new Regex(@"permission\s+denied|access\s+denied", RegexOptions.IgnoreCase), 106)
            , (new Regex(@"locked|in\s+use", RegexOptions.IgnoreCase), 105)
            , (new Regex(@"read[\s-]?only", RegexOptions.IgnoreCase), 103)
            , (new Regex(@"invalid\s+(value|attribute)", RegexOptions.IgnoreCase), 104)
            , (new Regex(@"file\s+not\s+found", RegexOptions.IgnoreCase), 300)
            , (new Regex(@"division\s+by\s+zero", RegexOptions.IgnoreCase), 8)
            , (new Regex(@"timeout|timed?\s*out", RegexOptions.IgnoreCase), 502)
            , (new Regex(@"out\s+of\s+memory", RegexOptions.IgnoreCase), 501)
        };

        /// <summary>
        /// 将 PML 错误消息转换为人类可读的描述
        /// </summary>
        /// <param name="errorMessage">原始错误消息</param>
        /// <param name="errorCode">错误码（如果已知）</param>
        /// <returns>增强后的错误信息</returns>
        public static string MapError(string errorMessage, int? errorCode = null)
        {
            if (string.IsNullOrWhiteSpace(errorMessage))
                return "未知错误：PML 执行失败，无错误信息";

            // 1. 如果提供了错误码，直接查表
            if (errorCode.HasValue && ErrorMap.TryGetValue(errorCode.Value, out var info))
            {
                return FormatError(info, errorMessage);
            }

            // 2. 尝试从错误消息中提取错误码
            var codeMatch = Regex.Match(errorMessage, @"[Ee]rror\s*[:#]?\s*(\d+)");
            if (codeMatch.Success && int.TryParse(codeMatch.Groups[1].Value, out int extractedCode))
            {
                if (ErrorMap.TryGetValue(extractedCode, out var info2))
                {
                    return FormatError(info2, errorMessage);
                }
            }

            // 3. 通过消息模式匹配
            foreach (var (pattern, code) in MessagePatterns)
            {
                if (pattern.IsMatch(errorMessage))
                {
                    if (ErrorMap.TryGetValue(code, out var info3))
                    {
                        return FormatError(info3, errorMessage);
                    }
                }
            }

            // 4. 无法识别，返回原始消息 + 通用建议
            return $"PML 执行错误：{errorMessage}\n\n建议：检查 PML 语法是否正确；" +
                   "可调用 grep(knowledge=true, pattern=\"<相关主题>\") 取回黄金范式后重写并重试。";
        }

        /// <summary>
        /// 格式化错误信息
        /// </summary>
        private static string FormatError(PmlErrorInfo info, string originalMessage)
        {
            return $"【{info.Title}】{info.Description}\n" +
                   $"原始错误：{originalMessage}\n" +
                   $"修复建议：{info.Suggestion}\n" +
                   NextStepHint(info.Title);
        }

        /// <summary>
        /// 闭环提示：引导 LLM 先检索黄金范式再重写重试，而不是凭记忆反复试错。
        /// </summary>
        private static string NextStepHint(string title)
        {
            // 语法/命令/引号/类型/变量类错误 → 明确建议检索知识库黄金范式
            bool syntaxLike = title.Contains("语法") || title.Contains("命令")
                              || title.Contains("引号") || title.Contains("括号")
                              || title.Contains("类型") || title.Contains("变量")
                              || title.Contains("参数");
            if (syntaxLike)
                return "下一步：调用 grep(knowledge=true, pattern=\"<相关主题，如 collection/attribute/handle>\") " +
                       "取回已验证的 PML 黄金范式，据此重写脚本后再执行；不要凭记忆反复试错。";
            return "下一步：核对元素名/属性/范围是否真实存在（可先用 query 或 grep(knowledge=true) 确认），修正后重试。";
        }

        /// <summary>
        /// 获取所有已知错误码列表（用于文档/调试）
        /// </summary>
        public static IReadOnlyDictionary<int, PmlErrorInfo> GetAllErrors() => ErrorMap;
    }

    /// <summary>
    /// PML 错误信息结构
    /// </summary>
    public class PmlErrorInfo
    {
        public string Title { get; }
        public string Description { get; }
        public string Suggestion { get; }

        public PmlErrorInfo(string title, string description, string suggestion)
        {
            Title = title;
            Description = description;
            Suggestion = suggestion;
        }
    }
}
