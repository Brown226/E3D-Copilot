using System;
using System.Windows.Forms;
using E3DCopilot.Core;
using E3DCopilot.Core.Tools;
using E3DCopilot.Loader;
using E3DCopilot.Tools.Bridge;
using E3DCopilot.Tools.Registry;

namespace E3DCopilot.Addin
{
    /// <summary>
    /// 开发插件实现 — 由 Loader 通过反射创建
    /// 优先创建 WebViewForm（React 前端），WebView2 不可用时降级到 CopilotPanel（WinForms）
    /// </summary>
    public class DevAddinImpl : DevAddinBase
    {
        private CopilotController _controller;
        private Control _panel;

        /// <summary>
        /// 创建面板（在子域中执行）
        /// </summary>
        public override Control CreatePanel()
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
                _panel = CreatePanelWithFallback();

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
            try { (_panel as IDisposable)?.Dispose(); } catch { }
            _panel = null;

            try { _controller?.Dispose(); } catch { }
            _controller = null;
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

                System.Diagnostics.Debug.WriteLine("[DevAddinImpl] Using WebView2 + React UI");
                return webViewForm;
            }
            catch (Exception ex)
            {
                // WebView2 Runtime 未安装或初始化失败，降级到 WinForms
                System.Diagnostics.Debug.WriteLine("[DevAddinImpl] WebView2 unavailable, falling back to WinForms: " + ex.Message);

                return new CopilotPanel(_controller);
            }
        }
    }
}
