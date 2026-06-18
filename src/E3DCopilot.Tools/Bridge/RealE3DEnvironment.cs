using System;
using System.Collections.Generic;
using Aveva.Core.Database;
using Aveva.Core.Utilities.CommandLine;

namespace E3DCopilot.Tools.Bridge
{
    /// <summary>
    /// 真实 E3D 环境实现
    /// 使用 Aveva.* DLL 直接 API 调用（编译时引用 lib/e3d/ 下的 DLL）
    ///
    /// 已验证 API 签名（文档核实 + 反射验证）：
    ///   DbElement.GetElement(string dbUri)   -> static   ✅
    ///   DbElement.GetElement()              -> static   ✅
    ///   DbElement.GetAsString(DbAttribute)  -> string   ✅
    ///   DbElement.FirstMember()            -> DbElement ✅
    ///   DbElement.LastMember()             -> DbElement ✅
    ///   DbElement.Members()                -> DbElement[] ✅
    ///   DbAttribute.GetDbAttribute(string) -> static   ✅
    ///   Command.CreateCommand(string)      -> static   ✅
    ///   Command.RunInPdms()                -> bool     ✅
    ///
    /// 只应在 E3D 进程内加载（作为 Addin）。
    /// </summary>
    public class RealE3DEnvironment : IE3DEnvironment
    {
        public List<ElementInfo> QueryElements(string elementType, string namePattern, string scope, int limit)
        {
            var results = new List<ElementInfo>();
            if (limit <= 0) limit = 100;

            try
            {
                // 确定起始元素
                DbElement root;
                if (!string.IsNullOrEmpty(scope) && scope != "/")
                {
                    root = DbElement.GetElement(scope);
                }
                else
                {
                    root = DbElement.GetElement("/");
                }

                if (root == null) return results;

                // 一次性获取所有子元素并遍历
                DbElement[] children = root.Members();
                if (children == null) return results;

                int count = 0;
                foreach (var child in children)
                {
                    if (count >= limit) break;

                    string type = SafeGetAttr(child, "TYPE");
                    if (!string.IsNullOrEmpty(elementType) && !type.ToUpper().Contains(elementType.ToUpper()))
                        continue;

                    string name = SafeGetAttr(child, "NAME");
                    bool nameMatch = string.IsNullOrEmpty(namePattern);
                    if (!nameMatch)
                    {
                        string pat = namePattern.Replace("*", "").ToUpper();
                        nameMatch = name.ToUpper().Contains(pat);
                    }

                    if (nameMatch)
                    {
                        results.Add(BuildElementInfo(child));
                        count++;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[RealE3DEnvironment] QueryElements error: " + ex.Message);
            }

            return results;
        }

        public string GetAttribute(string elementName, string attributeName)
        {
            try
            {
                DbElement elem = ResolveElement(elementName);
                if (elem == null) return null;

                DbAttribute attr = DbAttribute.GetDbAttribute(attributeName.ToUpper());
                return elem.GetAsString(attr);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[RealE3DEnvironment] GetAttribute error: " + ex.Message);
                return null;
            }
        }

        public void SetAttribute(string elementName, string attributeName, string value)
        {
            try
            {
                // 属性写入通过 PML 执行（DbElement 没有直接的 SetAsString API）
                string pml = string.Format("{0}.{1} = '{2}'",
                    elementName.TrimStart('/'),
                    attributeName.ToUpper(),
                    value.Replace("'", "''"));
                ExecutePml(pml);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("[RealE3DEnvironment] SetAttribute error: " + ex.Message);
            }
        }

        public bool CheckExists(string elementName)
        {
            try
            {
                DbElement elem = ResolveElement(elementName);
                return elem != null;
            }
            catch
            {
                return false;
            }
        }

        public string ExecutePml(string pmlCommand)
        {
            try
            {
                var cmd = Command.CreateCommand(pmlCommand);
                bool ok = cmd.RunInPdms();
                return ok ? (cmd.Result ?? "") : "Error: PML execution failed";
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }
        }

        public string GetCurrentElementName()
        {
            try
            {
                // DbElement.GetElement() 无参静态方法返回当前元素
                DbElement current = DbElement.GetElement();
                if (current == null) return null;
                return SafeGetAttr(current, "NAME");
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 解析元素引用（支持 DBURI / 名称模式）
        /// </summary>
        private DbElement ResolveElement(string elementName)
        {
            if (string.IsNullOrEmpty(elementName)) return null;

            // 1. 尝试作为 DBURI
            if (elementName.StartsWith("/"))
            {
                var elem = DbElement.GetElement(elementName);
                if (elem != null) return elem;
            }

            // 2. 通过遍历根成员按 NAME 匹配
            DbElement root = DbElement.GetElement("/");
            if (root != null)
            {
                DbElement[] all = root.Members();
                if (all != null)
                {
                    foreach (var child in all)
                    {
                        string name = SafeGetAttr(child, "NAME");
                        if (name.Equals(elementName, StringComparison.OrdinalIgnoreCase))
                            return child;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 从 DbElement 构建 ElementInfo
        /// </summary>
        private ElementInfo BuildElementInfo(DbElement elem)
        {
            var info = new ElementInfo
            {
                Name = SafeGetAttr(elem, "NAME"),
                Type = SafeGetAttr(elem, "TYPE"),
                DbUri = string.Empty
            };

            // 尝试获取 DBURI
            try
            {
                DbAttribute uriAttr = DbAttribute.GetDbAttribute("DBURI");
                info.DbUri = elem.GetAsString(uriAttr);
            }
            catch
            {
                info.DbUri = $"/{info.Name}";
            }

            // 读取常用属性
            var commonAttrs = new[] { "NAME", "TYPE", "DESCRIPTION", "STATUS", "OWNER", "PURPOSE" };
            foreach (var attr in commonAttrs)
            {
                string val = SafeGetAttr(elem, attr);
                if (!string.IsNullOrEmpty(val))
                    info.Attributes[attr] = val;
            }

            return info;
        }

        private string SafeGetAttr(DbElement elem, string attrName)
        {
            try
            {
                DbAttribute attr = DbAttribute.GetDbAttribute(attrName);
                string val = elem.GetAsString(attr);
                return val ?? "";
            }
            catch
            {
                return "";
            }
        }
    }
}
