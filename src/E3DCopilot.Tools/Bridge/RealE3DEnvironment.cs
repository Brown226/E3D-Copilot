using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Aveva.Core.Database;
using Aveva.Core.Utilities.CommandLine;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Tools.Bridge
{
    /// <summary>
    /// 真实 E3D 环境实现
    /// 使用 Aveva.* DLL 直接 API 调用（编译时引用 lib/e3d/ 下的 DLL）
    ///
    /// 已验证 API 签名（文档核实 + 反射验证）：
    ///   DbElement.GetElement(string dbUri)   -> static   ✅
    ///   DbElement.GetElement()              -> static   ✅
    ///   CurrentElement.Element              -> static   ✅ (正确获取 CE 的 API)
    ///   DbElement.GetAsString(DbAttribute)  -> string   ✅
    ///   DbElement.FirstMember()            -> DbElement ✅
    ///   DbElement.LastMember()             -> DbElement ✅
    ///   DbElement.Members()                -> DbElement[] ✅
    ///   DbAttribute.GetDbAttribute(string) -> static   ✅
    ///   Command.CreateCommand(string)      -> static   ✅
    ///   Command.RunInPdms()                -> bool     ✅
    ///
    /// 只应在 E3D 进程内加载（作为 Addin）。
    /// ⚠ 所有 E3D API 调用通过 SynchronizationContext 封送到 UI 线程
    /// </summary>
    public class RealE3DEnvironment : IE3DEnvironment
    {
        private readonly SynchronizationContext _uiContext;
        // 缓存当前元素名称，通过 CurrentElementChanged 事件实时更新
        private string _currentElementName;
        // 多选元素列表，通过 Selection.SelectionChanged 事件实时更新
        private readonly List<string> _selectedElementNames = new List<string>();
        private readonly object _selectedLock = new object();

        /// <summary>
        /// 必须在 E3D UI 线程上创建（捕获 SynchronizationContext）
        /// 同时订阅 CurrentElementChanged 事件，实时跟踪当前选中元素
        /// </summary>
        public RealE3DEnvironment()
        {
            _uiContext = SynchronizationContext.Current
                ?? new SynchronizationContext();

            // 初始化当前元素缓存 — 使用 DbElement.GetElement()
            try
            {
                DbElement ce = DbElement.GetElement();
                if (ce != null && ce.IsValid)
                {
                    string initName;
                    try { initName = ce.GetAsString(DbAttributeInstance.NAME); } catch { initName = null; }
                    if (string.IsNullOrEmpty(initName))
                        try { initName = ce.GetAsString(DbAttributeInstance.FLNN); } catch { initName = null; }
                    _currentElementName = initName;
                }
            }
            catch
            {
                _currentElementName = null;
            }

            // 订阅 CE 变化事件 — 实时跟踪用户选择的元素
            try
            {
                CurrentElement.CurrentElementChanged += OnCurrentElementChanged;
            }
            catch
            {
                // E3D 可能尚未完全初始化，忽略订阅失败
            }

            // 订阅多选变化事件 — 实时跟踪用户选中的所有元素
            // 注意：Selection 类在 Aveva.Pdms.Shared 命名空间，需引用 Aveva.Pdms.Shared.dll
            // 暂未引用该 DLL，后续添加引用后启用以下代码
            /*
            try
            {
                Selection.SelectionChanged += OnSelectionChanged;
                var sel = Selection.CurrentSelection;
                if (sel != null && sel.Members != null)
                {
                    lock (_selectedLock)
                    {
                        _selectedElementNames.Clear();
                        foreach (DbElement member in sel.Members)
                        {
                            if (member != null && member.IsValid)
                            {
                                string n;
                                try { n = member.GetAsString(DbAttributeInstance.NAME); } catch { n = null; }
                                if (!string.IsNullOrEmpty(n))
                                    _selectedElementNames.Add(n);
                            }
                        }
                    }
                }
            }
            catch
            {
                // E3D 可能尚未完全初始化，忽略订阅失败
            }
            */
        }

        /// <summary>
        /// 当前元素变化时的回调
        /// </summary>
        private void OnCurrentElementChanged(object sender, CurrentElementChangedEventArgs e)
        {
            try
            {
                DbElement ce = e?.Element;
                if (ce != null && ce.IsValid)
                {
                    string name;
                    try { name = ce.GetAsString(DbAttributeInstance.NAME); } catch { name = null; }
                    if (string.IsNullOrEmpty(name))
                        try { name = ce.GetAsString(DbAttributeInstance.FLNN); } catch { name = null; }
                    _currentElementName = name;
                }
                else
                {
                    _currentElementName = null;
                }
            }
            catch
            {
                _currentElementName = null;
            }
        }

        /// <summary>
        /// 多选变化时的回调
        /// 注意：需要先引用 Aveva.Pdms.Shared.dll 并添加 using Aveva.Pdms.Shared;
        /// </summary>
        /*
        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                lock (_selectedLock)
                {
                    _selectedElementNames.Clear();
                    var sel = Selection.CurrentSelection;
                    if (sel != null && sel.Members != null)
                    {
                        foreach (DbElement member in sel.Members)
                        {
                            if (member != null && member.IsValid)
                            {
                                string n;
                                try { n = member.GetAsString(DbAttributeInstance.NAME); } catch { n = null; }
                                if (!string.IsNullOrEmpty(n))
                                    _selectedElementNames.Add(n);
                            }
                        }
                    }
                }
            }
            catch
            {
                // 忽略
            }
        }
        */

        /// <summary>
        /// 获取当前多选的所有元素名称
        /// </summary>
        public List<string> GetSelectedElementNames()
        {
            lock (_selectedLock)
            {
                return _selectedElementNames.ToList();
            }
        }

        /// <summary>
        /// 在 E3D UI 线程上执行委托（E3D API 非线程安全）
        /// 使用 Post + TaskCompletionSource 避免线程死锁，带可配置超时保护
        /// </summary>
        private T InvokeOnUi<T>(Func<T> action, int timeoutMs = 5000)
        {
            // 如果当前就在 UI 线程上，直接执行
            if (SynchronizationContext.Current == _uiContext)
            {
                return action();
            }

            var tcs = new TaskCompletionSource<T>();
            _uiContext.Post(_ =>
            {
                try
                {
                    var result = action();
                    tcs.SetResult(result);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }, null);

            // 等待结果，带超时
            if (tcs.Task.Wait(TimeSpan.FromMilliseconds(timeoutMs)))
            {
                return tcs.Task.Result;
            }
            else
            {
                throw new TimeoutException($"E3D UI thread operation timeout ({timeoutMs / 1000}s). E3D may be unresponsive.");
            }
        }

        private void InvokeOnUi(Action action, int timeoutMs = 5000)
        {
            // 如果当前就在 UI 线程上，直接执行
            if (SynchronizationContext.Current == _uiContext)
            {
                action();
                return;
            }

            var tcs = new TaskCompletionSource<object>();
            _uiContext.Post(_ =>
            {
                try
                {
                    action();
                    tcs.SetResult(null);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            }, null);

            // 等待结果，带超时
            if (!tcs.Task.Wait(TimeSpan.FromMilliseconds(timeoutMs)))
            {
                throw new TimeoutException($"E3D UI thread operation timeout ({timeoutMs / 1000}s). E3D may be unresponsive.");
            }
        }
        public List<ElementInfo> QueryElements(string elementType, string namePattern, string scope, int limit)
        {
            return InvokeOnUi(() =>
            {
                var results = new List<ElementInfo>();
                if (limit <= 0) limit = 100;

                try
                {
                    // 确定起始元素 — 使用 ResolveElement 支持多种名称格式
                    DbElement root;
                    if (!string.IsNullOrEmpty(scope) && scope != "/")
                    {
                        root = ResolveElement(scope);
                    }
                    else
                    {
                        root = DbElement.GetElement("/");
                    }

                    if (root == null || !root.IsValid) return results;

                    int count = 0;
                    QueryRecursive(root, elementType, namePattern, limit, ref count, results);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("[RealE3DEnvironment] QueryElements error: " + ex.Message);
                }

                return results;
            });
        }

        /// <summary>
        /// 递归遍历所有子元素（深度优先），支持按类型/名称过滤
        /// </summary>
        private void QueryRecursive(DbElement parent, string elementType, string namePattern, int limit, ref int count, List<ElementInfo> results)
        {
            if (count >= limit) return;
            if (parent == null) return;

            try
            {
                DbElement[] children = parent.Members();
                if (children == null) return;

                foreach (var child in children)
                {
                    if (count >= limit) break;
                    if (child == null) continue;

                    string type = SafeGetAttr(child, "TYPE");
                    bool typeMatch = string.IsNullOrEmpty(elementType)
                        || elementType == "*"
                        || elementType.Equals("ALL", StringComparison.OrdinalIgnoreCase)
                        || type.ToUpper().Contains(elementType.ToUpper());

                    string name = SafeGetAttr(child, "NAME");
                    bool nameMatch = string.IsNullOrEmpty(namePattern)
                        || name.ToUpper().Contains(namePattern.Replace("*", "").ToUpper());

                    if (typeMatch && nameMatch)
                    {
                        results.Add(BuildElementInfo(child));
                        count++;
                    }

                    // 递归遍历子元素
                    QueryRecursive(child, elementType, namePattern, limit, ref count, results);
                }
            }
            catch
            {
                // 跳过无法访问的元素，继续遍历
            }
        }

        public string GetAttribute(string elementName, string attributeName)
        {
            return InvokeOnUi(() =>
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
            });
        }

        public bool SetAttribute(string elementName, string attributeName, string value)
        {
            return InvokeOnUi(() =>
            {
                try
                {
                    // 属性写入通过 PML 执行（DbElement 没有直接的 SetAsString API）
                    // 正确写法：先 navigate 到元素，再设置属性
                    string pml = string.Format("$P var = DB ELEMENT '{0}' ; var.{1} = '{2}'",
                        elementName.Replace("'", "''"),
                        attributeName.ToUpper(),
                        value.Replace("'", "''"));
                    string result = ExecutePml(pml);

                    // 检查 ExecutePml 返回值（C# 异常和超时会产生 "Error:" 前缀）
                    if (result != null && result.StartsWith("Error:"))
                        return false;

                    // 回读验证：PML handle return 会吞掉 PML 级错误，需验证属性是否实际写入
                    string actualValue = GetAttribute(elementName, attributeName);
                    if (actualValue == null)
                        return false;

                    // 值匹配检查（忽略大小写和首尾空格）
                    return string.Equals(actualValue.Trim(), value.Trim(), StringComparison.OrdinalIgnoreCase);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("[RealE3DEnvironment] SetAttribute error: " + ex.Message);
                    return false;
                }
            });
        }

        public bool CheckExists(string elementName)
        {
            return InvokeOnUi(() =>
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
            });
        }

        /// <summary>
        /// 创建子元素（通过 PML，与现有 SetAttribute 模式保持一致）
        /// </summary>
        public string CreateElement(string parentElement, string name, string elementType, string attributesJson)
        {
            return InvokeOnUi(() =>
            {
                try
                {
                    // 构建 PML 创建脚本
                    string pml = $"$P parent = DB ELEMENT '{parentElement.Replace("'", "''")}'";
                    pml += $" ; $P new = NEW {elementType.ToUpper()} parent";
                    if (!string.IsNullOrEmpty(name))
                        pml += $" ; $P new.NAME = '{name.Replace("'", "''")}'";

                    // 设置附加属性
                    if (!string.IsNullOrEmpty(attributesJson))
                    {
                        try
                        {
                            var jattrs = JObject.Parse(attributesJson);
                            foreach (var prop in jattrs.Properties())
                            {
                                string val = prop.Value?.ToString() ?? "";
                                pml += $" ; $P new.{prop.Name.ToUpper()} = '{val.Replace("'", "''")}'";
                            }
                        }
                        catch { /* 忽略属性解析错误 */ }
                    }

                    string pmlResult = ExecutePml(pml);

                    // 校验 ExecutePml 返回值
                    if (pmlResult != null && pmlResult.StartsWith("Error:"))
                    {
                        return $"{{\"success\": false, \"error\": \"创建元素失败: {pmlResult}\"}}";
                    }

                    // 验证元素是否实际创建成功
                    string verifyName = !string.IsNullOrEmpty(name) ? name : parentElement;
                    bool created = CheckExists(verifyName);

                    var result = new JObject
                    {
                        ["success"] = created,
                        ["name"] = name ?? "",
                        ["type"] = elementType,
                        ["parent"] = parentElement
                    };
                    if (!created)
                        result["error"] = "创建后验证失败：元素不存在";
                    return result.ToString();
                }
                catch (Exception ex)
                {
                    return $"{{\"success\": false, \"error\": \"创建元素失败: {ex.Message}\"}}";
                }
            });
        }

        /// <summary>
        /// 删除元素（通过 PML）
        /// </summary>
        public bool DeleteElement(string elementName)
        {
            return InvokeOnUi(() =>
            {
                try
                {
                    string pml = $"$P var = DB ELEMENT '{elementName.Replace("'", "''")}' ; DELETE $P var";
                    string result = ExecutePml(pml);

                    // 校验 ExecutePml 返回值
                    if (result != null && result.StartsWith("Error:"))
                        return false;

                    // 验证元素是否实际删除
                    return !CheckExists(elementName);
                }
                catch
                {
                    return false;
                }
            });
        }

        /// <summary>
        /// 执行 PML 命令（带超时保护，防止卡死）
        /// 参考 CNPE 官方 PmlUtility.RunInPdmsAsMac 模式：
        ///   1. 写入临时文件
        ///   2. 用 $m "路径" 执行（双引号包裹，支持 PML1+PML2）
        ///   3. Command.CreateCommand($m "路径").RunInPdms()
        /// </summary>
        public string ExecutePml(string pmlCommand)
        {
            // 写入临时文件（用 handle return 包裹防止错误弹窗）
            string tempFile = System.IO.Path.GetTempFileName();
            try
            {
                string wrapped = "handle return\n" + pmlCommand + "\nendhandle";
                // 使用 UTF-8 编码（避免跨区域设置导致中文 PML 乱码）
                System.IO.File.WriteAllText(tempFile, wrapped, new System.Text.UTF8Encoding(false));

                // CNPE 官方格式：$m "路径"（双引号，不是单引号）
                string cmd = "$m \"" + tempFile + "\"";
                return ExecutePmlDirect(cmd);
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }
            finally
            {
                try { System.IO.File.Delete(tempFile); } catch { }
            }
        }

        /// <summary>
        /// 直接执行单条 PML 命令（带超时保护）
        /// </summary>
        private string ExecutePmlDirect(string pmlCommand)
        {
            var task = Task.Run(() =>
            {
                // PML 操作可能较慢，使用 30 秒超时（而非默认 5 秒）
                return InvokeOnUi(() =>
                {
                    try
                    {
                        var cmd = Command.CreateCommand(pmlCommand);
                        bool ok = cmd.RunInPdms();
                        return ok ? (cmd.Result ?? "") : "Error: PML execution failed (RunInPdms returned false)";
                    }
                    catch (Exception ex)
                    {
                        return "Error: " + ex.Message;
                    }
                }, 30000);
            });

            // 外层超时 60 秒（内层 30 秒会先触发）
            if (task.Wait(TimeSpan.FromSeconds(60)))
            {
                return task.Result;
            }
            else
            {
                return "Error: PML execution timeout (60s). E3D may be busy or unresponsive.";
            }
        }

        public string GetCurrentElementName()
        {
            return InvokeOnUi(() =>
            {
                // 方法1: CurrentElement.Element 静态属性（推荐，参考项目验证）
                try
                {
                    DbElement current = CurrentElement.Element;
                    if (current != null && current.IsValid)
                    {
                        string name;
                        try { name = current.GetAsString(DbAttributeInstance.NAME); } catch { name = null; }
                        if (string.IsNullOrEmpty(name))
                            try { name = current.GetAsString(DbAttributeInstance.FLNN); } catch { name = null; }
                        if (!string.IsNullOrEmpty(name))
                        {
                            _currentElementName = name;
                            return name;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("[CE] CurrentElement.Element 异常: " + ex.Message);
                }

                // 方法2: DbElement.GetElement() 无参调用（回退）
                try
                {
                    DbElement current = DbElement.GetElement();
                    if (current != null && current.IsValid)
                    {
                        string name;
                        try { name = current.GetAsString(DbAttributeInstance.NAME); } catch { name = null; }
                        if (string.IsNullOrEmpty(name))
                            try { name = current.GetAsString(DbAttributeInstance.FLNN); } catch { name = null; }
                        if (!string.IsNullOrEmpty(name))
                        {
                            _currentElementName = name;
                            return name;
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("[CE] DbElement.GetElement 异常: " + ex.Message);
                }

                // 最终回退：使用缓存值
                return _currentElementName;
            });
        }

        /// <summary>
        /// 解析元素引用（支持 DBURI / 名称模式）
        /// </summary>
        private DbElement ResolveElement(string elementName)
        {
            if (string.IsNullOrEmpty(elementName)) return null;

            // 1. 尝试作为 DBURI（带 / 和不带 / 都试试）
            foreach (var name in new[] { elementName, elementName.TrimStart('/') })
            {
                try
                {
                    var elem = DbElement.GetElement(name);
                    if (elem != null && elem.IsValid) return elem;
                }
                catch { }
            }

            // 2. 通过遍历根成员按 NAME 匹配
            DbElement root = DbElement.GetElement("/");
            if (root != null)
            {
                var found = FindElementRecursive(root, elementName);
                if (found != null) return found;
            }

            return null;
        }

        /// <summary>
        /// 递归搜索元素（深度优先），按 NAME 匹配
        /// </summary>
        private DbElement FindElementRecursive(DbElement parent, string elementName)
        {
            if (parent == null) return null;

            try
            {
                string name = SafeGetAttr(parent, "NAME");
                var targetName = elementName.TrimStart('/');
                if (name.Equals(elementName, StringComparison.OrdinalIgnoreCase) ||
                    name.TrimStart('/').Equals(targetName, StringComparison.OrdinalIgnoreCase))
                    return parent;
            }
            catch { }

            try
            {
                DbElement[] children = parent.Members();
                if (children != null)
                {
                    foreach (var child in children)
                    {
                        var found = FindElementRecursive(child, elementName);
                        if (found != null) return found;
                    }
                }
            }
            catch { }

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
