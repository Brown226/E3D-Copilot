using System.Text;

namespace E3DCopilot.Tools.Bridge
{
    /// <summary>
    /// PML 脚本代码生成器
    /// 基于验证过的 PML 黄金范式
    /// </summary>
    public class PmlGenerator
    {
        /// <summary>
        /// 生成集合查询 PML
        /// </summary>
        public string GenerateQuery(string type, string pattern, string scope = null)
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(pattern))
            {
                sb.AppendLine($"var !items coll all {type} with Matchwild(name,'{pattern}')"
                    + (scope != null ? $" for $!{scope}" : ""));
            }
            else
            {
                sb.AppendLine($"var !items coll all {type}"
                    + (scope != null ? $" for $!{scope}" : ""));
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
        public string GenerateBatchSet(string type, string attribute,
            string value, string filter = null, string scope = null)
        {
            var sb = new StringBuilder();

            if (!string.IsNullOrEmpty(filter))
            {
                sb.AppendLine($"var !items coll all {type} with Matchwild(name,'{filter}')"
                    + (scope != null ? $" for $!{scope}" : ""));
            }
            else
            {
                sb.AppendLine($"var !items coll all {type}"
                    + (scope != null ? $" for $!{scope}" : ""));
            }

            sb.AppendLine("!count = 0");
            sb.AppendLine("DO !item values !items");
            sb.AppendLine("    $!item");
            sb.AppendLine($"    !item.:{attribute} = '{value}'");
            sb.AppendLine("    !count = !count + 1");
            sb.AppendLine("enddo");
            sb.AppendLine($"$p 已修改 {{!count}} 个元素的 {attribute} = {value}");

            return sb.ToString();
        }

        /// <summary>
        /// 生成存在性检查 PML
        /// </summary>
        public string GenerateCheck(string elementName)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"var !flag exist $!{elementName}");
            sb.AppendLine("if !flag eq 'TRUEA' then");
            sb.AppendLine($"    $p {elementName} 存在");
            sb.AppendLine("else");
            sb.AppendLine($"    $p {elementName} 不存在");
            sb.AppendLine("endif");
            return sb.ToString();
        }

        /// <summary>
        /// 生成元素导航 PML
        /// </summary>
        public string GenerateNavigate(string elementName)
        {
            return $"$!{elementName}\n$p 当前元素: {{!!ce.name}} ({{!!ce.type}})";
        }

        /// <summary>
        /// 生成子元素查询 PML
        /// </summary>
        public string GenerateGetChildren(string scope = null)
        {
            var sb = new StringBuilder();
            if (scope != null)
                sb.AppendLine($"$!{scope}");
            sb.AppendLine("DO !child values !!ce.mem");
            sb.AppendLine("    $p {!child.name} | {!child.type}");
            sb.AppendLine("enddo");
            return sb.ToString();
        }

        /// <summary>
        /// 生成距离计算 PML
        /// </summary>
        public string GenerateDistance(string element1, string element2)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"$!{element1}");
            sb.AppendLine("!pos1 = !!ce.Position");
            sb.AppendLine($"$!{element2}");
            sb.AppendLine("!pos2 = !!ce.Position");
            sb.AppendLine("!dist = !pos1.Distance(!pos2)");
            sb.AppendLine($"$p {element1} 到 {element2} 距离: {{!dist}} mm");
            return sb.ToString();
        }
    }
}
