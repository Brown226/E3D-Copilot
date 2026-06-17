using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace E3DCopilot.Tools.Bridge
{
    /// <summary>
    /// PML 执行引擎
    /// 封装 Command.CreateCommand(str).RunInPdms() 真实 API
    /// （命名空间：Aveva.Core.Utilities.CommandLine）
    /// </summary>
    public class PmlEngine
    {
        /// <summary>
        /// 执行 PML 脚本（自动包裹 handle 块）
        /// </summary>
        public string Run(string pmlScript)
        {
            string wrapped = WrapWithHandle(pmlScript);

            // 真实环境（%E3DPath% 已设置）：
            // Aveva.Core.Utilities.CommandLine.Command cmd =
            //     Aveva.Core.Utilities.CommandLine.Command.CreateCommand(wrapped);
            // bool ok = cmd.RunInPdms();
            // return ok ? cmd.Result : ("Error: " + cmd.Error.MessageText);

            // 开发环境（无 E3D DLL）模拟返回
            return SimulateRun(pmlScript);
        }

        /// <summary>
        /// 执行 PML 并返回结果（失败抛异常）
        /// </summary>
        public string RunWithResult(string pmlScript)
        {
            string result = Run(pmlScript);
            if (result != null && result.StartsWith("Error:"))
                throw new InvalidOperationException(result);
            return result;
        }

        /// <summary>
        /// 解析 PML 输出为行列表
        /// </summary>
        public List<string> ParseOutput(string output)
        {
            var lines = new List<string>();
            if (string.IsNullOrEmpty(output)) return lines;

            foreach (string line in output.Split(new[] { '\r', '\n' },
                StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = line.Trim();
                if (trimmed.Length > 0)
                    lines.Add(trimmed);
            }
            return lines;
        }

        /// <summary>
        /// 包裹 PML 脚本到 handle 块（防止崩溃）
        /// </summary>
        private string WrapWithHandle(string script)
        {
            if (script.TrimStart().StartsWith("handle", StringComparison.OrdinalIgnoreCase))
                return script;

            return "handle any\n" + script + "\nendhandle";
        }

        /// <summary>
        /// 开发环境模拟执行：解析 PML 并返回模拟结果
        /// </summary>
        private string SimulateRun(string pmlScript)
        {
            // 检测集合查询模式
            var collMatch = Regex.Match(pmlScript,
                @"coll\s+all\s+(\w+)", RegexOptions.IgnoreCase);
            if (collMatch.Success)
            {
                string type = collMatch.Groups[1].Value;
                return $"[模拟] 查询 {type}: 返回 3 个元素\n- {type}-001\n- {type}-002\n- {type}-003";
            }

            // 检测存在性检查
            if (pmlScript.IndexOf("exist", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "TRUEA";
            }

            // 检测属性写入
            if (pmlScript.Contains(":WTHK") || pmlScript.Contains(":DIA"))
            {
                return "[模拟] 属性已修改";
            }

            return "[模拟] PML 执行完成";
        }
    }
}
