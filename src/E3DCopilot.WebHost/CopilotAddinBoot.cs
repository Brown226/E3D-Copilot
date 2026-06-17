using System;
using System.Reflection;
using System.Windows.Forms;
using Aveva.ApplicationFramework;
using Aveva.ApplicationFramework.Presentation;
using E3DCopilot.Core;
using E3DCopilot.Tools.Registry;

namespace E3DCopilot.WebHost
{
    /// <summary>
    /// E小智 E3D AI Copilot — Addin 入口
    /// 在 E3D 侧边栏创建 DockedWindow 并嵌入 WebView2
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

                // 1. 创建 ToolRegistry（工具调度器）
                var registry = new ToolRegistry();
                // TODO: 注册具体的 IE3DTool 实现

                // 2. 创建 Controller（传入 registry 作为 dispatcher）
                _controller = CopilotController.CreateDefault(registry);

                // 3. 创建 WebView2 宿主面板
                _webViewForm = new WebViewForm(_controller);

                // 4. 注册 DockedWindow（4 参数，含 DockedPosition）
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
