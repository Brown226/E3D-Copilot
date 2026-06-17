using System;
using System.Drawing;
using System.Windows.Forms;
using E3DCopilot.Core;
using E3DCopilot.Core.Events;
using E3DCopilot.UI.Controls;
using E3DCopilot.UI.Dialogs;
using E3DCopilot.UI.Themes;

namespace E3DCopilot.UI.Forms
{
    /// <summary>
    /// E小智 主面板 — 嵌入 E3D DockedWindow
    /// 功能分区：工具栏 | 聊天区 + 侧边栏 | 输入区 | 状态栏
    /// </summary>
    public class CopilotForm : UserControl
    {
        private readonly CopilotController _controller;

        // ---- 主布局 ----
        private Panel _mainContainer;
        private Panel _leftPanel;   // 聊天区
        private Panel _rightPanel;  // 侧边栏

        // ---- 顶部工具栏 ----
        private ToolStrip _toolbar;
        private ToolStripButton _btnNewSession;
        private ToolStripButton _btnPlanMode;
        private ToolStripButton _btnQuickActions;
        private ToolStripButton _btnSettings;
        private ToolStripButton _btnToggleSidebar;

        // ---- 聊天区 ----
        private ChatListBox _chatList;

        // ---- 侧边栏 ----
        private TabControl _sideTab;
        private Panel _panelElementTree;
        private Panel _panelFavorites;
        private Panel _panelHistory;

        // ---- 输入区 ----
        private Panel _inputPanel;
        private TextBox _inputBox;
        private Button _sendButton;
        private Button _btnVoice;
        private Button _btnClear;

        // ---- 状态栏 ----
        private Panel _statusBar;
        private Label _lblStatus;
        private Label _lblConnection;
        private Label _lblTokens;
        private Label _lblModel;

        // ---- 快速操作面板 ----
        private Panel _quickActionPanel;
        private bool _quickActionVisible;

        public CopilotForm(CopilotController controller)
        {
            _controller = controller ?? throw new ArgumentNullException(nameof(controller));
            this.Dock = DockStyle.Fill;
            this.MinimumSize = new Size(800, 500);
            InitializeComponent();
            SubscribeEvents();
        }

        private void InitializeComponent()
        {
            this.BackColor = CopilotTheme.BgDark;

            // ==================== 顶部工具栏 ====================
            _toolbar = new ToolStrip
            {
                BackColor = CopilotTheme.BgMid,
                ForeColor = CopilotTheme.TextPrimary,
                GripStyle = ToolStripGripStyle.Hidden,
                AutoSize = false,
                Height = 36,
                Padding = new Padding(4, 0, 4, 0),
                ImageScalingSize = new Size(18, 18)
            };

            _btnNewSession = new ToolStripButton("  新会话  ", null, (s, e) => NewSession())
            {
                ForeColor = CopilotTheme.TextPrimary,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Font = CopilotTheme.FontNormal
            };

            _btnPlanMode = new ToolStripButton("  Plan  ", null, (s, e) => TogglePlanMode())
            {
                ForeColor = CopilotTheme.TextSecondary,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Font = CopilotTheme.FontNormal
            };

            _btnQuickActions = new ToolStripButton("  快捷操作  ", null, (s, e) => ToggleQuickActions())
            {
                ForeColor = CopilotTheme.TextSecondary,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Font = CopilotTheme.FontNormal
            };

            _btnSettings = new ToolStripButton("  ⚙  ", null, (s, e) => ShowSettings())
            {
                ForeColor = CopilotTheme.TextSecondary,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Font = CopilotTheme.FontNormal,
                Alignment = ToolStripItemAlignment.Right
            };

            _btnToggleSidebar = new ToolStripButton("  ☰  ", null, (s, e) => ToggleSidebar())
            {
                ForeColor = CopilotTheme.TextSecondary,
                DisplayStyle = ToolStripItemDisplayStyle.Text,
                Font = CopilotTheme.FontNormal,
                Alignment = ToolStripItemAlignment.Right
            };

            _toolbar.Items.Add(_btnNewSession);
            _toolbar.Items.Add(new ToolStripSeparator { ForeColor = CopilotTheme.Border });
            _toolbar.Items.Add(_btnPlanMode);
            _toolbar.Items.Add(_btnQuickActions);
            _toolbar.Items.Add(new ToolStripSeparator { ForeColor = CopilotTheme.Border });
            _toolbar.Items.Add(_btnToggleSidebar);
            _toolbar.Items.Add(_btnSettings);

            // ==================== 主容器 ====================
            _mainContainer = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CopilotTheme.BgDark
            };

            // ==================== 左侧聊天区 ====================
            _leftPanel = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = CopilotTheme.BgDark
            };

            // 聊天消息列表
            _chatList = new ChatListBox
            {
                Dock = DockStyle.Fill,
                BackColor = CopilotTheme.BgDark
            };

            // 快速操作面板（初始隐藏）
            _quickActionPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 160,
                BackColor = CopilotTheme.BgMid,
                Visible = false
            };
            BuildQuickActions();

            // 输入区
            _inputPanel = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 64,
                BackColor = CopilotTheme.BgMid,
                Padding = new Padding(12, 8, 12, 8)
            };
            BuildInputPanel();

            _leftPanel.Controls.Add(_chatList);
            _leftPanel.Controls.Add(_quickActionPanel);
            _leftPanel.Controls.Add(_inputPanel);

            // ==================== 右侧侧边栏 ====================
            _rightPanel = new Panel
            {
                Dock = DockStyle.Right,
                Width = 280,
                BackColor = CopilotTheme.BgMid,
                Visible = true
            };
            BuildSidebar();

            // ==================== 状态栏 ====================
            _statusBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                BackColor = CopilotTheme.BgMid,
                Padding = new Padding(8, 0, 8, 0)
            };
            BuildStatusBar();

            // ==================== 组装布局 ====================
            _mainContainer.Controls.Add(_leftPanel);
            _mainContainer.Controls.Add(_rightPanel);

            this.Controls.Add(_mainContainer);
            this.Controls.Add(_statusBar);
            this.Controls.Add(_toolbar);
        }

        /// <summary>构建输入面板</summary>
        private void BuildInputPanel()
        {
            _inputBox = new TextBox
            {
                Multiline = true,
                Location = new Point(12, 8),
                Size = new Size(_inputPanel.ClientSize.Width - 180, 44),
                BackColor = CopilotTheme.BgLight,
                ForeColor = CopilotTheme.TextPrimary,
                BorderStyle = BorderStyle.FixedSingle,
                Font = CopilotTheme.FontInput,
                MaxLength = 4000,
                WordWrap = true,
                AcceptsReturn = true,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top | AnchorStyles.Bottom
            };
            _inputBox.KeyDown += InputBox_KeyDown;
            _inputBox.TextChanged += (s, e) =>
                _sendButton.Enabled = !string.IsNullOrWhiteSpace(_inputBox.Text);

            _sendButton = new Button
            {
                Text = "发送",
                Location = new Point(_inputPanel.ClientSize.Width - 155, 8),
                Size = new Size(65, 44),
                BackColor = CopilotTheme.AccentBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = CopilotTheme.FontNormal,
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Enabled = false
            };
            _sendButton.Click += SendButton_Click;
            _sendButton.FlatAppearance.BorderSize = 0;

            _btnClear = new Button
            {
                Text = "清空",
                Location = new Point(_inputPanel.ClientSize.Width - 85, 8),
                Size = new Size(65, 20),
                BackColor = CopilotTheme.BgLight,
                ForeColor = CopilotTheme.TextSecondary,
                FlatStyle = FlatStyle.Flat,
                Font = CopilotTheme.FontSmall,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            _btnClear.Click += (s, e) => { _inputBox.Clear(); };

            _btnVoice = new Button
            {
                Text = "🎤",
                Location = new Point(_inputPanel.ClientSize.Width - 85, 30),
                Size = new Size(65, 22),
                BackColor = CopilotTheme.BgLight,
                ForeColor = CopilotTheme.TextSecondary,
                FlatStyle = FlatStyle.Flat,
                Font = CopilotTheme.FontSmall,
                Anchor = AnchorStyles.Right | AnchorStyles.Top,
                Enabled = false
            };
            _btnVoice.Click += (s, e) =>
            {
                _chatList.AddSystemMessage("语音输入功能将在 Phase 3 实现");
            };

            _inputPanel.Controls.Add(_inputBox);
            _inputPanel.Controls.Add(_sendButton);
            _inputPanel.Controls.Add(_btnClear);
            _inputPanel.Controls.Add(_btnVoice);

            this.Resize += (s, e) =>
            {
                _inputBox.Width = _inputPanel.ClientSize.Width - 180;
                _sendButton.Left = _inputPanel.ClientSize.Width - 155;
                _btnClear.Left = _inputPanel.ClientSize.Width - 85;
                _btnVoice.Left = _inputPanel.ClientSize.Width - 85;
            };
        }

        /// <summary>构建侧边栏</summary>
        private void BuildSidebar()
        {
            _sideTab = new TabControl
            {
                Dock = DockStyle.Fill,
                BackColor = CopilotTheme.BgMid,
                ForeColor = CopilotTheme.TextPrimary,
                Font = CopilotTheme.FontSmall,
                Padding = new Point(6, 4)
            };

            // Tab 1: 元素信息
            _panelElementTree = new Panel
            {
                BackColor = CopilotTheme.BgMid,
                Padding = new Padding(8)
            };
            BuildElementTreePanel();

            // Tab 2: 收藏夹
            _panelFavorites = new Panel
            {
                BackColor = CopilotTheme.BgMid,
                Padding = new Padding(8)
            };
            BuildFavoritesPanel();

            // Tab 3: 历史
            _panelHistory = new Panel
            {
                BackColor = CopilotTheme.BgMid,
                Padding = new Padding(8)
            };
            BuildHistoryPanel();

            _sideTab.TabPages.Add(new TabPage("元素") { Controls = { _panelElementTree } });
            _sideTab.TabPages.Add(new TabPage("收藏") { Controls = { _panelFavorites } });
            _sideTab.TabPages.Add(new TabPage("历史") { Controls = { _panelHistory } });

            _rightPanel.Controls.Add(_sideTab);
        }

        /// <summary>构建元素树面板</summary>
        private void BuildElementTreePanel()
        {
            var title = new Label
            {
                Text = "当前元素",
                Font = CopilotTheme.FontTitle,
                ForeColor = CopilotTheme.TextPrimary,
                AutoSize = true
            };

            var elemName = new Label
            {
                Text = "PIPE-001",
                Font = CopilotTheme.FontNormal,
                ForeColor = CopilotTheme.AccentBlue,
                AutoSize = true,
                Location = new Point(0, 24)
            };

            var elemType = new Label
            {
                Text = "类型: PIPE  |  状态: 已修改",
                Font = CopilotTheme.FontSmall,
                ForeColor = CopilotTheme.TextSecondary,
                AutoSize = true,
                Location = new Point(0, 46)
            };

            // 属性快速查看
            var attrTitle = new Label
            {
                Text = "常用属性",
                Font = CopilotTheme.FontTitle,
                ForeColor = CopilotTheme.TextPrimary,
                AutoSize = true,
                Location = new Point(0, 72)
            };

            string[] attrs = {
                "名称: PIPE-001",
                "壁厚: SCH40 (6.02mm)",
                "直径: DN100 (114.3mm)",
                "材质: A106 Gr.B",
                "等级: 3AS1",
                "温度: 350°C",
                "压力: 2.5MPa"
            };

            var attrList = new ListBox
            {
                Location = new Point(0, 96),
                Size = new Size(_rightPanel.Width - 16, 180),
                BackColor = CopilotTheme.BgLight,
                ForeColor = CopilotTheme.TextPrimary,
                Font = CopilotTheme.FontSmall,
                BorderStyle = BorderStyle.None,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            foreach (var a in attrs) attrList.Items.Add(a);

            // 子元素快速列表
            var childTitle = new Label
            {
                Text = "子元素 (3)",
                Font = CopilotTheme.FontTitle,
                ForeColor = CopilotTheme.TextPrimary,
                AutoSize = true,
                Location = new Point(0, 286)
            };

            var childList = new ListBox
            {
                Location = new Point(0, 310),
                Size = new Size(_rightPanel.Width - 16, 80),
                BackColor = CopilotTheme.BgLight,
                ForeColor = CopilotTheme.TextSecondary,
                Font = CopilotTheme.FontSmall,
                BorderStyle = BorderStyle.None,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            childList.Items.Add("BRAN-001  (主管)");
            childList.Items.Add("BRAN-002  (支管)");
            childList.Items.Add("BRAN-003  (排放)");

            _panelElementTree.Controls.Add(title);
            _panelElementTree.Controls.Add(elemName);
            _panelElementTree.Controls.Add(elemType);
            _panelElementTree.Controls.Add(attrTitle);
            _panelElementTree.Controls.Add(attrList);
            _panelElementTree.Controls.Add(childTitle);
            _panelElementTree.Controls.Add(childList);
        }

        /// <summary>构建收藏夹面板</summary>
        private void BuildFavoritesPanel()
        {
            var title = new Label
            {
                Text = "常用查询",
                Font = CopilotTheme.FontTitle,
                ForeColor = CopilotTheme.TextPrimary,
                AutoSize = true
            };

            string[] favorites = {
                "🔍 查当前元素属性",
                "📋 列出所有子元素",
                "📏 测量两点距离",
                "✅ 检查属性完整性",
                "📊 导出元素清单",
                "⚡ 执行 PML 脚本"
            };

            var favList = new ListBox
            {
                Location = new Point(0, 28),
                Size = new Size(_rightPanel.Width - 16, 160),
                BackColor = CopilotTheme.BgLight,
                ForeColor = CopilotTheme.TextPrimary,
                Font = CopilotTheme.FontNormal,
                BorderStyle = BorderStyle.None,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            foreach (var f in favorites) favList.Items.Add(f);
            favList.DoubleClick += (s, e) =>
            {
                if (favList.SelectedItem != null)
                {
                    _chatList.AddUserMessage(favList.SelectedItem.ToString());
                    _chatList.AddSystemMessage($"已发送查询: {favList.SelectedItem}");
                }
            };

            var recentTitle = new Label
            {
                Text = "最近使用",
                Font = CopilotTheme.FontTitle,
                ForeColor = CopilotTheme.TextPrimary,
                AutoSize = true,
                Location = new Point(0, 200)
            };

            string[] recent = {
                "查 DN100 管道",
                "改 PIPE-001 壁厚为 SCH80",
                "导出区域元素清单"
            };

            var recentList = new ListBox
            {
                Location = new Point(0, 228),
                Size = new Size(_rightPanel.Width - 16, 80),
                BackColor = CopilotTheme.BgLight,
                ForeColor = CopilotTheme.TextSecondary,
                Font = CopilotTheme.FontSmall,
                BorderStyle = BorderStyle.None,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            foreach (var r in recent) recentList.Items.Add(r);

            _panelFavorites.Controls.Add(title);
            _panelFavorites.Controls.Add(favList);
            _panelFavorites.Controls.Add(recentTitle);
            _panelFavorites.Controls.Add(recentList);
        }

        /// <summary>构建历史面板</summary>
        private void BuildHistoryPanel()
        {
            var title = new Label
            {
                Text = "会话历史",
                Font = CopilotTheme.FontTitle,
                ForeColor = CopilotTheme.TextPrimary,
                AutoSize = true
            };

            string[] sessions = {
                "📌 当前会话 (3条消息)",
                "📁 管道查询 (2026-06-17)",
                "📁 设备检查 (2026-06-16)",
                "📁 批量修改 (2026-06-15)",
                "📁 导出报表 (2026-06-14)"
            };

            var sessionList = new ListBox
            {
                Location = new Point(0, 28),
                Size = new Size(_rightPanel.Width - 16, 200),
                BackColor = CopilotTheme.BgLight,
                ForeColor = CopilotTheme.TextPrimary,
                Font = CopilotTheme.FontNormal,
                BorderStyle = BorderStyle.None,
                Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Top
            };
            foreach (var s in sessions) sessionList.Items.Add(s);

            var btnExport = new Button
            {
                Text = "导出会话",
                Location = new Point(0, 240),
                Size = new Size(120, 28),
                BackColor = CopilotTheme.BgLight,
                ForeColor = CopilotTheme.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = CopilotTheme.FontSmall
            };
            btnExport.Click += (s, e) =>
            {
                _chatList.AddSystemMessage("会话导出功能将在 Phase 3 实现");
            };

            _panelHistory.Controls.Add(title);
            _panelHistory.Controls.Add(sessionList);
            _panelHistory.Controls.Add(btnExport);
        }

        /// <summary>构建状态栏</summary>
        private void BuildStatusBar()
        {
            _lblStatus = new Label
            {
                Text = "就绪",
                Font = CopilotTheme.FontSmall,
                ForeColor = CopilotTheme.AccentGreen,
                AutoSize = true,
                Location = new Point(8, 5)
            };

            _lblConnection = new Label
            {
                Text = "● LLM 已连接",
                Font = CopilotTheme.FontSmall,
                ForeColor = CopilotTheme.AccentGreen,
                AutoSize = true,
                Location = new Point(100, 5)
            };

            _lblModel = new Label
            {
                Text = "Qwen3.5-32B",
                Font = CopilotTheme.FontSmall,
                ForeColor = CopilotTheme.TextSecondary,
                AutoSize = true,
                Location = new Point(220, 5)
            };

            _lblTokens = new Label
            {
                Text = "Token: 0",
                Font = CopilotTheme.FontSmall,
                ForeColor = CopilotTheme.TextMuted,
                AutoSize = true,
                Location = new Point(340, 5)
            };

            _statusBar.Controls.Add(_lblStatus);
            _statusBar.Controls.Add(_lblConnection);
            _statusBar.Controls.Add(_lblModel);
            _statusBar.Controls.Add(_lblTokens);
        }

        /// <summary>构建快速操作面板</summary>
        private void BuildQuickActions()
        {
            var flowPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                Padding = new Padding(8, 4, 8, 4),
                AutoScroll = true,
                BackColor = CopilotTheme.BgMid
            };

            // 操作分类标题
            AddQuickActionGroup(flowPanel, "🔍 查询", new (string, string)[] {
                ("查当前元素", "查看当前选中元素的属性和信息"),
                ("查管道", "按名称/直径/等级查询管道"),
                ("查设备", "查询设备清单和属性"),
                ("查子元素", "列出当前元素的所有子元素")
            });

            AddQuickActionGroup(flowPanel, "✏️ 修改", new (string, string)[] {
                ("改壁厚", "修改管道/管件的壁厚等级"),
                ("改直径", "修改管道公称直径"),
                ("改材质", "修改元素材质"),
                ("批量改名", "批量重命名元素")
            });

            AddQuickActionGroup(flowPanel, "✅ 检查", new (string, string)[] {
                ("完整性检查", "检查属性是否完整"),
                ("命名规范", "检查命名是否符合规范"),
                ("碰撞检查", "检查元素间距是否足够"),
                ("一致性检查", "检查上下游口径一致性")
            });

            AddQuickActionGroup(flowPanel, "📐 计算", new (string, string)[] {
                ("测距离", "测量两点/两元素间距离"),
                ("测角度", "测量管道弯头角度"),
                ("定位点", "获取元素的世界坐标"),
                ("路线长度", "计算管道总长度")
            });

            AddQuickActionGroup(flowPanel, "📊 导出", new (string, string)[] {
                ("导出 Excel", "导出元素清单到 Excel"),
                ("生成报表", "生成 HTML/PDF 报表"),
                ("导入 Excel", "从 Excel 导入数据"),
                ("导出 PML", "导出操作记录为 PML 脚本")
            });

            _quickActionPanel.Controls.Add(flowPanel);
        }

        private void AddQuickActionGroup(FlowLayoutPanel parent, string groupTitle, (string name, string desc)[] items)
        {
            var titleLabel = new Label
            {
                Text = groupTitle,
                Font = CopilotTheme.FontTitle,
                ForeColor = CopilotTheme.AccentBlue,
                AutoSize = true,
                Margin = new Padding(0, 6, 0, 2)
            };
            parent.Controls.Add(titleLabel);

            foreach (var (name, desc) in items)
            {
                var btn = new Button
                {
                    Text = $"  {name}  — {desc}",
                    TextAlign = ContentAlignment.MiddleLeft,
                    AutoSize = false,
                    Height = 26,
                    Width = _quickActionPanel.Width - 32,
                    BackColor = CopilotTheme.BgLight,
                    ForeColor = CopilotTheme.TextPrimary,
                    FlatStyle = FlatStyle.Flat,
                    Font = CopilotTheme.FontSmall,
                    Cursor = Cursors.Hand,
                    Margin = new Padding(0, 1, 0, 1)
                };
                btn.FlatAppearance.BorderSize = 0;
                btn.Click += (s, e) =>
                {
                    _chatList.AddUserMessage(name);
                    _chatList.AddSystemMessage($"⏳ 正在{desc}...");
                    _quickActionPanel.Visible = false;
                    _quickActionVisible = false;
                };
                parent.Controls.Add(btn);
            }
        }

        /// <summary>事件订阅</summary>
        private void SubscribeEvents()
        {
            if (_controller != null)
                _controller.OnEvent += OnCopilotEvent;
        }

        private void OnCopilotEvent(CopilotEvent evt)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => OnCopilotEvent(evt)));
                return;
            }

            switch (evt.Kind)
            {
                case EventKind.Text:
                    _chatList.AppendStreamText(evt.Text);
                    break;
                case EventKind.Reasoning:
                    _chatList.AppendReasoning(evt.Text);
                    break;
                case EventKind.TurnStarted:
                    _lblStatus.Text = "AI 思考中...";
                    _lblStatus.ForeColor = CopilotTheme.AccentOrange;
                    break;
                case EventKind.TurnDone:
                    _chatList.FinalizeMessage();
                    _lblStatus.Text = "就绪";
                    _lblStatus.ForeColor = CopilotTheme.AccentGreen;
                    EnableInput(true);
                    break;
                case EventKind.Notice:
                    _chatList.AddSystemMessage(evt.Text);
                    break;
                case EventKind.Error:
                    _chatList.AddErrorMessage(evt.Text);
                    _lblStatus.Text = "错误";
                    _lblStatus.ForeColor = CopilotTheme.AccentRed;
                    EnableInput(true);
                    break;
                case EventKind.PlanModeChanged:
                    _btnPlanMode.Text = _controller.IsPlanMode ? "  Plan (开)  " : "  Plan  ";
                    _btnPlanMode.ForeColor = _controller.IsPlanMode
                        ? CopilotTheme.AccentOrange : CopilotTheme.TextSecondary;
                    break;
                case EventKind.Usage:
                    _lblTokens.Text = $"Token: {evt.Text}";
                    break;
                case EventKind.ToolDispatch:
                    _lblStatus.Text = $"执行: {evt.Text}";
                    break;
                case EventKind.ToolResult:
                    _chatList.AddAssistantMessage($"✅ {evt.Text}");
                    break;
            }
        }

        private void SendButton_Click(object sender, EventArgs e) => SendMessage();

        private void InputBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                SendMessage();
            }
        }

        private async void SendMessage()
        {
            string text = _inputBox.Text.Trim();
            if (string.IsNullOrEmpty(text)) return;

            _chatList.AddUserMessage(text);
            _inputBox.Clear();
            EnableInput(false);
            _lblStatus.Text = "AI 思考中...";
            _lblStatus.ForeColor = CopilotTheme.AccentOrange;

            try
            {
                await _controller.SendAsync(text);
            }
            catch (Exception ex)
            {
                _chatList.AddErrorMessage($"发送失败: {ex.Message}");
                EnableInput(true);
                _lblStatus.Text = "错误";
                _lblStatus.ForeColor = CopilotTheme.AccentRed;
            }
        }

        private void EnableInput(bool enabled)
        {
            _inputBox.Enabled = enabled;
            _sendButton.Enabled = enabled && !string.IsNullOrWhiteSpace(_inputBox.Text);
            if (enabled) _inputBox.Focus();
        }

        private void TogglePlanMode()
        {
            if (_controller != null)
                _controller.SetPlanMode(!_controller.IsPlanMode);
        }

        private void ToggleQuickActions()
        {
            _quickActionVisible = !_quickActionVisible;
            _quickActionPanel.Visible = _quickActionVisible;
        }

        private void ToggleSidebar()
        {
            _rightPanel.Visible = !_rightPanel.Visible;
        }

        private void NewSession()
        {
            _controller?.NewSession();
            _chatList.ClearMessages();
            _chatList.AddSystemMessage("已创建新会话");
        }

        private void ShowSettings()
        {
            using (var dialog = new SettingsDialog())
            {
                dialog.ShowDialog(this);
            }
        }
    }
}
