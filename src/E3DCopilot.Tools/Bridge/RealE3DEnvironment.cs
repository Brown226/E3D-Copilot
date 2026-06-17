using System;
using System.Collections.Generic;
using E3DCopilot.Core.Threading;

namespace E3DCopilot.Tools.Bridge
{
    /// <summary>
    /// 真实 E3D 环境实现
    /// 所有 API 调用通过 ThreadMarshaller 切换到 UI 线程
    /// 
    /// 关键 API 签名（已核实）：
    /// - DbElement.GetElement(string dbUri) → DbElement
    /// - DbAttribute.GetDbAttribute(string name) → DbAttribute
    /// - DbElement.GetAsString(DbAttribute) → string
    /// - Command.CreateCommand(string) → Command
    /// - Command.RunInPdms() → bool
    /// </summary>
    public class RealE3DEnvironment : IE3DEnvironment
    {
        public List<ElementInfo> QueryElements(string elementType, string namePattern, string scope, int limit)
        {
            return ThreadMarshaller.Invoke(() =>
            {
                var results = new List<ElementInfo>();

                // 构建 PML 查询命令
                string pmlQuery = BuildPmlQuery(elementType, namePattern, scope, limit);
                string output = ExecutePmlInternal(pmlQuery);

                // 解析 PML 输出为 ElementInfo 列表
                if (!string.IsNullOrEmpty(output) && !output.StartsWith("Error"))
                {
                    var lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (var line in lines)
                    {
                        var parts = line.Split('|');
                        if (parts.Length >= 2)
                        {
                            var info = new ElementInfo
                            {
                                Name = parts[0].Trim(),
                                Type = parts[1].Trim(),
                                DbUri = parts.Length > 2 ? parts[2].Trim() : ""
                            };
                            results.Add(info);
                        }
                    }
                }

                return results;
            });
        }

        public string GetAttribute(string elementName, string attributeName)
        {
            return ThreadMarshaller.Invoke(() =>
            {
                // 通过 PML 间接读取
                string pml = string.Format("$p val = !{0}.{1}; $p val", elementName, attributeName);
                string result = ExecutePmlInternal(pml);
                return string.IsNullOrEmpty(result) ? null : result.Trim();
            });
        }

        public void SetAttribute(string elementName, string attributeName, string value)
        {
            ThreadMarshaller.Invoke(() =>
            {
                string pml = string.Format("$p {0}.{1} = {2}", elementName, attributeName, value);
                ExecutePmlInternal(pml);
            });
        }

        public bool CheckExists(string elementName)
        {
            return ThreadMarshaller.Invoke(() =>
            {
                string pml = string.Format("if exist {0} then $p YES else $p NO", elementName);
                string result = ExecutePmlInternal(pml);
                return result != null && result.Trim().Contains("YES");
            });
        }

        public string ExecutePml(string pmlCommand)
        {
            return ThreadMarshaller.Invoke(() => ExecutePmlInternal(pmlCommand));
        }

        public string GetCurrentElementName()
        {
            return ThreadMarshaller.Invoke(() =>
            {
                string pml = "$p !ce.Name";
                string result = ExecutePmlInternal(pml);
                return string.IsNullOrEmpty(result) ? null : result.Trim();
            });
        }

        /// <summary>
        /// 内部 PML 执行方法（已在 UI 线程上调用）
        /// 使用反射避免编译时对 E3D DLL 的硬依赖
        /// </summary>
        private string ExecutePmlInternal(string pmlCommand)
        {
            try
            {
                var cmdType = Type.GetType("Aveva.Core.Utilities.CommandLine.Command, Aveva.Core");
                if (cmdType == null)
                    return "Error: E3D API 未加载";

                var createMethod = cmdType.GetMethod("CreateCommand", new[] { typeof(string) });
                if (createMethod == null)
                    return "Error: CreateCommand 方法未找到";

                var cmd = createMethod.Invoke(null, new object[] { pmlCommand });
                var runMethod = cmdType.GetMethod("RunInPdms");
                if (runMethod == null)
                    return "Error: RunInPdms 方法未找到";

                bool ok = (bool)runMethod.Invoke(cmd, null);
                var resultProp = cmdType.GetProperty("Result");
                string result = resultProp != null ? resultProp.GetValue(cmd)?.ToString() ?? "" : "";

                return ok ? result : "Error: PML 执行失败";
            }
            catch (Exception ex)
            {
                return string.Format("Error: {0}", ex.Message);
            }
        }

        /// <summary>
        /// 构建 PML 查询脚本
        /// </summary>
        private string BuildPmlQuery(string elementType, string namePattern, string scope, int limit)
        {
            string typeFilter = string.IsNullOrEmpty(elementType) ? "" : string.Format("type eq {0}", elementType);
            string nameFilter = string.IsNullOrEmpty(namePattern) ? "" : string.Format("name like {0}", namePattern);
            string scopeUri = string.IsNullOrEmpty(scope) ? "!" : scope;

            return string.Format("$p query elements in {0} where {1} {2} limit {3}", scopeUri, typeFilter, nameFilter, limit);
        }
    }
}
