using System;
using System.Threading.Tasks;
using E3DCopilot.Core;
using E3DCopilot.Core.Config;
using E3DCopilot.Core.Events;
using E3DCopilot.Core.Providers;
using E3DCopilot.Core.Security;
using E3DCopilot.Core.Tools;
using E3DCopilot.Tools.Bridge;

namespace E3DCopilot.E2ETest
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.Title = "E小智 E2E 测试";

            Console.WriteLine("╔══════════════════════════════════════╗");
            Console.WriteLine("║   E小智 v1.0 — 端到端验证控制台     ║");
            Console.WriteLine("╚══════════════════════════════════════╝");
            Console.WriteLine();

            // 配置 LLM
            string baseUrl = "https://token-plan-cn.xiaomimimo.com/v1";
            string model = "mimo-v2.5";
            string apiKey = "tp-c6vbxwk3ttizyn5z97ua2to1szxz3eso49r11x65nwoi4r2e";

            Console.WriteLine($"🔗 LLM: {baseUrl}");
            Console.WriteLine($"📦 模型: {model}");
            Console.WriteLine();

            // 创建 E3D 环境（模拟模式）
            var env = new SimulatedE3DEnvironment();
            var dispatcher = new E3DToolDispatcher(env);

            // 创建 Provider
            var provider = new VllmProvider(baseUrl, model, apiKey);

            // 创建 Controller（使用默认配置）
            var config = CopilotConfig.Load();
            var executor = ToolExecutor.CreateDefault(dispatcher, null);
            var permission = CommandPermissionController.CreateDefault();
            var controller = new CopilotController(provider, executor, permission, config, null);

            // 订阅事件
            controller.OnEvent += evt =>
            {
                switch (evt.Kind)
                {
                    case EventKind.Text:
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.Write(evt.Text);
                        Console.ResetColor();
                        break;

                    case EventKind.StreamDelta:
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.Write(evt.Text);
                        Console.ResetColor();
                        break;

                    case EventKind.StreamEnd:
                        Console.WriteLine();
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine("─── 流式结束 ───");
                        Console.ResetColor();
                        break;

                    case EventKind.Thinking:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.Write($"[思考] {evt.Text}");
                        Console.ResetColor();
                        break;

                    case EventKind.ToolDispatch:
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine($"\n🛠 工具调用: {evt.Text}");
                        if (evt.Data != null)
                            Console.WriteLine($"   参数: {evt.Data}");
                        Console.ResetColor();
                        break;

                    case EventKind.ToolResult:
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"✅ 工具结果: {evt.Text}");
                        if (evt.Data != null)
                            Console.WriteLine($"   数据: {evt.Data}");
                        Console.ResetColor();
                        break;

                    case EventKind.ToolError:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"❌ 工具错误: {evt.Text}");
                        Console.ResetColor();
                        break;

                    case EventKind.ApprovalRequest:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"\n⚡ 需要审批: {evt.Text}");
                        Console.ResetColor();
                        // 自动批准
                        controller.Approve(evt.ToolId, true);
                        break;

                    case EventKind.Error:
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"\n❌ 错误: {evt.Text}");
                        Console.ResetColor();
                        break;

                    case EventKind.Notice:
                        Console.ForegroundColor = ConsoleColor.DarkGray;
                        Console.WriteLine($"\n📝 {evt.Text}");
                        Console.ResetColor();
                        break;
                }
            };

            // 发送测试消息
            string[] testQueries = new[]
            {
                "查询 DN100 管道",           // 测试 query 工具
                // "你好，你是谁？",           // 测试普通对话
            };

            foreach (var query in testQueries)
            {
                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"👤 >>> {query}");
                Console.ResetColor();
                Console.WriteLine();

                try
                {
                    await controller.SendAsync(query);
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"\n💥 异常: {ex.GetType().Name}: {ex.Message}");
                    Console.ResetColor();
                }

                Console.WriteLine();
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine("══════════════════════════════════════");
                Console.ResetColor();
                await Task.Delay(500);
            }

            Console.WriteLine();
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();
        }
    }
}
