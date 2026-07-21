using System;
using System.Text;

namespace E3DCopilot.Tools.Bridge
{
    /// <summary>
    /// PML 脚本代码生成器
    /// 基于验证过的 PML 黄金范式
    /// 
    /// 安全：所有用户输入通过 PmlSanitizer 过滤，防止 PML 注入攻击
    /// </summary>
    public class PmlGenerator
    {
        /// <summary>
        /// 生成集合查询 PML
        /// </summary>
        /// <param name="type">元素类型（如 PIPE, EQUI）</param>
        /// <param name="pattern">名称匹配模式（支持通配符 * ?）</param>
        /// <param name="scope">搜索范围（元素名或 CE）</param>
        public string GenerateQuery(string type, string pattern, string scope = null)
        {
            // 安全过滤输入
            var safeType = PmlSanitizer.SanitizeTypeName(type);
            var safePattern = string.IsNullOrEmpty(pattern) ? null : PmlSanitizer.SanitizePattern(pattern);
            var safeScope = PmlSanitizer.SanitizeScope(scope);

            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(safePattern))
            {
                sb.AppendLine($"var !items coll all {safeType} with Matchwild(name,'{safePattern}')"
                    + (safeScope != null ? $" for $!{safeScope}" : ""));
            }
            else
            {
                sb.AppendLine($"var !items coll all {safeType}"
                    + (safeScope != null ? $" for $!{safeScope}" : ""));
            }

            sb.AppendLine("DO !item values !items");
            sb.AppendLine("    $p {!item.name} | {!item.type} | DIA={!item.:DIA} | WTHK={!item.:WTHK}");
            sb.AppendLine("enddo");
            sb.AppendLine("$p 共 {!items.size()} 个元素");

            return sb.ToString();
        }

        /// <summary>
        /// 生成批量属性修改 PML
        /// </summary>
        /// <param name="type">元素类型</param>
        /// <param name="attribute">属性名</param>
        /// <param name="value">新值</param>
        /// <param name="filter">名称过滤模式</param>
        /// <param name="scope">搜索范围</param>
        public string GenerateBatchSet(string type, string attribute,
            string value, string filter = null, string scope = null)
        {
            // 安全过滤输入
            var safeType = PmlSanitizer.SanitizeTypeName(type);
            var safeAttr = PmlSanitizer.SanitizeAttributeName(attribute);
            var safeValue = PmlSanitizer.SanitizeValue(value);
            var safeFilter = string.IsNullOrEmpty(filter) ? null : PmlSanitizer.SanitizePattern(filter);
            var safeScope = PmlSanitizer.SanitizeScope(scope);

            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(safeFilter))
            {
                sb.AppendLine($"var !items coll all {safeType} with Matchwild(name,'{safeFilter}')"
                    + (safeScope != null ? $" for $!{safeScope}" : ""));
            }
            else
            {
                sb.AppendLine($"var !items coll all {safeType}"
                    + (safeScope != null ? $" for $!{safeScope}" : ""));
            }

            sb.AppendLine("!count = 0");
            sb.AppendLine("DO !item values !items");
            sb.AppendLine("    $!item");
            sb.AppendLine($"    !item.:{safeAttr} = '{safeValue}'");
            sb.AppendLine("    !count = !count + 1");
            sb.AppendLine("enddo");
            sb.AppendLine($"$p 已修改 {{!count}} 个元素的 {safeAttr} = {safeValue}");

            return sb.ToString();
        }

        /// <summary>
        /// 生成存在性检查 PML
        /// </summary>
        /// <param name="elementName">元素名</param>
        public string GenerateCheck(string elementName)
        {
            var safeName = PmlSanitizer.SanitizeElementName(elementName);

            var sb = new StringBuilder();
            sb.AppendLine($"var !flag exist $!{safeName}");
            sb.AppendLine("if !flag eq 'TRUEA' then");
            sb.AppendLine($"    $p {safeName} 存在");
            sb.AppendLine("else");
            sb.AppendLine($"    $p {safeName} 不存在");
            sb.AppendLine("endif");
            return sb.ToString();
        }

        /// <summary>
        /// 生成元素导航 PML
        /// </summary>
        /// <param name="elementName">元素名</param>
        public string GenerateNavigate(string elementName)
        {
            var safeName = PmlSanitizer.SanitizeElementName(elementName);
            return $"$!{safeName}\n$p 当前元素: {{!!ce.name}} ({{!!ce.type}})";
        }

        /// <summary>
        /// 生成子元素查询 PML
        /// </summary>
        /// <param name="scope">搜索范围（元素名），null 表示当前元素</param>
        public string GenerateGetChildren(string scope = null)
        {
            var safeScope = PmlSanitizer.SanitizeScope(scope);

            var sb = new StringBuilder();
            if (safeScope != null)
                sb.AppendLine($"$!{safeScope}");
            sb.AppendLine("DO !child values !!ce.mem");
            sb.AppendLine("    $p {!child.name} | {!child.type}");
            sb.AppendLine("enddo");
            return sb.ToString();
        }

        /// <summary>
        /// 生成距离计算 PML
        /// </summary>
        /// <param name="element1">第一个元素名</param>
        /// <param name="element2">第二个元素名</param>
        public string GenerateDistance(string element1, string element2)
        {
            var safeElem1 = PmlSanitizer.SanitizeElementName(element1);
            var safeElem2 = PmlSanitizer.SanitizeElementName(element2);

            var sb = new StringBuilder();
            sb.AppendLine($"$!{safeElem1}");
            sb.AppendLine("!pos1 = !!ce.Position");
            sb.AppendLine($"$!{safeElem2}");
            sb.AppendLine("!pos2 = !!ce.Position");
            sb.AppendLine("!dist = !pos1.Distance(!pos2)");
            sb.AppendLine($"$p {safeElem1} 到 {safeElem2} 距离: {{!dist}} mm");
            return sb.ToString();
        }
    }
}
