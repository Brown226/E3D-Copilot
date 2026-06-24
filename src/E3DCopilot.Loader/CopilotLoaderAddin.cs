using System;
using System.Collections.Generic;
using System.IO;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Aveva.ApplicationFramework;
using Aveva.ApplicationFramework.Presentation;

namespace E3DCopilot.Loader
{
    /// <summary>
    /// E小智 开发模式加载器 — 同域加载（兼容 E3D 2.1）
    /// 
    /// 功能：
    ///   - 在 Loader 所在 AppDomain 中直接加载开发版插件
    ///   - 使用 Assembly.Load(byte[]) 避免锁定 DLL
    ///   - 点击「重载」按钮 → 销毁旧面板 → 加载新 DLL
    ///   - 显示加载状态和版本信息
    ///   
    /// ⚠ WinForms 控件不能跨 AppDomain 封送，因此不创建独立子域。
    /// ⚠ 热重载需要重启 E3D（.NET Framework 不支持同域程序集卸载）。
    ///   
    /// 使用方式：
    ///   1. 将此 DLL 放到 E3D 根目录，注册到 DesignAddins.xml
    ///   2. 编译开发版后，重启 E3D 即可生效
    /// </summary>
    public class CopilotLoaderAddin : IAddin
    {
        private DockedWindow _dockedWindow;

        // ---- Loader 面板控件 ----
        private Panel _containerPanel;
        private Panel _loaderPanel;

        // ---- 当前加载的实例 ----
        private object _devAddinInstance;   // DevAddinImpl 实例（反射持有）
        private Control _devPanelControl;   // 当前面板

        // ---- 开发 DLL 路径 ----
        private string _devDir;

        // ---- 已加载的程序集集合（用于 AssemblyResolve）----
        private readonly Dictionary<string, Assembly> _devAssemblies
            = new Dictionary<string, Assembly>();

        public string Name => "E3DCopilot.Loader";
        public string Description => "E小智 开发模式加载器";

        public void Start(ServiceManager serviceManager)
        {
            try
            {
                InitializeDevPath();
                CreateLoaderPanel();
                _dockedWindow = WindowManager.Instance.CreateDockedWindow(
                    "E3DCopilot.Loader",
                    "E小智 Dev",
                    _loaderPanel,
                    DockedPosition.Right
                );
                _dockedWindow.Width = 520;
                _dockedWindow.Show();

                // 自动加载开发版
                LoadDevAddin();
            }
            catch (Exception ex)
            {
                try
                {
                    var cmd = Aveva.Core.Utilities.CommandLine.Command.CreateCommand(
                        "$p E小智 Loader 启动失败: " + ex.Message);
                    cmd.RunInPdms();
                }
                catch { }
            }
        }

        public void Stop()
        {
            UnloadDevAddin();
            _dockedWindow?.Close();
            _dockedWindow = null;
        }

        // ================================================================
        // 路径初始化
        // ================================================================

        private void InitializeDevPath()
        {
            // 优先使用环境变量 E3DCOPILOT_DEV
            string envDir = Environment.GetEnvironmentVariable("E3DCOPILOT_DEV");
            if (!string.IsNullOrEmpty(envDir))
            {
                _devDir = envDir;
                return;
            }

            // 默认开发路径
            _devDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                @"source\repos\E3D-E小智\E小智-v1.0-开发中\src\E3DCopilot.Addin\bin\Release\net48"
            );

            // 如果不存在，尝试项目路径
            if (!Directory.Exists(_devDir))
            {
                _devDir = @"E:\工作\E3D-E小智\E小智-v1.0-开发中\src\E3DCopilot.Addin\bin\Release\net48";
            }
        }

        // ================================================================
        // Loader 面板
        // ================================================================

        private void CreateLoaderPanel()
        {
            _loaderPanel = new Panel();
            _loaderPanel.BackColor = Color.FromArgb(45, 45, 48);

            // ---- 开发面板容器 ----
            _containerPanel = new Panel();
            _containerPanel.Dock = DockStyle.Fill;
            _containerPanel.BackColor = Color.FromArgb(45, 45, 48);

            // ---- 组装 ----
            _loaderPanel.Controls.Add(_containerPanel);
        }

        // SetStatus 已移除（顶部状态栏不再需要）
        private void SetStatus(string text, Color color) { /* no-op */ }

        // ================================================================
        // 加载核心 — 同域加载（无跨 AppDomain 问题）
        // ================================================================

        private void LoadDevAddin()
        {

            try
            {
                // 验证开发 DLL 是否存在
                string addinDllPath = Path.Combine(_devDir, "E3DCopilot.Addin.dll");
                string coreDllPath = Path.Combine(_devDir, "E3DCopilot.Core.dll");
                string toolsDllPath = Path.Combine(_devDir, "E3DCopilot.Tools.dll");
                string webHostDllPath = Path.Combine(_devDir, "E3DCopilot.WebHost.dll");

                if (!File.Exists(addinDllPath))
                {
                    string msg = "❌ 未找到 " + addinDllPath;
                    SetStatus(msg, Color.IndianRed);
                    OutputToE3D("[E小智] " + msg);
                    return;
                }

                // 获取 DLL 版本信息（在加载文件之前读取）
                var versionInfo = System.Diagnostics.FileVersionInfo.GetVersionInfo(addinDllPath);
                string version = versionInfo.FileVersion ?? versionInfo.ProductVersion ?? "?";

                // ---- 以字节数组加载 DLL，不锁定源文件 ----
                _devAssemblies.Clear();

                // 先注册 AssemblyResolve 处理器，确保依赖解析能走通
                AppDomain.CurrentDomain.AssemblyResolve += OnDevAssemblyResolve;

                // 按依赖顺序加载：Core → Tools → WebHost → Addin
                byte[] coreBytes = File.ReadAllBytes(coreDllPath);
                var coreAsm = Assembly.Load(coreBytes);
                _devAssemblies[coreAsm.GetName().Name] = coreAsm;
                System.Diagnostics.Debug.WriteLine("[Loader] 已加载 " + coreAsm.GetName().Name);

                byte[] toolsBytes = File.ReadAllBytes(toolsDllPath);
                var toolsAsm = Assembly.Load(toolsBytes);
                _devAssemblies[toolsAsm.GetName().Name] = toolsAsm;
                System.Diagnostics.Debug.WriteLine("[Loader] 已加载 " + toolsAsm.GetName().Name);

                if (File.Exists(webHostDllPath))
                {
                    byte[] webHostBytes = File.ReadAllBytes(webHostDllPath);
                    var webAsm = Assembly.Load(webHostBytes);
                    _devAssemblies[webAsm.GetName().Name] = webAsm;
                    System.Diagnostics.Debug.WriteLine("[Loader] 已加载 " + webAsm.GetName().Name);
                }

                byte[] addinBytes = File.ReadAllBytes(addinDllPath);
                var addinAsm = Assembly.Load(addinBytes);
                _devAssemblies[addinAsm.GetName().Name] = addinAsm;
                System.Diagnostics.Debug.WriteLine("[Loader] 已加载 " + addinAsm.GetName().Name);

                // ---- 通过反射创建 DevAddinImpl（避免类型标识不匹配）----
                Type devAddinType = addinAsm.GetType("E3DCopilot.Addin.DevAddinImpl");
                if (devAddinType == null)
                {
                    throw new TypeLoadException("在 E3DCopilot.Addin.dll 中未找到类型 E3DCopilot.Addin.DevAddinImpl");
                }

                _devAddinInstance = Activator.CreateInstance(devAddinType);

                // ---- 调用 CreatePanel() 方法获取面板 ----
                MethodInfo createPanelMethod = devAddinType.GetMethod("CreatePanel");
                if (createPanelMethod == null)
                {
                    throw new MissingMethodException("DevAddinImpl 缺少 CreatePanel 方法");
                }

                var panel = (Control)createPanelMethod.Invoke(_devAddinInstance, null);
                if (panel == null)
                {
                    throw new InvalidOperationException("DevAddinImpl.CreatePanel() 返回 null");
                }

                panel.Dock = DockStyle.Fill;

                // ---- 添加到容器（同域，无 remoting 问题）----
                _containerPanel.Controls.Clear();
                _containerPanel.Controls.Add(panel);
                _containerPanel.Update();
                _devPanelControl = panel;

                SetStatus($"✅ 已加载 v{version}", Color.LightGreen);
                OutputToE3D($"[E小智] 已加载开发版 v{version}");

                // WebView2 加载成功后隐藏状态标签（节省空间）
            // CE 诊断弹窗（Loader 同域，可直接访问 E3D API）
                try
                {
                    string ceDiag = "CE 诊断:\n\n";
                    var ce1 = Aveva.Core.Database.CurrentElement.Element;
                    if (ce1 != null && ce1.IsValid)
                    {
                        string n1;
                        try { n1 = ce1.GetAsString(Aveva.Core.Database.DbAttributeInstance.NAME); } catch { n1 = null; }
                        string t1;
                        try { t1 = ce1.GetElementType().ShortName; } catch { t1 = "?"; }
                        ceDiag += $"方法1(CurrentElement.Element) → '{n1 ?? "(no name)"}' 类型={t1}\n";
                    }
                    else
                    {
                        ceDiag += $"方法1(CurrentElement.Element) → null/invalid\n";
                    }

                    var ce2 = Aveva.Core.Database.DbElement.GetElement();
                    if (ce2 != null && ce2.IsValid)
                    {
                        string n2;
                        try { n2 = ce2.GetAsString(Aveva.Core.Database.DbAttributeInstance.NAME); } catch { n2 = null; }
                        string t2;
                        try { t2 = ce2.GetElementType().ShortName; } catch { t2 = "?"; }
                        ceDiag += $"方法2(DbElement.GetElement) → '{n2 ?? "(no name)"}' 类型={t2}\n";
                    }
                    else
                    {
                        ceDiag += $"方法2(DbElement.GetElement) → null/invalid\n";
                    }

                    // 输出到 System Message
                    try
                    {
                        var echo = Aveva.Core.Utilities.CommandLine.Command.CreateCommand(
                            "$p " + ceDiag.Replace("'", "''").Replace("\n", " | "));
                        echo.RunInPdms();
                    }
                    catch { }

                    // 弹窗已移除 —— 诊断信息在 System Message 窗口查看
                }
                catch (Exception ex)
                {
                    // 仅通过 System Message 输出错误
                    try
                    {
                        var echo = Aveva.Core.Utilities.CommandLine.Command.CreateCommand(
                            "$p [CE诊断错误] " + ex.Message.Replace("'", "''"));
                        echo.RunInPdms();
                    }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                string errorText = $"❌ {ex.GetType().Name}: {ex.Message}";
                // ponytail: 内层异常更关键
                if (ex.InnerException != null)
                    errorText += $" ⮕ {ex.InnerException.GetType().Name}: {ex.InnerException.Message}";
                SetStatus(errorText, Color.IndianRed);

                // 弹窗已移除 —— 错误信息通过 System Message 输出
                OutputToE3D($"[E小智 Loader] {ex.GetType().Name}: {ex.Message}");
                if (ex.InnerException != null)
                    OutputToE3D($"[E小智 Loader] 原因: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");
            }
            finally
            {
                // reload button removed
            }
        }

        /// <summary>
        /// AssemblyResolve 处理器：为字节数组加载的程序集解析依赖
        /// </summary>
        private Assembly OnDevAssemblyResolve(object sender, ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name;
            if (_devAssemblies.TryGetValue(name, out var existing))
                return existing;

            // 尝试从开发目录直接加载
            string dllPath = Path.Combine(_devDir, name + ".dll");
            if (File.Exists(dllPath))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(dllPath);
                    var asm = Assembly.Load(bytes);
                    _devAssemblies[name] = asm;
                    System.Diagnostics.Debug.WriteLine("[Loader] AssemblyResolve 加载: " + name);
                    return asm;
                }
                catch { }
            }

            return null;
        }

        /// <summary>
        /// 将消息输出到 E3D 消息窗（PML $p 命令）
        /// </summary>
        private void OutputToE3D(string message)
        {
            try
            {
                var cmd = Aveva.Core.Utilities.CommandLine.Command.CreateCommand(
                    "$p " + message.Replace("'", "''"));
                cmd.RunInPdms();
            }
            catch
            {
                // E3D 消息窗输出失败，忽略
            }
        }

        /// <summary>
        /// 销毁当前开发版实例（不卸载程序集）
        /// </summary>
        private void UnloadDevAddin()
        {
            // 先销毁面板控件
            try
            {
                if (_devPanelControl != null && !_devPanelControl.IsDisposed)
                {
                    _containerPanel.Controls.Remove(_devPanelControl);
                    _devPanelControl.Dispose();
                }
            }
            catch { }
            _devPanelControl = null;

            // 调用 DevAddinImpl.Destroy()
            if (_devAddinInstance != null)
            {
                try
                {
                    Type t = _devAddinInstance.GetType();
                    MethodInfo destroyMethod = t.GetMethod("Destroy");
                    destroyMethod?.Invoke(_devAddinInstance, null);
                }
                catch { }
                _devAddinInstance = null;
            }

            // 清理 AssemblyResolve 处理器（如果不再需要）
            // 注意：程序集一旦加载，在本进程内无法卸载

            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        /// <summary>
        /// 重载按钮 — 由于 .NET Framework 不支持同域程序集卸载，
        /// 重载会销毁旧面板和控制器（使用旧程序集中的类型），
        /// 然后提示重启 E3D。
        /// </summary>
        private void OnReloadClick(object sender, EventArgs e)
        {
            UnloadDevAddin();

            // 尝试重新加载（如果程序集已存在，Assembly.Load 会返回已缓存的版本）
            // 因此真正更新代码需要重启 E3D
            LoadDevAddin();
        }
    }
}
