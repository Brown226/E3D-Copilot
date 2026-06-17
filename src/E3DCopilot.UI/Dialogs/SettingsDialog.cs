using System;
using System.Drawing;
using System.Windows.Forms;
using E3DCopilot.UI.Themes;

namespace E3DCopilot.UI.Dialogs
{
    /// <summary>
    /// 设置对话框 — LLM / 安全 / 主题 / 记忆 配置
    /// </summary>
    public class SettingsDialog : Form
    {
        private TabControl _tabControl;

        // LLM 设置
        private TextBox _txtBaseUrl;
        private TextBox _txtModel;
        private NumericUpDown _numTemperature;
        private NumericUpDown _numMaxTokens;

        // 安全设置
        private CheckBox _chkAutoApprove;
        private NumericUpDown _numBatchThreshold;
        private CheckBox _chkConfirmDelete;

        // 主题设置
        private ComboBox _cmbTheme;
        private NumericUpDown _numFontSize;

        public SettingsDialog()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "E小智 设置";
            this.Size = new Size(520, 420);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = CopilotTheme.BgDark;
            this.ForeColor = CopilotTheme.TextPrimary;
            this.Font = CopilotTheme.FontNormal;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            _tabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                BackColor = CopilotTheme.BgMid,
                ForeColor = CopilotTheme.TextPrimary,
                Font = CopilotTheme.FontNormal,
                Padding = new Point(8, 6)
            };

            // ---- LLM 设置页 ----
            var llmPage = new Panel { BackColor = CopilotTheme.BgDark, Padding = new Padding(16) };
            int y = 16;

            AddLabel(llmPage, "LLM 服务地址", 0, ref y);
            _txtBaseUrl = AddTextBox(llmPage, "http://localhost:8000/v1", 0, ref y);

            AddLabel(llmPage, "模型名称", 0, ref y);
            _txtModel = AddTextBox(llmPage, "Qwen3.5-32B", 0, ref y);

            AddLabel(llmPage, "温度 (Temperature)", 0, ref y);
            _numTemperature = AddNumeric(llmPage, 0.0m, 2.0m, 0.1m, 0.1m, 0, ref y);

            AddLabel(llmPage, "最大 Token", 0, ref y);
            _numMaxTokens = AddNumeric(llmPage, 1024, 65536, 8192, 1024, 0, ref y);

            // ---- 安全设置页 ----
            var safetyPage = new Panel { BackColor = CopilotTheme.BgDark, Padding = new Padding(16) };
            y = 16;

            _chkAutoApprove = AddCheckbox(safetyPage, "只读工具自动执行（无需确认）", true, 0, ref y);
            _chkConfirmDelete = AddCheckbox(safetyPage, "删除操作需确认", true, 0, ref y);

            AddLabel(safetyPage, "批量修改阈值（超过此数量需审批）", 0, ref y);
            _numBatchThreshold = AddNumeric(safetyPage, 1, 100, 10, 5, 0, ref y);

            // ---- 主题设置页 ----
            var themePage = new Panel { BackColor = CopilotTheme.BgDark, Padding = new Padding(16) };
            y = 16;

            AddLabel(themePage, "主题", 0, ref y);
            _cmbTheme = new ComboBox
            {
                Location = new Point(0, y),
                Size = new Size(200, 24),
                BackColor = CopilotTheme.BgLight,
                ForeColor = CopilotTheme.TextPrimary,
                Font = CopilotTheme.FontNormal,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            _cmbTheme.Items.AddRange(new[] { "暗色 (默认)", "亮色", "跟随系统" });
            _cmbTheme.SelectedIndex = 0;
            themePage.Controls.Add(_cmbTheme);
            y += 32;

            AddLabel(themePage, "字体大小", 0, ref y);
            _numFontSize = AddNumeric(themePage, 8, 20, 12, 1, 0, ref y);

            // ---- 关于页 ----
            var aboutPage = new Panel { BackColor = CopilotTheme.BgDark, Padding = new Padding(16) };
            y = 16;

            var aboutTitle = new Label
            {
                Text = "E小智 v1.0",
                Font = CopilotTheme.FontTitle,
                ForeColor = CopilotTheme.AccentBlue,
                AutoSize = true,
                Location = new Point(0, y)
            };
            aboutPage.Controls.Add(aboutTitle);
            y += 28;

            var aboutDesc = new Label
            {
                Text = "AVEVA E3D AI Copilot\n自然语言驱动的 E3D 工厂设计助手\n\n技术栈: C# / .NET Framework 4.8 / WinForms\nLLM: vLLM + Qwen3.5-32B\n\n© 2026 E3D-E小智 Team",
                Font = CopilotTheme.FontNormal,
                ForeColor = CopilotTheme.TextSecondary,
                AutoSize = true,
                Location = new Point(0, y)
            };
            aboutPage.Controls.Add(aboutDesc);

            // ---- 添加 Tab 页 ----
            _tabControl.TabPages.Add(new TabPage("🤖 LLM") { Controls = { llmPage } });
            _tabControl.TabPages.Add(new TabPage("🔒 安全") { Controls = { safetyPage } });
            _tabControl.TabPages.Add(new TabPage("🎨 主题") { Controls = { themePage } });
            _tabControl.TabPages.Add(new TabPage("ℹ️ 关于") { Controls = { aboutPage } });

            // ---- 底部按钮 ----
            var btnPanel = new Panel
            {
                Height = 48,
                Dock = DockStyle.Bottom,
                BackColor = CopilotTheme.BgMid,
                Padding = new Padding(12, 8, 12, 8)
            };

            var btnSave = new Button
            {
                Text = "保存",
                Location = new Point(btnPanel.Width - 170, 8),
                Size = new Size(75, 28),
                BackColor = CopilotTheme.AccentBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = CopilotTheme.FontNormal,
                Anchor = AnchorStyles.Right
            };
            btnSave.Click += (s, e) => { /* Phase 2: 保存配置 */ this.Close(); };

            var btnCancel = new Button
            {
                Text = "取消",
                Location = new Point(btnPanel.Width - 90, 8),
                Size = new Size(75, 28),
                BackColor = CopilotTheme.BgLight,
                ForeColor = CopilotTheme.TextPrimary,
                FlatStyle = FlatStyle.Flat,
                Font = CopilotTheme.FontNormal,
                Anchor = AnchorStyles.Right
            };
            btnCancel.Click += (s, e) => this.Close();

            btnPanel.Controls.Add(btnSave);
            btnPanel.Controls.Add(btnCancel);

            this.Controls.Add(_tabControl);
            this.Controls.Add(btnPanel);
        }

        private void AddLabel(Panel parent, string text, int x, ref int y)
        {
            var lbl = new Label
            {
                Text = text,
                Font = CopilotTheme.FontNormal,
                ForeColor = CopilotTheme.TextPrimary,
                AutoSize = true,
                Location = new Point(x, y)
            };
            parent.Controls.Add(lbl);
            y += 22;
        }

        private TextBox AddTextBox(Panel parent, string defaultValue, int x, ref int y)
        {
            var tb = new TextBox
            {
                Text = defaultValue,
                Location = new Point(x, y),
                Size = new Size(280, 24),
                BackColor = CopilotTheme.BgLight,
                ForeColor = CopilotTheme.TextPrimary,
                Font = CopilotTheme.FontNormal,
                BorderStyle = BorderStyle.FixedSingle
            };
            parent.Controls.Add(tb);
            y += 32;
            return tb;
        }

        private NumericUpDown AddNumeric(Panel parent, decimal min, decimal max,
            decimal defaultValue, decimal increment, int x, ref int y)
        {
            var num = new NumericUpDown
            {
                Minimum = min,
                Maximum = max,
                Value = defaultValue,
                Increment = increment,
                Location = new Point(x, y),
                Size = new Size(120, 24),
                BackColor = CopilotTheme.BgLight,
                ForeColor = CopilotTheme.TextPrimary,
                Font = CopilotTheme.FontNormal
            };
            parent.Controls.Add(num);
            y += 32;
            return num;
        }

        private CheckBox AddCheckbox(Panel parent, string text, bool defaultValue, int x, ref int y)
        {
            var cb = new CheckBox
            {
                Text = text,
                Checked = defaultValue,
                Location = new Point(x, y),
                AutoSize = true,
                Font = CopilotTheme.FontNormal,
                ForeColor = CopilotTheme.TextPrimary
            };
            parent.Controls.Add(cb);
            y += 28;
            return cb;
        }
    }
}
