using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: AssemblyTitle("E3DCopilot.Core")]
// 测试项目需要访问 internal 成员（如 AgentLoop.BuildToolPolicy 的四种权限模式映射）
[assembly: InternalsVisibleTo("E3DCopilot.Tests")]
[assembly: AssemblyDescription("E3D AI Copilot — 核心逻辑层")]
[assembly: AssemblyCompany("E3D-E小智 Team")]
[assembly: AssemblyProduct("E3DCopilot")]
[assembly: AssemblyCopyright("Copyright © 2026")]
[assembly: ComVisible(false)]
[assembly: Guid("B2C3D4E5-E3DC-4C4F-A222-000000000002")]
