using System;
using System.Drawing;
using System.Windows.Forms;
using E3DCopilot.UI.Themes;

namespace E3DCopilot.UI.Controls
{
    /// <summary>
    /// 聊天消息列表 — 支持多态气泡渲染
    /// 用户/助手/系统/错误/推理 五种消息类型
    /// </summary>
    public class ChatListBox : Panel
    {
        private readonly FlowLayoutPanel _messagePanel;
        private Label _streamingLabel;
        private string _streamingText = "";
        private string _streamingHeader = "";

        public ChatListBox()
        {
            this.AutoScroll = true;
            this.BackColor = CopilotTheme.BgDark;

            _messagePanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(12, 8, 12, 8),
                BackColor = CopilotTheme.BgDark
            };

            this.Controls.Add(_messagePanel);

            // 欢迎消息
            AddAssistantMessage("👋 你好！我是 **E小智**，你的 E3D AI 编程助手。\n\n我可以帮你：\n• 🔍 查询管道、设备、结构元素\n• ✏️ 修改属性（壁厚、直径、材质等）\n• ✅ 检查元素完整性、命名规范\n• 📐 计算距离、角度、朝向\n• 📊 导出报表、导入 Excel\n• ⚡ 执行 PML 脚本\n\n**试试对我说：** \"查一下所有 DN100 的管道\"");
        }

        /// <summary>添加用户消息</summary>
        public void AddUserMessage(string text)
        {
            AddBubble(text, "🧑 你", CopilotTheme.BubbleUser, CopilotTheme.TextPrimary, false);
        }

        /// <summary>添加 AI 消息</summary>
        public void AddAssistantMessage(string text)
        {
            AddBubble(text, "🤖 E小智", CopilotTheme.BubbleAssistant, CopilotTheme.TextPrimary, false);
        }

        /// <summary>添加系统通知</summary>
        public void AddSystemMessage(string text)
        {
            AddBubble("ℹ️ " + text, "系统", CopilotTheme.BubbleSystem, CopilotTheme.TextSecondary, false);
        }

        /// <summary>添加错误消息</summary>
        public void AddErrorMessage(string text)
        {
            AddBubble("❌ " + text, "错误", CopilotTheme.BubbleError, CopilotTheme.AccentRed, false);
        }

        /// <summary>流式追加 AI 文本</summary>
        public void AppendStreamText(string text)
        {
            if (_streamingLabel == null)
            {
                _streamingLabel = CreateBubble("", "🤖 E小智",
                    CopilotTheme.BubbleAssistant, CopilotTheme.TextPrimary, false);
                _streamingHeader = "🤖 E小智";
                _messagePanel.Controls.Add(_streamingLabel);
                ScrollToBottom();
            }

            _streamingText += text;
            _streamingLabel.Text = _streamingHeader + "\n" + _streamingText;
        }

        /// <summary>追加推理过程（灰色折叠）</summary>
        public void AppendReasoning(string text)
        {
            if (_streamingLabel == null)
            {
                _streamingLabel = CreateBubble("", "💭 思考",
                    CopilotTheme.BubbleReasoning, CopilotTheme.TextSecondary, false);
                _streamingHeader = "💭 思考";
                _messagePanel.Controls.Add(_streamingLabel);
                ScrollToBottom();
            }

            _streamingText += text;
            _streamingLabel.Text = _streamingHeader + "\n" + _streamingText;
        }

        /// <summary>完成当前流式消息</summary>
        public void FinalizeMessage()
        {
            _streamingLabel = null;
            _streamingText = "";
            ScrollToBottom();
        }

        /// <summary>添加表格结果（模拟数据展示）</summary>
        public void AddTableResult(string[] columns, string[][] rows)
        {
            var tablePanel = new TableLayoutPanel
            {
                AutoSize = true,
                MaximumSize = new Size(this.ClientSize.Width - 48, 0),
                Padding = new Padding(0),
                Margin = new Padding(0, 4, 0, 4),
                BackColor = CopilotTheme.BgMid
            };

            // 表头
            tablePanel.ColumnCount = columns.Length;
            tablePanel.RowCount = rows.Length + 1;
            for (int c = 0; c < columns.Length; c++)
            {
                tablePanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
                var header = new Label
                {
                    Text = columns[c],
                    Font = CopilotTheme.FontSmall,
                    ForeColor = CopilotTheme.AccentBlue,
                    BackColor = CopilotTheme.BgLight,
                    Padding = new Padding(8, 4, 8, 4),
                    AutoSize = true
                };
                tablePanel.Controls.Add(header, c, 0);
            }

            // 数据行
            for (int r = 0; r < rows.Length; r++)
            {
                for (int c = 0; c < rows[r].Length && c < columns.Length; c++)
                {
                    var cell = new Label
                    {
                        Text = rows[r][c],
                        Font = CopilotTheme.FontSmall,
                        ForeColor = CopilotTheme.TextPrimary,
                        BackColor = r % 2 == 0 ? CopilotTheme.BgMid : CopilotTheme.BgHighlight,
                        Padding = new Padding(8, 3, 8, 3),
                        AutoSize = true
                    };
                    tablePanel.Controls.Add(cell, c, r + 1);
                }
            }

            // 包裹在气泡里
            var wrapper = new Panel
            {
                AutoSize = true,
                MaximumSize = new Size(this.ClientSize.Width - 32, 0),
                BackColor = CopilotTheme.BubbleAssistant,
                Padding = new Padding(10, 8, 10, 8),
                Margin = new Padding(0, 4, 0, 4)
            };
            wrapper.Controls.Add(tablePanel);
            _messagePanel.Controls.Add(wrapper);
            ScrollToBottom();
        }

        /// <summary>添加快速操作卡片</summary>
        public void AddQuickActionCard(string title, string description, Action onClick)
        {
            var card = new Panel
            {
                AutoSize = true,
                Width = this.ClientSize.Width - 48,
                BackColor = CopilotTheme.BgMid,
                Padding = new Padding(12, 10, 12, 10),
                Margin = new Padding(0, 3, 0, 3),
                Cursor = Cursors.Hand
            };
            card.Click += (s, e) => onClick?.Invoke();

            var titleLabel = new Label
            {
                Text = title,
                Font = CopilotTheme.FontTitle,
                ForeColor = CopilotTheme.AccentBlue,
                AutoSize = true,
                MaximumSize = new Size(card.Width - 24, 0)
            };
            titleLabel.Click += (s, e) => onClick?.Invoke();

            var descLabel = new Label
            {
                Text = description,
                Font = CopilotTheme.FontSmall,
                ForeColor = CopilotTheme.TextSecondary,
                AutoSize = true,
                MaximumSize = new Size(card.Width - 24, 0),
                Location = new Point(0, 22)
            };
            descLabel.Click += (s, e) => onClick?.Invoke();

            card.Controls.Add(titleLabel);
            card.Controls.Add(descLabel);
            _messagePanel.Controls.Add(card);
            ScrollToBottom();
        }

        /// <summary>清空所有消息</summary>
        public void ClearMessages()
        {
            _messagePanel.Controls.Clear();
            _streamingLabel = null;
            _streamingText = "";
        }

        private void AddBubble(string text, string header, Color bgColor, Color textColor, bool isStreaming)
        {
            var label = CreateBubble(text, header, bgColor, textColor, isStreaming);
            _messagePanel.Controls.Add(label);
            ScrollToBottom();
        }

        private Label CreateBubble(string text, string header, Color bgColor, Color textColor, bool isStreaming)
        {
            return new Label
            {
                AutoSize = true,
                Width = this.ClientSize.Width - 32,
                MaximumSize = new Size(this.ClientSize.Width - 32, 0),
                BackColor = bgColor,
                ForeColor = textColor,
                Font = CopilotTheme.FontNormal,
                Padding = new Padding(12, 10, 12, 10),
                Margin = new Padding(0, 4, 0, 4),
                Text = header + "\n" + text
            };
        }

        private void ScrollToBottom()
        {
            this.AutoScrollPosition = new Point(0,
                _messagePanel.Height - this.ClientSize.Height);
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            foreach (Control c in _messagePanel.Controls)
            {
                if (c is Label label)
                {
                    label.MaximumSize = new Size(this.ClientSize.Width - 32, 0);
                    label.Width = this.ClientSize.Width - 32;
                }
            }
        }
    }
}
