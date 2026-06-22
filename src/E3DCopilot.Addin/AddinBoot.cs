using System;
using Aveva.ApplicationFramework;
using Aveva.ApplicationFramework.Presentation;
using E3DCopilot.Core;
using E3DCopilot.Tools.Bridge;

namespace E3DCopilot.Addin
{
    /// <summary>
    /// E小智 Addin — E3D AI Copilot 入口
    /// 创建 WinForms CopilotPanel + CopilotController + E3D 环境
    /// 使用简单 WinForms 界面（不依赖 WebView2）
    /// </summary>
    public class CopilotAddin : IAddin
    {
        private DockedWindow _dockedWindow;
        private CopilotController _controller;
        private CopilotPanel _copilotPanel;

        public string Name => "E3DCopilot";
        public string Description => "E小智 AI Copilot for E3D";

        public void Start(ServiceManager serviceManager)
        {
            try
            {
                // 1. 创建 E3D 真实环境
                var env = new RealE3DEnvironment();
                var dispatcher = new E3DToolDispatcher(env);

                // 2. 创建 Controller
                _controller = CopilotController.CreateDefault(dispatcher);

                // 3. 创建 WinForms Copilot 面板
                _copilotPanel = new CopilotPanel(_controller);

                // 4. 注册 DockedWindow
                _dockedWindow = WindowManager.Instance.CreateDockedWindow(
                    "E3DCopilot",
                    "E小智",
                    _copilotPanel,
                    DockedPosition.Right
                );
                _dockedWindow.Width = 520;
                _dockedWindow.Show();

                var cmd = Aveva.Core.Utilities.CommandLine.Command.CreateCommand(
                    "$p E小智 Copilot v1.0 已启动 (WinForms Panel)");
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
            _copilotPanel = null;
        }
    }
}
