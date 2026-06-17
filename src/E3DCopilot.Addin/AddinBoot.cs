using System;
using Aveva.ApplicationFramework;
using Aveva.ApplicationFramework.Presentation;
using E3DCopilot.Core;
using E3DCopilot.UI.Forms;

namespace E3DCopilot.Addin
{
    /// <summary>
    /// E小智 E3D AI Copilot 插件入口
    /// 继承 Aveva.ApplicationFramework.Addin
    /// 验证过的 API：CreateDockedWindow 4 参 + Command.CreateCommand().RunInPdms()
    /// </summary>
    public class CopilotAddin : Aveva.ApplicationFramework.Addin
    {
        private CopilotController _controller;
        private CopilotForm _copilotForm;
        private DockedWindow _dockedWindow;

        /// <summary>
        /// 插件名称
        /// </summary>
        public override string Name => "E3DCopilot";

        /// <summary>
        /// 插件描述
        /// </summary>
        public override string Description =>
            "E3D AI Copilot — 自然语言驱动的 E3D 操作助手";

        /// <summary>程序集引用（Addin 抽象属性）</summary>
        public override System.Reflection.Assembly Assembly =>
            System.Reflection.Assembly.GetExecutingAssembly();

        /// <summary>是否已启动（Addin 抽象属性）</summary>
        public override bool IsStarted => _isStarted;
        private bool _isStarted;

        /// <summary>IAddin 接口对象（Addin 抽象属性）</summary>
        public override Aveva.ApplicationFramework.IAddin IAddinInterfaceObject
            => (Aveva.ApplicationFramework.IAddin)this;

        /// <summary>
        /// 插件启动（E3D 加载时调用）
        /// </summary>
        public override void Start()
        {
            try
            {
                _isStarted = true;

                // 1. 创建 Controller（默认配置：确认模式）
                // UI 通过 _controller.OnEvent 订阅事件
                _controller = CopilotController.CreateDefault();

                // 3. 创建 UI 主面板
                _copilotForm = new CopilotForm(_controller);

                // 4. 注册 DockedWindow（4 参数，含 DockedPosition）
                // 验证过的 API：WindowManager.Instance.CreateDockedWindow()
                _dockedWindow = WindowManager.Instance.CreateDockedWindow(
                    "E3DCopilot",           // 窗口 Key
                    "E小智 Copilot",         // 窗口标题
                    _copilotForm,           // UserControl
                    DockedPosition.Right     // 停靠位置
                );

                // 5. 注册工具栏按钮
                RegisterToolbarButton();

                // 6. 输出启动消息（验证过的 API：Command.CreateCommand）
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

        /// <summary>
        /// 插件停止（E3D 卸载时调用）
        /// </summary>
        public override void Stop()
        {
            try
            {
                _isStarted = false;

                // 1. 释放 Controller
                if (_controller != null)
                {
                    _controller.Dispose();
                    _controller = null;
                }

                // 2. 关闭窗口
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

        /// <summary>
        /// 注册 E3D 工具栏按钮
        /// Phase 1b: 通过 CommandManager 注册
        /// </summary>
        private void RegisterToolbarButton()
        {
            // Phase 1b: 使用 DependencyResolver 获取 ICommandManager
            // var cmdManager = DependencyResolver.GetImplementationOf<ICommandManager>();
            // var command = new Command("E3DCopilot", "E小智 Copilot", "显示/隐藏 Copilot 面板");
            // command.Execute += (s, e) => { _dockedWindow?.Show(); };
            // cmdManager.Commands.Add(command);
        }
    }

}
