using System;
using System.Drawing;
using System.Windows.Forms;
using Aveva.Core.Database;
using Aveva.Core.Utilities.CommandLine;
using E3DCopilot.Core;
using E3DCopilot.Core.Events;
using E3DCopilot.Core.Providers;

namespace E3DCopilot.Addin
{
    /// <summary>
    /// E小智 Copilot 交互面板 — WinForms 实现
    /// 支持 PML 执行、元素查询、子元素浏览 + AI Copilot 模式
    /// </summary>
    public class CopilotPanel : UserControl
    {
        private ComboBox _modeCombo;
        private RichTextBox _outputBox;
        private TextBox _inputBox;
        private Button _executeBtn;
        private Button _newSessionBtn;
        private Label _statusLabel;
        private FlowLayoutPanel _chipPanel;
        private System.Windows.Forms.Timer _chipTimer;

        // AI Copilot 控制器
        private CopilotController _controller;

        private const string MODE_PML = "PML Execute";
        private const string MODE_QUERY = "Query Element";
        private const string MODE_CHILDREN = "List Children";
        private const string MODE_PROPERTIES = "Get Properties";
        private const string MODE_AI = "AI Copilot";

        public CopilotPanel() : this(null)
        {
        }

        /// <summary>
        /// 创建 Copilot 面板
        /// </summary>
        /// <param name="controller">AI Copilot 控制器（可选，不提供时禁用 AI 模式）</param>
        public CopilotPanel(CopilotController controller)
        {
            _controller = controller;
            
            // 使面板可获得焦点（E3D DockedWindow 需要此设置）
            this.SetStyle(ControlStyles.Selectable, true);
            this.TabStop = true;
            
            InitializeComponent();
            WireControllerEvents();
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            FocusInputBox();
        }

        /// <summary>
        /// 强制焦点到输入框（解决 E3D DockedWindow 键盘拦截问题）
        /// </summary>
        private void FocusInputBox()
        {
            if (_inputBox != null && !_inputBox.IsDisposed)
            {
                _inputBox.Select();
                _inputBox.Focus();
            }
        }

        protected override void OnEnter(EventArgs e)
        {
            base.OnEnter(e);
            // 当面板获得焦点时，立即传递给输入框
            FocusInputBox();
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            FocusInputBox();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            // 点击面板空白区域也聚焦到输入框
            FocusInputBox();
        }

        private void InitializeComponent()
        {
            // ============================================================
            // 使用 Dock 布局确保各区域始终可见
            // ============================================================

            // ---- 顶部：模式选择 + 模型切换栏 ----
            var topPanel = new Panel();
            topPanel.Height = 60;
            topPanel.Dock = DockStyle.Top;
            topPanel.BackColor = Color.FromArgb(55, 55, 58);
            topPanel.Padding = new Padding(6, 4, 6, 2);

            // 第1行：Mode
            var modeLabel = new Label();
            modeLabel.Text = "Mode:";
            modeLabel.Location = new Point(6, 5);
            modeLabel.Size = new Size(40, 22);

            _modeCombo = new ComboBox();
            _modeCombo.Location = new Point(48, 4);
            _modeCombo.Size = new Size(120, 24);
            _modeCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            _modeCombo.Items.AddRange(new object[] {
                MODE_PML, MODE_QUERY, MODE_CHILDREN, MODE_PROPERTIES, MODE_AI
            });
            _modeCombo.SelectedIndex = 0;
            _modeCombo.SelectedIndexChanged += OnModeChanged;
            topPanel.Controls.AddRange(new Control[] { modeLabel, _modeCombo });

            // 第2行：模型选择
            var modelLabel = new Label();
            modelLabel.Text = "Model:";
            modelLabel.Location = new Point(6, 32);
            modelLabel.Size = new Size(40, 22);

            var modelCombo = new ComboBox();
            modelCombo.Name = "_modelCombo";
            modelCombo.Location = new Point(48, 31);
            modelCombo.Size = new Size(130, 24);
            modelCombo.DropDownStyle = ComboBoxStyle.DropDownList;
            modelCombo.SelectedIndexChanged += (s, e) => OnModelChanged(modelCombo);
            topPanel.Controls.AddRange(new Control[] { modelLabel, modelCombo });

            // 新会话按钮（右上）
            _newSessionBtn = new Button();
            _newSessionBtn.Text = "新会话";
            _newSessionBtn.Location = new Point(186, 3);
            _newSessionBtn.Size = new Size(60, 24);
            _newSessionBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _newSessionBtn.UseVisualStyleBackColor = true;
            _newSessionBtn.Click += OnNewSessionClick;
            topPanel.Controls.Add(_newSessionBtn);
            
            // 填充模型列表（启动后异步加载，避免构造函数阻塞）
            this.Load += (s, e) => PopulateModelList(modelCombo);

            // ---- 中部：输出区域（Dock=Fill，自动填充剩余空间）----
            _outputBox = new RichTextBox();
            _outputBox.Dock = DockStyle.Fill;
            _outputBox.ReadOnly = true;
            _outputBox.BackColor = Color.FromArgb(30, 30, 30);
            _outputBox.ForeColor = Color.FromArgb(220, 220, 220);
            _outputBox.Font = new Font("Consolas", 9.5f);
            _outputBox.WordWrap = true;
            _outputBox.BorderStyle = BorderStyle.None;
            _outputBox.Padding = new Padding(4);
            _outputBox.ShortcutsEnabled = true;

            // ---- 输出框右键菜单：复制 + 清空 ----
            var outputMenu = new ContextMenuStrip();
            outputMenu.Items.Add("复制", null, (s, e) => { if (_outputBox.SelectedText.Length > 0) Clipboard.SetText(_outputBox.SelectedText); else Clipboard.SetText(_outputBox.Text); });
            outputMenu.Items.Add("全选", null, (s, e) => _outputBox.SelectAll());
            outputMenu.Items.Add(new ToolStripSeparator());
            outputMenu.Items.Add("清空输出", null, (s, e) => _outputBox.Clear());
            _outputBox.ContextMenuStrip = outputMenu;

            // ---- 底部：输入区域（Dock=Bottom，始终可见）----
            var bottomPanel = new Panel();
            bottomPanel.Height = 70;
            bottomPanel.Dock = DockStyle.Bottom;
            bottomPanel.BackColor = Color.FromArgb(50, 50, 53);
            bottomPanel.Padding = new Padding(6);

            _statusLabel = new Label();
            _statusLabel.Text = "输入内容后按 Enter 发送 (Shift+Enter 换行)";
            _statusLabel.Location = new Point(6, 2);
            _statusLabel.Size = new Size(360, 18);
            _statusLabel.ForeColor = Color.LightGray;

            _inputBox = new TextBox();
            _inputBox.Location = new Point(6, 24);
            _inputBox.Size = new Size(300, 24);
            _inputBox.Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top;
            _inputBox.Font = new Font("Consolas", 10f);
            _inputBox.BackColor = Color.FromArgb(60, 60, 63);
            _inputBox.ForeColor = Color.White;
            _inputBox.BorderStyle = BorderStyle.FixedSingle;
            _inputBox.KeyDown += OnInputKeyDown;

            _executeBtn = new Button();
            _executeBtn.Text = "Execute";
            _executeBtn.Location = new Point(314, 23);
            _executeBtn.Size = new Size(70, 26);
            _executeBtn.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _executeBtn.UseVisualStyleBackColor = true;
            _executeBtn.Click += OnExecuteClick;

            bottomPanel.Controls.AddRange(new Control[] {
                _statusLabel, _inputBox, _executeBtn
            });

            // ---- 元素标签面板（显示当前多选元素）----
            _chipPanel = new FlowLayoutPanel();
            _chipPanel.Height = 28;
            _chipPanel.Dock = DockStyle.Bottom;
            _chipPanel.BackColor = Color.FromArgb(45, 45, 48);
            _chipPanel.Padding = new Padding(4, 3, 4, 0);
            _chipPanel.WrapContents = false;
            _chipPanel.AutoScroll = true;

            // 定时器刷新选中元素显示
            _chipTimer = new System.Windows.Forms.Timer { Interval = 800 };
            _chipTimer.Tick += (s, ev) => RefreshElementChips();
            _chipTimer.Start();

            // ---- 装盘 ----
            this.Controls.AddRange(new Control[] {
                _outputBox, bottomPanel, _chipPanel, topPanel
            });

            // ---- UserControl settings ----
            this.BackColor = Color.FromArgb(45, 45, 48);
            this.ForeColor = Color.White;
            this.Padding = new Padding(0);
            
            // 点击空白区域聚焦到输入框
            this.Click += (s, e) => FocusInputBox();
            _outputBox.Click += (s, e) => FocusInputBox();
        }

        private void OnModeChanged(object sender, EventArgs e)
        {
            string mode = _modeCombo.SelectedItem as string;
            switch (mode)
            {
                case MODE_PML:
                    _statusLabel.Text = "输入 PML 命令，按 Enter 执行";
                    _inputBox.Text = "";
                    break;
                case MODE_QUERY:
                    _statusLabel.Text = "输入元素路径或 DB URI（如 /TEST111）";
                    _inputBox.Text = "/TEST111";
                    break;
                case MODE_CHILDREN:
                    _statusLabel.Text = "输入元素路径，列出其子元素";
                    _inputBox.Text = "/TEST111";
                    break;
                case MODE_PROPERTIES:
                    _statusLabel.Text = "输入元素路径，查看所有属性";
                    _inputBox.Text = "/TEST111";
                    break;
                case MODE_AI:
                    if (_controller == null)
                    {
                        _statusLabel.Text = "AI Copilot not available (no controller)";
                        _inputBox.Text = "";
                    }
                    else
                    {
                        _statusLabel.Text = "输入自然语言，按 Enter 发送 (Shift+Enter 换行)";
                        _inputBox.Text = "查询这个元素的属性";
                        // 切换 AI 模式时自动清空历史
                        NewSession();
                    }
                    break;
            }
        }

        /// <summary>
        /// 新会话按钮
        /// </summary>
        private void OnNewSessionClick(object sender, EventArgs e)
        {
            NewSession();
            // 在输出框显示提示
            AppendOutput(Color.Cyan, "── 新会话已开始 ──\n");
            FocusInputBox();
        }

        /// <summary>
        /// 重置会话（清空对话历史）
        /// </summary>
        private void NewSession()
        {
            _controller?.NewSession();
        }

        // ================================================================
        // 模型切换
        // ================================================================

        /// <summary>
        /// 填充模型列表（从 CopilotConfig 读取所有 Provider.Model 组合）
        /// </summary>
        private void PopulateModelList(ComboBox modelCombo)
        {
            try
            {
                modelCombo.Items.Clear();

                var config = _controller?.Config;
                if (config == null || config.Providers == null || config.Providers.Count == 0)
                {
                    modelCombo.Items.Add("(无 Provider)");
                    modelCombo.SelectedIndex = 0;
                    return;
                }

                string defaultRef = config.DefaultModel ?? "";
                int selectedIndex = 0;

                foreach (var provider in config.Providers)
                {
                    foreach (var model in provider.Models)
                    {
                        string display = $"{provider.Name}/{model}";
                        modelCombo.Items.Add(display);

                        // 匹配当前默认模型
                        string fullRef = $"{provider.Name}/{model}";
                        if (fullRef.Equals(defaultRef, StringComparison.OrdinalIgnoreCase)
                            || model.Equals(defaultRef, StringComparison.OrdinalIgnoreCase))
                        {
                            selectedIndex = modelCombo.Items.Count - 1;
                        }
                    }
                }

                if (modelCombo.Items.Count > 0)
                {
                    modelCombo.SelectedIndex = selectedIndex;
                }
            }
            catch (Exception ex)
            {
                modelCombo.Items.Clear();
                modelCombo.Items.Add($"加载失败: {ex.Message}");
                modelCombo.SelectedIndex = 0;
                System.Diagnostics.Debug.WriteLine("[CopilotPanel] PopulateModelList error: " + ex);
            }
        }

        /// <summary>
        /// 切换模型 — 创建新 Provider 并注入 Controller
        /// </summary>
        private void OnModelChanged(ComboBox modelCombo)
        {
            if (_controller == null || modelCombo.SelectedItem == null) return;
            if (_controller.IsRunning)
            {
                AppendOutput(Color.Yellow, "请等待当前任务完成后再切换模型\n");
                return;
            }

            string selected = modelCombo.SelectedItem.ToString();
            var config = _controller.Config;

            try
            {
                // 解析 provider/model
                var (providerConfig, modelName) = config.ResolveModel(selected);
                if (providerConfig == null)
                {
                    AppendOutput(Color.IndianRed, $"未找到 Provider: {selected}\n");
                    return;
                }

                // 创建新 Provider 实例
                var newProvider = new VllmProvider(
                    providerConfig.BaseUrl,
                    modelName,
                    providerConfig.ApiKey
                );

                // 切换
                _controller.SwitchProvider(newProvider, selected);

                AppendOutput(Color.LightGreen,
                    $"🔄 已切换到 {providerConfig.Name}/{modelName}\n");
            }
            catch (Exception ex)
            {
                AppendOutput(Color.IndianRed, $"切换模型失败: {ex.Message}\n");
            }
        }

        private void OnInputKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && !e.Shift)
            {
                // Enter → 发送（Shift+Enter 换行）
                e.SuppressKeyPress = true;
                OnExecuteClick(sender, e);
            }
            else if (e.KeyCode == Keys.Escape)
            {
                // Esc → 终止当前 AI 任务
                e.SuppressKeyPress = true;
                CancelCurrentTask();
            }
        }

        private void OnExecuteClick(object sender, EventArgs e)
        {
            string mode = _modeCombo.SelectedItem as string;
            string input = _inputBox.Text.Trim();
            if (string.IsNullOrEmpty(input))
            {
                AppendOutput(Color.Yellow, "Please enter input first.\n");
                return;
            }

            _executeBtn.Enabled = false;
            try
            {
                switch (mode)
                {
                    case MODE_PML:
                        ExecutePml(input);
                        break;
                    case MODE_QUERY:
                        QueryElement(input);
                        break;
                    case MODE_CHILDREN:
                        ListChildren(input);
                        break;
                    case MODE_PROPERTIES:
                        GetProperties(input);
                        break;
                    case MODE_AI:
                        ExecuteAiAsync(input);
                        break;
                }
            }
            catch (Exception ex)
            {
                AppendOutput(Color.IndianRed,
                    "Error: " + ex.Message + "\n");
            }
            finally
            {
                _executeBtn.Enabled = true;
                _inputBox.SelectAll();
                _inputBox.Focus();
            }
        }

        // ----------------------------------------------------------------
        // AI Copilot — 通过自然语言操作 E3D
        // ----------------------------------------------------------------
        private async void ExecuteAiAsync(string input)
        {
            if (_controller == null || _controller.IsRunning)
            {
                AppendOutput(Color.Yellow, "Controller busy or unavailable.\n");
                return;
            }

            AppendOutput(Color.CornflowerBlue,
                $">> 用户: {input}\n");
            AppendOutput(Color.Gray,
                $">> AI 处理中... (按 Esc 可终止)\n\n");

            try
            {
                await _controller.SendAsync(input);
            }
            catch (Exception ex)
            {
                AppendOutput(Color.IndianRed,
                    $"AI Error: {ex.Message}\n");
            }

            AppendOutput(Color.Gray,
                "\n>> AI 响应结束\n");
        }

        /// <summary>
        /// 终止当前 AI 任务
        /// </summary>
        private void CancelCurrentTask()
        {
            if (_controller != null && _controller.IsRunning)
            {
                _controller.Cancel();
                AppendOutput(Color.Yellow, "\n🛑 已终止当前任务\n");
            }
            else
            {
                // 没有运行任务时，Esc 清空输入框
                _inputBox.Clear();
            }
        }

        /// <summary>
        /// 订阅 Controller 事件 → 输出到面板
        /// </summary>
        private void WireControllerEvents()
        {
            if (_controller == null) return;

            _controller.OnEvent += evt =>
            {
                // WinForms 线程安全：Invoke 到 UI 线程
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action(() =>
                        HandleControllerEvent(evt)));
                    return;
                }
                HandleControllerEvent(evt);
            };
        }

        private void HandleControllerEvent(CopilotEvent evt)
        {
            switch (evt.Kind)
            {
                case EventKind.Text:
                case EventKind.StreamDelta:
                    AppendOutput(Color.LightGreen, evt.Text);
                    break;

                case EventKind.Thinking:
                    AppendOutput(Color.Yellow, evt.Text);
                    break;

                case EventKind.ToolDispatch:
                    AppendOutput(Color.Magenta, $"\n🛠 {evt.Text}\n");
                    break;

                case EventKind.ToolResult:
                    AppendOutput(Color.LightGreen, $"✅ {evt.Text}\n");
                    break;

                case EventKind.ToolError:
                    AppendOutput(Color.IndianRed, $"❌ {evt.Text}\n");
                    break;

                case EventKind.Error:
                    AppendOutput(Color.IndianRed, $"\n❌ 错误: {evt.Text}\n");
                    break;

                case EventKind.Notice:
                    AppendOutput(Color.Gray, $"\n📝 {evt.Text}\n");
                    break;

                case EventKind.ApprovalRequest:
                    AppendOutput(Color.Yellow, $"\n⚡ 需要审批: {evt.Text}\n");
                    // 在测试面板中自动批准
                    _controller?.Approve(evt.ToolId, true);
                    AppendOutput(Color.Gray, "   (已自动批准)\n");
                    break;

                case EventKind.TurnDone:
                    AppendOutput(Color.Cyan, "\n── 本轮结束 ──\n");
                    break;
            }
        }

        // ----------------------------------------------------------------
        // PML Execution
        // ----------------------------------------------------------------
        private void ExecutePml(string pml)
        {
            AppendOutput(Color.CornflowerBlue,
                ">> PML: " + pml + "\n");

            var cmd = Command.CreateCommand(pml);
            bool ok = cmd.RunInPdms();

            if (ok)
                AppendOutput(Color.LightGreen,
                    ">> PML executed successfully.\n");
            else
                AppendOutput(Color.IndianRed,
                    ">> PML execution failed.\n");
        }

        // ----------------------------------------------------------------
        // Query Element by DB URI
        // ----------------------------------------------------------------
        private void QueryElement(string dbUri)
        {
            DbElement elem = DbElement.GetElement(dbUri);
            if (elem == null || !elem.IsValid)
            {
                AppendOutput(Color.IndianRed,
                    "Element not found: " + dbUri + "\n");
                return;
            }

            AppendOutput(Color.CornflowerBlue,
                "=== Element: " + dbUri + " ===\n");

            // Common attributes to display
            string[] attrs = { "NAME", "TYPE", "PURP", "OWNER",
                "DESC", "SPRE", "SREF", "WTHK", "LENG", "BORE" };

            foreach (string attrName in attrs)
            {
                try
                {
                    DbAttribute attr = DbAttribute.GetDbAttribute(attrName);
                    string val = elem.GetAsString(attr);
                    if (!string.IsNullOrEmpty(val))
                    {
                        AppendOutput(Color.White,
                            "  " + attrName + " = " + val + "\n");
                    }
                }
                catch { /* attribute not applicable */ }
            }

            // Count children
            try
            {
                DbElement[] kids = elem.Members();
                AppendOutput(Color.Gray,
                    "  -- " + kids.Length + " child element(s)\n");
            }
            catch { }
        }

        // ----------------------------------------------------------------
        // List Children
        // ----------------------------------------------------------------
        private void ListChildren(string dbUri)
        {
            DbElement elem = DbElement.GetElement(dbUri);
            if (elem == null || !elem.IsValid)
            {
                AppendOutput(Color.IndianRed,
                    "Element not found: " + dbUri + "\n");
                return;
            }

            AppendOutput(Color.CornflowerBlue,
                "=== Children of: " + dbUri + " ===\n");

            DbElement[] kids = elem.Members();
            if (kids.Length == 0)
            {
                AppendOutput(Color.Gray, "  (no children)\n");
                return;
            }

            foreach (DbElement kid in kids)
            {
                try
                {
                    string name = kid.GetAsString(
                        DbAttribute.GetDbAttribute("NAME"));
                    string type = kid.GetAsString(
                        DbAttribute.GetDbAttribute("TYPE"));
                    AppendOutput(Color.White,
                        "  [" + type + "]  " + name + "\n");
                }
                catch
                {
                    AppendOutput(Color.Gray, "  (unknown)\n");
                }
            }

            AppendOutput(Color.Gray,
                "  -- " + kids.Length + " total\n");
        }

        // ----------------------------------------------------------------
        // Get All Properties
        // ----------------------------------------------------------------
        private void GetProperties(string dbUri)
        {
            DbElement elem = DbElement.GetElement(dbUri);
            if (elem == null || !elem.IsValid)
            {
                AppendOutput(Color.IndianRed,
                    "Element not found: " + dbUri + "\n");
                return;
            }

            AppendOutput(Color.CornflowerBlue,
                "=== Properties of: " + dbUri + " ===\n");

            // Note: E3D doesn't expose a "get all attributes" API easily.
            // We query a comprehensive set of common attributes.
            string[] allAttrs = {
                "NAME", "TYPE", "PURP", "OWNER", "DESC",
                "SPRE", "SREF", "WTHK", "LENG", "BORE",
                "HEIG", "WIDT", "DPTH", "VOLU", "AREA",
                "POSX", "POSY", "POSZ", "DIRX", "DIRY", "DIRZ",
                "PREF", "PSPC", "PCOM", "PASH", "PDES",
                "SPEC", "SCOM", "SASH", "SDES",
                "ACTP", "ACTU", "ACTQ", "ACTD",
                "CREF", "CSPR", "CDES", "CCOM",
                "DENS", "TEMP", "PRES", "PIPE", "BRAN",
                "CBOR", "CWTH", "CDIA", "CRAD",
                "FREF", "FDES", "FCOM"
            };

            int count = 0;
            foreach (string attrName in allAttrs)
            {
                try
                {
                    DbAttribute attr = DbAttribute.GetDbAttribute(attrName);
                    string val = elem.GetAsString(attr);
                    if (!string.IsNullOrEmpty(val))
                    {
                        AppendOutput(Color.White,
                            "  " + attrName.PadRight(6) + " = " + val + "\n");
                        count++;
                    }
                }
                catch { }
            }

            if (count == 0)
            {
                AppendOutput(Color.Gray,
                    "  (no readable attributes on this element)\n");
            }
            else
            {
                AppendOutput(Color.Gray,
                    "  -- " + count + " attributes found\n");
            }
        }

        // ----------------------------------------------------------------
        // Output helper
        // ----------------------------------------------------------------
        private void AppendOutput(Color color, string text)
        {
            if (_outputBox.InvokeRequired)
            {
                _outputBox.Invoke(new Action(() =>
                    AppendOutput(color, text)));
                return;
            }

            _outputBox.SelectionStart = _outputBox.TextLength;
            _outputBox.SelectionLength = 0;
            _outputBox.SelectionColor = color;
            _outputBox.AppendText(text);
            _outputBox.SelectionStart = _outputBox.TextLength;
            _outputBox.ScrollToCaret();
        }

        // ----------------------------------------------------------------
        // 元素标签刷新
        // ----------------------------------------------------------------
        private string _lastChipElements = "";

        private void RefreshElementChips()
        {
            if (_chipPanel == null || _controller == null) return;

            try
            {
                var names = _controller.Executor?.GetSelectedElementNames();
                string current = names != null && names.Count > 0
                    ? string.Join(",", names)
                    : "";

                // 避免无意义的重绘
                if (current == _lastChipElements) return;
                _lastChipElements = current;

                _chipPanel.Controls.Clear();

                if (names == null || names.Count == 0)
                {
                    var emptyLabel = new Label
                    {
                        Text = "📌 在 E3D 中 Ctrl+点击多选元素",
                        AutoSize = true,
                        ForeColor = Color.Gray,
                        Font = new Font("Segoe UI", 8f)
                    };
                    _chipPanel.Controls.Add(emptyLabel);
                }
                else
                {
                    foreach (string name in names)
                    {
                        var chip = new Panel
                        {
                            Height = 22,
                            Padding = new Padding(6, 0, 6, 0),
                            BackColor = Color.FromArgb(70, 130, 180),
                            Margin = new Padding(2, 1, 2, 1)
                        };

                        var label = new Label
                        {
                            Text = name.Length > 30 ? name.Substring(0, 30) + "..." : name,
                            AutoSize = true,
                            ForeColor = Color.White,
                            Font = new Font("Consolas", 8.5f),
                            TextAlign = ContentAlignment.MiddleCenter
                        };

                        // 计算宽度
                        using (var g = label.CreateGraphics())
                            chip.Width = Math.Max(40, (int)g.MeasureString(label.Text, label.Font).Width + 14);

                        label.Location = new Point(0, 3);
                        chip.Controls.Add(label);
                        _chipPanel.Controls.Add(chip);
                    }
                }
            }
            catch
            {
                // 忽略刷新错误
            }
        }
    }
}
