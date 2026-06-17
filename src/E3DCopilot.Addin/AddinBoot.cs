using System;
using Aveva.ApplicationFramework;
using Aveva.ApplicationFramework.Presentation;
using E3DCopilot.Core;
using E3DCopilot.Tools.Registry;
using E3DCopilot.WebHost;

namespace E3DCopilot.Addin
{
    /// <summary>
    /// E小智 E3D AI Copilot 插件入口
    /// 委托 WebHost 管理 WebView2 面板
    /// </summary>
    public class CopilotAddin : Aveva.ApplicationFramework.Addin
    {
        private CopilotController _controller;
        private WebViewForm _webViewForm;
        private DockedWindow _dockedWindow;

        public override string Name => "E3DCopilot";

        public override string Description =>
            "E3D AI Copilot — 自然语言驱动的 E3D 操作助手";

        public override System.Reflection.Assembly Assembly =>
            System.Reflection.Assembly.GetExecutingAssembly();

        public override bool IsStarted => _isStarted;
        private bool _isStarted;

        public override Aveva.ApplicationFramework.IAddin IAddinInterfaceObject
            => (Aveva.ApplicationFramework.IAddin)this;

        public override void Start()
        {
            try
            {
                _isStarted = true;

                // 1. 创建 ToolRegistry（工具调度器）
                var registry = new ToolRegistry();
                // TODO: 注册具体的 IE3DTool 实现

                // 2. 创建 Controller（传入 registry 作为 dispatcher）
                _controller = CopilotController.CreateDefault(registry);

                // 3. 创建 WebView2 宿主面板
                _webViewForm = new WebViewForm(_controller);

                // 4. 注册 DockedWindow
                _dockedWindow = WindowManager.Instance.CreateDockedWindow(
                    "E3DCopilot",
                    "E小智 Copilot",
                    _webViewForm,
                    DockedPosition.Right
                );

                // 5. 输出启动消息
                var cmd = Aveva.Core.Utilities.CommandLine.Command
                    .CreateCommand("$p E小智 Copilot v1.0 已启动");
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
