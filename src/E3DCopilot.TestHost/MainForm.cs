using System;
using System.Drawing;
using System.Windows.Forms;
using E3DCopilot.UI.Controls;
using E3DCopilot.UI.Themes;

namespace E3DCopilot.TestHost
{
    /// <summary>
    /// 独立测试宿主窗体 — 展示 UI 控件而不需要 E3D 环境
    /// </summary>
    public class MainForm : Form
    {
        private ChatListBox _chatList;
        private InputPanel _inputControl;
        private TabControl _demoTabs;

        public MainForm()
        {
            this.Text = "E小智 v1.0 — UI 演示 (独立模式)";
            this.Size = new Size(900, 680);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = CopilotTheme.BgDark;
            this.ForeColor = CopilotTheme.TextPrimary;
            this.Font = CopilotTheme.FontNormal;
            this.MinimumSize = new Size(700, 500);

            BuildUI();
            LoadDemoData();
        }

        private void BuildUI()
        {
            // 顶部导航
            var navToolbar = new NavToolbar
            {
                Dock = DockStyle.Top
            };
            navToolbar.NewSessionClicked += (s, e) => _chatList.ClearMessages();
            navToolbar.QuickActionsClicked += (s, e) => ToggleDemoMessages();
            navToolbar.SettingsClicked += (s, e) =>
                MessageBox.Show("设置面板在 Phase 2 中实现", "设置", MessageBoxButtons.OK, MessageBoxIcon.Information);
            navToolbar.SidebarToggled += (s, e) =>
                MessageBox.Show("侧边栏切换", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // 状态栏
            var statusBar = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                BackColor = CopilotTheme.BgMid,
                Padding = new Padding(8, 4, 8, 4)
            };
            var statusLabel = new Label
            {
                Text = "✅ 就绪 · 独立演示模式 (无需 E3D)",
                Font = CopilotTheme.FontSmall,
                ForeColor = CopilotTheme.AccentGreen,
                AutoSize = true,
                Location = new Point(8, 5)
            };
            statusBar.Controls.Add(statusLabel);

            // 主聊天区
            _chatList = new ChatListBox
            {
                Dock = DockStyle.Fill,
                BackColor = CopilotTheme.BgDark
            };

            // 输入面板
            _inputControl = new InputPanel
            {
                Dock = DockStyle.Bottom,
                Height = 64,
                BackColor = CopilotTheme.BgMid
            };
            _inputControl.SendClicked += OnInputSend;

            // 底部演示功能区
            _demoTabs = new TabControl
            {
                Dock = DockStyle.Right,
                Width = 260,
                BackColor = CopilotTheme.BgMid,
                ForeColor = CopilotTheme.TextPrimary,
                Font = CopilotTheme.FontSmall
            };

            // Tab 1: 演示操作
            var demoPage = new Panel { BackColor = CopilotTheme.BgDark, Padding = new Padding(8) };
            int y = 8;
            AddDemoButton(demoPage, "📝 添加用户消息", () => _chatList.AddUserMessage($"这是一条用户消息 ({DateTime.Now:HH:mm:ss})"), ref y);
            AddDemoButton(demoPage, "🤖 AI 回复 (Markdown)", () =>
                _chatList.AddAssistantMessage(GetDemoMarkdown()), ref y);
            AddDemoButton(demoPage, "🔄 流式 AI 回复", () =>
            {
                _chatList.AppendStreamText("这是**流式**回复的");
                Timer t = null;
                t = new Timer { Interval = 300 };
                int step = 0;
                t.Tick += (s, e) =>
                {
                    step++;
                    if (step <= 3)
                        _chatList.AppendStreamText($" 第{step}段 ");
                    else
                    {
                        _chatList.FinalizeMessage();
                        t.Stop(); t.Dispose();
                    }
                };
                t.Start();
            }, ref y);
            AddDemoButton(demoPage, "🧠 推理过程", () =>
            {
                _chatList.AppendReasoning("正在分析管道拓扑...\n检查连接点...");
                Timer t = null;
                t = new Timer { Interval = 500 };
                int step = 0;
                t.Tick += (s, e) =>
                {
                    step++;
                    if (step <= 2)
                        _chatList.AppendReasoning($"推理步骤 {step}...\n");
                    else
                    {
                        _chatList.FinalizeMessage();
                        t.Stop(); t.Dispose();
                    }
                };
                t.Start();
            }, ref y);
            AddDemoButton(demoPage, "🛠 工具卡片 (模拟)", () => ShowDemoToolCard(), ref y);
            AddDemoButton(demoPage, "✅ 任务追踪演示", () => ShowDemoTasks(), ref y);
            AddDemoButton(demoPage, "📊 差异对比演示", () => ShowDemoDiff(), ref y);
            AddDemoButton(demoPage, "🔍 搜索结果演示", () => ShowDemoSearch(), ref y);
            AddDemoButton(demoPage, "💻 代码块演示", () => ShowDemoCodeBlock(), ref y);
            AddDemoButton(demoPage, "⚠️ 错误演示", () =>
                _chatList.AddErrorMessage("执行查询失败: 连接超时"), ref y);
            AddDemoButton(demoPage, "ℹ️ 系统通知", () =>
                _chatList.AddSystemMessage($"系统已就绪 ({DateTime.Now:HH:mm:ss})"), ref y);
            AddDemoButton(demoPage, "🗑 清空", () => _chatList.ClearMessages(), ref y);
            var demoPageTab = new TabPage("🎮 演示操作");
            demoPageTab.Controls.Add(demoPage);
            _demoTabs.TabPages.Add(demoPageTab);

            // Tab 2: 当前状态
            var infoPage = new Panel { BackColor = CopilotTheme.BgDark, Padding = new Padding(8) };
            var infoLabel = new Label
            {
                Text = "UI Phase 1-3 全部完成\n\n" +
                       "Controls: 23 个控件\n" +
                       "Services: 5 个服务\n" +
                       "文件总计: 29 新增 + 5 升级\n\n" +
                       "🎉 编译 0 错误通过",
                Font = CopilotTheme.FontNormal,
                ForeColor = CopilotTheme.TextPrimary,
                AutoSize = true,
                Location = new Point(8, 8)
            };
            infoPage.Controls.Add(infoLabel);
            var infoPageTab = new TabPage("ℹ️ 状态");
            infoPageTab.Controls.Add(infoPage);
            _demoTabs.TabPages.Add(infoPageTab);

            var mainContainer = new Panel { Dock = DockStyle.Fill, BackColor = CopilotTheme.BgDark };
            mainContainer.Controls.Add(_chatList);

            this.Controls.Add(mainContainer);
            this.Controls.Add(_demoTabs);
            this.Controls.Add(_inputControl);
            this.Controls.Add(navToolbar);
            this.Controls.Add(statusBar);
        }

        private void AddDemoButton(Panel parent, string text, Action onClick, ref int y)
        {
            var btn = new Button
            {
                Text = text,
                Location = new Point(4, y),
                Width = 228,
                Height = 28,
                FlatStyle = FlatStyle.Flat,
                BackColor = CopilotTheme.BgLight,
                ForeColor = CopilotTheme.TextPrimary,
                Font = CopilotTheme.FontSmall,
                Cursor = Cursors.Hand,
                TextAlign = ContentAlignment.MiddleLeft
            };
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = CopilotTheme.BorderLight;
            btn.Click += (s, e) => onClick();
            btn.MouseEnter += (s, e) => btn.BackColor = CopilotTheme.BgHighlight;
            btn.MouseLeave += (s, e) => btn.BackColor = CopilotTheme.BgLight;
            parent.Controls.Add(btn);
            y += 32;
        }

        private void OnInputSend(object sender, string text)
        {
            _chatList.AddUserMessage(text);
            _chatList.AppendStreamText($"你发送了: **{text}**\n\n这是演示模式下的模拟回复。在 E3D 环境中，这条消息会发送给 LLM 处理。\n\n支持的特性：\n- **粗体** 和 *斜体*\n- `行内代码`\n- 列表\n\n> 引用块");
            _chatList.FinalizeMessage();
        }

        private void LoadDemoData()
        {
            _chatList.AddAssistantMessage(GetWelcomeMarkdown());
        }

        private string GetWelcomeMarkdown()
        {
            return "👋 你好！我是 **E小智** 的 UI 演示模式\n\n" +
                   "当前处于**独立运行模式**，无需 E3D 环境即可预览全部 UI 组件。\n\n" +
                   "### 已实现的 UI 功能\n\n" +
                   "| 类别 | 状态 |\n" +
                   "|------|:----:|\n" +
                   "| Markdown 渲染 | ✅ 13 种语法 |\n" +
                   "| 工具卡片 | ✅ 四态动画 |\n" +
                   "| 推理面板 | ✅ 流式折叠 |\n" +
                   "| Diff 对比 | ✅ 行级差异 |\n" +
                   "| 任务追踪 | ✅ 证据机制 |\n" +
                   "| 审批容器 | ✅ 6 种模式 |\n" +
                   "| 语法高亮 | ✅ PML/C#/JSON |\n\n" +
                   "> 点击右侧 **🎮 演示操作** 标签页体验各组件效果\n\n" +
                   "**试试对我说：** \"查一下所有 DN100 的管道\"";
        }

        private string GetDemoMarkdown()
        {
            return "### 查询结果: DN100 管道\n\n" +
                   "找到 **12 条** 管道记录\n\n" +
                   "| 编号 | 名称 | 规格 | 材质 |\n" +
                   "|:----:|------|:----:|:----:|\n" +
                   "| 1 | P-1001 | DN100×4.0mm | CS |\n" +
                   "| 2 | P-1002 | DN100×4.5mm | SS304 |\n" +
                   "| 3 | P-1003 | DN100×3.6mm | CS |\n" +
                   "| 4 | P-1004 | DN100×5.0mm | SS316L |\n\n" +
                   "```pml\n" +
                   "! 查询 DN100 管道\n" +
                   "DO element get all pipes\n" +
                   "  where spec = 'DN100'\n" +
                   "  select name, spec, material\n" +
                   "```\n\n" +
                   "> 提示: 以上为模拟数据";
        }

        private void ShowDemoToolCard()
        {
            var card = new ToolCardControl
            {
                ToolName = "query_pipes",
                Status = ToolCardControl.ToolCardStatus.Running
            };
            card.SetArgs("{ spec: \"DN100\", material: \"CS\" }");
            _chatList.AddToolCard(card);

            // 模拟 done 状态
            Timer t = null;
            t = new Timer { Interval = 1500 };
            t.Tick += (s, e) =>
            {
                card.Status = ToolCardControl.ToolCardStatus.Done;
                card.SetDuration(1.2);
                card.SetResult("找到 12 条 DN100 管道记录");
                card.SetSummary("12 results");
                t.Stop(); t.Dispose();
            };
            t.Start();
        }

        private void ShowDemoTasks()
        {
            var tasks = new TaskTrackingPanel();
            tasks.AddTask("查询 DN100 管道");
            tasks.AddTask("提取管道列表");
            tasks.AddTask("计算材料总量");

            Timer t = null;
            int step = 0;
            t = new Timer { Interval = 500 };
            t.Tick += (s, e) =>
            {
                step++;
                if (step == 1) tasks.CompleteTask("查询 DN100 管道", "12 条记录");
                if (step == 2) tasks.CompleteTask("提取管道列表", "规格/材质齐全");
                if (step == 3) { tasks.CompleteTask("计算材料总量", "CS: 180m, SS304: 45m"); t.Stop(); t.Dispose(); }
            };
            t.Start();

            _chatList.AddTaskTracking(tasks);
        }

        private void ShowDemoDiff()
        {
            var diff = new DiffViewControl();
            diff.SetDiff(
                "PIPE P-1001\n  WALL 4.0\n  DIAM 114.3\n  MATL CS\nEND",
                "PIPE P-1001\n  WALL 6.0\n  DIAM 114.3\n  MATL SS304\nEND"
            );
            _chatList.AddDiffView(diff);
        }

        private void ShowDemoSearch()
        {
            var search = new SearchResultsPanel();
            search.SetResults(new[]
            {
                new SearchResultItem
                {
                    Title = "GetPipes() API",
                    Snippet = "Gets all pipes matching the given specification",
                    Confidence = 0.95,
                    Source = SearchSource.Api
                },
                new SearchResultItem
                {
                    Title = "管道查询 PML 示例",
                    Snippet = "DO element get all pipes where spec = $spec",
                    Confidence = 0.88,
                    Source = SearchSource.Pml
                },
                new SearchResultItem
                {
                    Title = "管道属性查询模式",
                    Snippet = "查询管道规格的推荐实现方式",
                    Confidence = 0.72,
                    Source = SearchSource.Pattern
                }
            });
            _chatList.AddSearchResults(search);
        }

        private void ShowDemoCodeBlock()
        {
            var code = new CodeBlock();
            code.SetCode(@"/ Get all pipes by spec
FUNCTION get_pipes($spec)
    DO element get all pipes
        where spec = $spec
        select name, spec, material, wall
    return results
ENDFUNCTION

! Execute
$pipes = get_pipes(""DN100"")
FOR each $pipe IN $pipes
    PRINT $pipe.name
ENDFOR", "pml");
            _chatList.AddCodeBlock(code);
        }

        private void ToggleDemoMessages()
        {
            _chatList.AddAssistantMessage("已为您执行以下操作：\n\n" +
                "1. ✅ 查询 DN100 管道 — 12 条\n" +
                "2. ✅ 提取材料清单 — 完成\n" +
                "3. ✅ 生成差异对比 — 完成\n\n" +
                "所有操作已完成。");
        }
    }
}
