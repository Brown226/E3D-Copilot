using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;
using E3DCopilot.Core;
using E3DCopilot.Core.Events;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace E3DCopilot.WebHost
{
    /// <summary>
    /// WebView2 宿主面板 — 嵌入 E3D DockedWindow
    /// 加载 React 前端并通过 bridge 与 CopilotController 通信
    /// </summary>
    public class WebViewForm : UserControl
    {
        private readonly CopilotController _controller;
        private WebView2 _webView;
        private Bridge _bridge;
        private readonly CopilotEventDispatcher _eventDispatcher;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public WebViewForm(CopilotController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            _eventDispatcher = new CopilotEventDispatcher(this, OnCopilotEvent);
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Dock = DockStyle.Fill;
            this.MinimumSize = new Size(600, 400);
            this.BackColor = Color.FromArgb(12, 12, 20);

            // 加载占位面板（WebView2 初始化前显示）
            var loadingPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(12, 12, 20)
            };
            var loadingLabel = new Label
            {
                Text = "E小智 启动中...",
                ForeColor = Color.FromArgb(160, 160, 180),
                Font = new Font("Segoe UI", 12f),
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                AutoSize = false
            };
            loadingPanel.Controls.Add(loadingLabel);
            this.Controls.Add(loadingPanel);

            // 异步初始化 WebView2
            _ = InitWebView2Async(loadingPanel);
        }

        private async Task InitWebView2Async(Panel loadingPanel)
        {
            try
            {
                // 用户数据目录
                var userDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "E3DCopilot", "WebView2");
                Directory.CreateDirectory(userDataDir);

                // 创建 WebView2 环境
                var env = await CoreWebView2Environment.CreateAsync(
                    null, userDataDir);

                _webView = new WebView2
                {
                    Dock = DockStyle.Fill
                };

                // 在 UI 线程上操作控件
                void addWebView()
                {
                    this.Controls.Add(_webView);
                    _webView.BringToFront();
                }

                if (this.InvokeRequired)
                    this.Invoke((MethodInvoker)addWebView);
                else
                    addWebView();

                await _webView.EnsureCoreWebView2Async(env);

                // 配置 WebView2 设置
                _webView.CoreWebView2.Settings.AreDevToolsEnabled = true; // 临时开启方便调试
                _webView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _webView.CoreWebView2.Settings.IsScriptEnabled = true;

                // 使用虚拟主机名映射，避免 file:// 的 CORS 限制（ES Module 需要）
                var wwwrootDir = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory, "wwwroot");
                if (Directory.Exists(wwwrootDir))
                {
                    _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                        "app.e3dcopilot.local",
                        wwwrootDir,
                        CoreWebView2HostResourceAccessKind.Allow
                    );
                }

                // 创建桥接
                _bridge = new Bridge(_webView, _controller);

                // 监听前端消息
                _webView.CoreWebView2.WebMessageReceived += (s, e) =>
                {
                    var raw = e.TryGetWebMessageAsString();
                    if (!string.IsNullOrEmpty(raw))
                        _bridge.HandleMessage(raw);
                };

                // 导航完成 → 通知前端宿主就绪
                _webView.CoreWebView2.NavigationCompleted += (s, e) =>
                {
                    if (e.IsSuccess)
                    {
                        _bridge.SendToFrontend("host:ready", new
                        {
                            version = Assembly.GetExecutingAssembly()
                                .GetName().Version?.ToString() ?? "1.0.0",
                            platform = "E3D",
                            timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                        });

                        // 连接 Controller 事件（确保 WebView 已加载）
                        _eventDispatcher.ConnectTo(_controller);
                    }
                };

                // 加载前端页面
                // 使用虚拟主机名映射（SetVirtualHostNameToFolderMapping），
                // 避免 file:// 导致 ES Module 加载失败
                var devUrl = Environment.GetEnvironmentVariable("E3D_COPILOT_DEV_URL");
                if (!string.IsNullOrEmpty(devUrl))
                {
                    _webView.CoreWebView2.Navigate(devUrl);
                }
                else
                {
                    // 使用虚拟域名导航，支持 ES Module
                    _webView.CoreWebView2.Navigate(
                        "https://app.e3dcopilot.local/index.html");
                }

                // 移除加载面板
                void removeLoading()
                {
                    if (loadingPanel?.Parent != null)
                        this.Controls.Remove(loadingPanel);
                    loadingPanel?.Dispose();
                }

                if (this.InvokeRequired)
                    this.Invoke((MethodInvoker)removeLoading);
                else
                    removeLoading();
            }
            catch (Exception ex)
            {
                var msg = $"WebView2 初始化失败: {ex.Message}";
                void showError()
                {
                    MessageBox.Show(msg, "E小智", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                if (this.InvokeRequired)
                    this.Invoke((MethodInvoker)showError);
                else
                    showError();
            }
        }

        /// <summary>
        /// 接收 CopilotController 事件并转发到前端
        /// </summary>
        private void OnCopilotEvent(CopilotEvent evt)
        {
            if (_bridge == null || _webView?.CoreWebView2 == null)
                return;

            switch (evt.Kind)
            {
                case EventKind.Text:
                case EventKind.StreamDelta:
                    _bridge.SendToFrontend("llm:stream:delta", new { delta = evt.Text });
                    break;

                case EventKind.StreamEnd:
                    _bridge.SendToFrontend("llm:stream:end", new
                    {
                        usage = evt.Data // 可选的 token 使用统计
                    });
                    break;

                case EventKind.Thinking:
                    _bridge.SendToFrontend("llm:thinking", new { text = evt.Text });
                    break;

                case EventKind.ToolDispatch:
                    _bridge.SendToFrontend("tool:dispatch", new
                    {
                        id = evt.ToolId,
                        name = evt.Text,
                        args = evt.Data
                    });
                    break;

                case EventKind.ToolResult:
                    _bridge.SendToFrontend("tool:result", new
                    {
                        id = evt.ToolId,
                        result = evt.Data?.ToString()
                    });
                    break;

                case EventKind.ToolError:
                    _bridge.SendToFrontend("tool:result", new
                    {
                        id = evt.ToolId,
                        error = evt.Text
                    });
                    break;

                case EventKind.ApprovalRequest:
                    _bridge.SendToFrontend("tool:approval", new
                    {
                        id = evt.ToolId,
                        name = evt.Text,
                        args = evt.Data?.ToString(),
                        description = evt.Text
                    });
                    break;

                case EventKind.Notice:
                    _bridge.SendToFrontend("notice", new { text = evt.Text });
                    break;

                case EventKind.Error:
                    _bridge.SendToFrontend("error", new { message = evt.Text });
                    break;
            }
        }
    }
}
