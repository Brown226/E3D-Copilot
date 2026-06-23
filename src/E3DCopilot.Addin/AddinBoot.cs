using System;
using System.Reflection;
using System.Windows.Forms;
using Aveva.ApplicationFramework;
using Aveva.ApplicationFramework.Presentation;
using E3DCopilot.Addin;
using E3DCopilot.Core;
using E3DCopilot.Core.Tools;
using E3DCopilot.Tools.Bridge;
using E3DCopilot.Tools.Registry;

namespace E3DCopilot.Addin
{
    /// <summary>
    /// E小智 Addin — E3D AI Copilot 入口
    /// 优先创建 WebViewForm（React 前端），WebView2 不可用时降级到 CopilotPanel（WinForms）
    /// </summary>
    public class CopilotAddin : IAddin
    {
        private DockedWindow _dockedWindow;
        private CopilotController _controller;
        private Control _activePanel;   // 当前激活的面板（WebViewForm 或 CopilotPanel）
        private bool _usingWebView;     // 标记当前使用的是 WebView2 还是 WinForms 降级

        public string Name => "E3DCopilot";
        public string Description => "E小智 AI Copilot for E3D";

        public void Start(ServiceManager serviceManager)
        {
            try
            {
                // 1. 创建 E3D 真实环境
                var env = new RealE3DEnvironment();
                var dispatcher = new E3DToolDispatcher(env);

                // 1a. 创建 ToolRouter（路由 6 核心工具 → 41 专用工具）
                var router = new ToolRouter();

                // 2. 创建 Controller
                _controller = CopilotController.CreateDefault(dispatcher, null, router);

                // 3. 优先创建 WebView2 面板（React 前端），失败则降级到 WinForms
                _activePanel = CreatePanelWithFallback();

                // 4. 注册 DockedWindow（4 参数，含 DockedPosition）
                _dockedWindow = WindowManager.Instance.CreateDockedWindow(
                    "E3DCopilot",
                    "E小智",
                    _activePanel,
                    DockedPosition.Right
                );
                _dockedWindow.Width = 520;
                _dockedWindow.Show();

                string uiMode = _usingWebView ? "WebView2 + React" : "WinForms (降级)";
                var cmd = Aveva.Core.Utilities.CommandLine.Command.CreateCommand(
                    "$p E小智 Copilot v1.0 已启动 (" + uiMode + ")");
                cmd.RunInPdms();
            }
            catch (Exception ex)
            {
                try
                {
                    var cmd = Aveva.Core.Utilities.CommandLine.Command.CreateCommand(
                        "$p E小智 启动失败: " + ex.Message);
                    cmd.RunInPdms();
                }
                catch { }
            }
        }

        public void Stop()
        {
            if (_controller != null)
            {
                _controller.Dispose();
                _controller = null;
            }
            if (_dockedWindow != null)
            {
                _dockedWindow.Close();
                _dockedWindow = null;
            }
            if (_activePanel != null)
            {
                try { _activePanel.Dispose(); } catch { }
                _activePanel = null;
            }
            _usingWebView = false;
        }

        /// <summary>
        /// 优先创建 WebViewForm，WebView2 Runtime 不可用时降级到 CopilotPanel
        /// </summary>
        private Control CreatePanelWithFallback()
        {
            // 尝试 WebView2（通过反射检测，避免 Addin 直接引用 WebView2 DLL）
            try
            {
                var webViewForm = new E3DCopilot.WebHost.WebViewForm(_controller);
                _usingWebView = true;
                return webViewForm;
            }
            catch (Exception ex)
            {
                // WebView2 Runtime 未安装或初始化失败，降级到 WinForms
                try
                {
                    var fallbackCmd = Aveva.Core.Utilities.CommandLine.Command.CreateCommand(
                        "$p E小智 WebView2 不可用，降级到 WinForms 面板 (" + ex.Message + ")");
                    fallbackCmd.RunInPdms();
                }
                catch { }

                _usingWebView = false;
                return new CopilotPanel(_controller);
            }
        }
    }
}
