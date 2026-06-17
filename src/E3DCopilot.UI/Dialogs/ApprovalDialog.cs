using System;
using System.Drawing;
using System.Windows.Forms;
using E3DCopilot.Core.Security;
using E3DCopilot.UI.Themes;

namespace E3DCopilot.UI.Dialogs
{
    /// <summary>
    /// 操作审批对话框 — 用户确认/拒绝工具调用
    /// </summary>
    public class ApprovalDialog : Form
    {
        private readonly PendingApproval _approval;
        private Label _lblToolName;
        private TextBox _txtArgs;
        private CheckBox _chkPersist;
        private Button _btnAllow;
        private Button _btnDeny;

        public ApprovalResult Result { get; private set; }

        public ApprovalDialog(PendingApproval approval)
        {
            _approval = approval;
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.Text = "操作确认";
            this.Size = new Size(480, 300);
            this.StartPosition = FormStartPosition.CenterParent;
            this.BackColor = CopilotTheme.BgDark;
            this.ForeColor = CopilotTheme.TextPrimary;
            this.Font = CopilotTheme.FontNormal;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            // 标题
            var title = new Label
            {
                Text = "⚠️ E小智 请求执行以下操作",
                Font = CopilotTheme.FontTitle,
                ForeColor = CopilotTheme.AccentOrange,
                AutoSize = true,
                Location = new Point(16, 16)
            };

            // 工具名称
            _lblToolName = new Label
            {
                Text = $"工具: {_approval.ToolName}",
                Font = CopilotTheme.FontNormal,
                ForeColor = CopilotTheme.TextPrimary,
                AutoSize = true,
                Location = new Point(16, 48)
            };

            // 参数
            var argsLabel = new Label
            {
                Text = "参数:",
                Font = CopilotTheme.FontNormal,
                ForeColor = CopilotTheme.TextSecondary,
                AutoSize = true,
                Location = new Point(16, 76)
            };

            _txtArgs = new TextBox
            {
                Text = _approval.Args,
                Location = new Point(16, 100),
                Size = new Size(440, 60),
                BackColor = CopilotTheme.BgLight,
                ForeColor = CopilotTheme.TextPrimary,
                Font = CopilotTheme.FontCode,
                Multiline = true,
                ReadOnly = true,
                BorderStyle = BorderStyle.FixedSingle,
                ScrollBars = ScrollBars.Vertical
            };

            // 描述
            var descLabel = new Label
            {
                Text = _approval.Description,
                Font = CopilotTheme.FontSmall,
                ForeColor = CopilotTheme.TextMuted,
                AutoSize = true,
                Location = new Point(16, 170)
            };

            // 本次会话记住
            _chkPersist = new CheckBox
            {
                Text = "本次会话记住此选择，不再询问",
                Location = new Point(16, 196),
                AutoSize = true,
                Font = CopilotTheme.FontSmall,
                ForeColor = CopilotTheme.TextSecondary
            };

            // 按钮
            _btnDeny = new Button
            {
                Text = "拒绝",
                Location = new Point(280, 226),
                Size = new Size(80, 28),
                BackColor = CopilotTheme.AccentRed,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = CopilotTheme.FontNormal
            };
            _btnDeny.Click += (s, e) =>
            {
                _approval.Complete(false, _chkPersist.Checked);
                this.Close();
            };

            _btnAllow = new Button
            {
                Text = "允许",
                Location = new Point(370, 226),
                Size = new Size(80, 28),
                BackColor = CopilotTheme.AccentBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font = CopilotTheme.FontNormal
            };
            _btnAllow.Click += (s, e) =>
            {
                _approval.Complete(true, _chkPersist.Checked);
                this.Close();
            };

            this.Controls.Add(title);
            this.Controls.Add(_lblToolName);
            this.Controls.Add(argsLabel);
            this.Controls.Add(_txtArgs);
            this.Controls.Add(descLabel);
            this.Controls.Add(_chkPersist);
            this.Controls.Add(_btnDeny);
            this.Controls.Add(_btnAllow);
        }
    }
}
