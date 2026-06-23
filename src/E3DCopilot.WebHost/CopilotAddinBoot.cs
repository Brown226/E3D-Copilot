using System;
using System.Reflection;
using System.Windows.Forms;
using Aveva.ApplicationFramework;
using Aveva.ApplicationFramework.Presentation;
using E3DCopilot.Core;
using E3DCopilot.Core.Tools;
using E3DCopilot.Tools.Bridge;
using E3DCopilot.Tools.Registry;

namespace E3DCopilot.WebHost
{
    /// <summary>
    /// E小智 E3D AI Copilot — Addin 入口（WebHost 版）
    /// 创建 WebView2 宿主面板（React 前端）
    /// 注意：降级到 CopilotPanel 的逻辑在 E3DCopilot.Addin/CopilotAddinBoot 中实现
    /// </summary>
    public class CopilotAddinBoot : Aveva.ApplicationFramework.Addin
    {
        private CopilotController _controller;
        private WebViewForm _webViewForm;
        private DockedWindow _dockedWindow;
        private bool _isStarted;

        public override string Name => "E3DCopilot";
        public override string Description => "E3D AI Copilot — 自然语言驱动的 E3D 操作助手";
        public override Assembly Assembly => Assembly.GetExecutingAssembly();
        public override bool IsStarted => _isStarted;
        public override IAddin IAddinInterfaceObject => (IAddin)this;

        public override void Start()
        {
            try
            {
                _isStarted = true;

                // 1. 创建 E3D 环境
                var env = new RealE3DEnvironment();
                var dispatcher = new E3DToolDispatcher(env);

                // 1a. 创建 ToolRouter（路由 6 核心工具 → 41 专用工具）
                var router = new ToolRouter();

                // 2. 创建 Controller
                _controller = CopilotController.CreateDefault(dispatcher, null, router);

                // 3. 创建 WebView2 宿主面板
                _webViewForm = new WebViewForm(_controller);

                // 4. 注册 DockedWindow（4 参数，含 DockedPosition）
                _dockedWindow = WindowManager.Instance.CreateDockedWindow(
                    "E3DCopilot",
                    "E小智 Copilot",
                    _webViewForm,
                    DockedPosition.Right
                );
                _dockedWindow.Width = 520;
                _dockedWindow.Show();

                var cmd = Aveva.Core.Utilities.CommandLine.Command
                    .CreateCommand("$p E小智 Copilot v1.0 已启动 (WebView2 + React)");
                cmd.RunInPdms();
            }
            catch (Exception ex)
            {
                var cmd = Aveva.Core.Utilities.CommandLine.Command
                    .CreateCommand("$p E小智 启动失败: " + ex.Message);
                cmd.RunInPdms();
            }
        }

        public override void Stop()
        {
            try
            {
                _isStarted = false;

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

                if (_webViewForm != null)
                {
                    try { _webViewForm.Dispose(); } catch { }
                    _webViewForm = null;
                }

                var cmd = Aveva.Core.Utilities.CommandLine.Command
                    .CreateCommand("$p E小智 Copilot 已停止");
                cmd.RunInPdms();
            }
            catch (Exception ex)
            {
                var cmd = Aveva.Core.Utilities.CommandLine.Command
                    .CreateCommand("$p E小智 停止异常: " + ex.Message);
                cmd.RunInPdms();
            }
        }
    }
}
