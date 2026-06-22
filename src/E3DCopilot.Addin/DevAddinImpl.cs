using System;
using System.Windows.Forms;
using E3DCopilot.Core;
using E3DCopilot.Loader;
using E3DCopilot.Tools.Bridge;

namespace E3DCopilot.Addin
{
    /// <summary>
    /// 开发插件实现 — 由 Loader 通过反射创建
    /// 负责创建 CopilotController + CopilotPanel
    /// </summary>
    public class DevAddinImpl : DevAddinBase
    {
        private CopilotController _controller;
        private CopilotPanel _panel;

        /// <summary>
        /// 创建 Copilot 面板（在子域中执行）
        /// </summary>
        public override Control CreatePanel()
        {
            try
            {
                // 1. 创建 E3D 真实环境
                var env = new RealE3DEnvironment();
                var dispatcher = new E3DToolDispatcher(env);

                // 2. 创建 Controller
                _controller = CopilotController.CreateDefault(dispatcher);

                // 3. 创建 Copilot 面板
                _panel = new CopilotPanel(_controller);

                return _panel;
            }
            catch (Exception ex)
            {
                return new Label
                {
                    Text = "❌ 创建失败: " + ex.Message + "\n\n" + ex.ToString(),
                    ForeColor = System.Drawing.Color.IndianRed,
                    BackColor = System.Drawing.Color.FromArgb(30, 30, 30),
                    Dock = DockStyle.Fill,
                    Padding = new Padding(8)
                };
            }
        }

        /// <summary>
        /// 销毁面板和控制器
        /// </summary>
        public override void Destroy()
        {
            try { _panel?.Dispose(); } catch { }
            _panel = null;

            try { _controller?.Dispose(); } catch { }
            _controller = null;
        }
    }
}
