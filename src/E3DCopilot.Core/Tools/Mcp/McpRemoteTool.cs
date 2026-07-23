using System;
using System.Threading;
using System.Threading.Tasks;

namespace E3DCopilot.Core.Tools.Mcp
{
    /// <summary>
    /// MCP 远程工具适配器 — 对齐 Reasonix remoteTool。
    /// 实现 IToolHandler 接口，让 AgentLoop 像调用内置工具一样调用 MCP 工具。
    /// 工具名格式: mcp__&lt;server&gt;__&lt;tool&gt;
    /// </summary>
    public class McpRemoteTool : IToolHandler
    {
        private readonly McpClient _client;
        private readonly string _rawName;
        private readonly string _visibleName;
        private readonly McpToolInfo _toolInfo;
        private readonly bool _readOnlyOverride;

        /// <summary>模型可见名称: mcp__server__tool</summary>
        public string Name { get; }

        /// <summary>工具描述</summary>
        public string Description => _toolInfo.Description ?? "";

        /// <summary>参数 JSON Schema</summary>
        public string ParameterSchema => _toolInfo.InputSchema ?? "{\"type\":\"object\"}";

        /// <summary>是否只读（基于 MCP readOnlyHint + 配置覆盖）</summary>
        public bool IsReadOnly => _readOnlyOverride || _toolInfo.ReadOnlyHint;

        /// <summary>是否为破坏性工具（MCP destructiveHint）</summary>
        public bool IsDestructive => _toolInfo.DestructiveHint;

        /// <summary>原始工具名（用于 tools/call 协议调用）</summary>
        public string RawName => _rawName;

        /// <summary>所属 MCP server 名称</summary>
        public string ServerName => _client.Name;

        public McpRemoteTool(McpClient client, McpToolInfo toolInfo,
            string serverName, string stripRawPrefix = null,
            bool readOnlyOverride = false)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _toolInfo = toolInfo ?? throw new ArgumentNullException(nameof(toolInfo));
            _rawName = toolInfo.Name;
            _readOnlyOverride = readOnlyOverride;

            // 应用 StripRawPrefix（对齐 Reasonix Spec.StripRawPrefix）
            _visibleName = McpToolNaming.ApplyStripPrefix(_rawName, stripRawPrefix);

            // 构建模型可见名称: mcp__<server>__<tool>
            Name = McpToolNaming.ToolName(serverName, _visibleName);
        }

        /// <summary>
        /// 执行 MCP 工具（对齐 Reasonix remoteTool.Execute → tools/call）
        /// </summary>
        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                var result = await _client.CallToolAsync(_rawName, args, ct).ConfigureAwait(false);

                if (result.IsError)
                {
                    return new ToolResult
                    {
                        Success = false,
                        Text = result.Text,
                        Error = result.Text
                    };
                }

                return new ToolResult
                {
                    Success = true,
                    Text = result.Text
                };
            }
            catch (TimeoutException ex)
            {
                return new ToolResult
                {
                    Success = false,
                    Error = $"MCP 工具超时: {ex.Message}"
                };
            }
            catch (Exception ex)
            {
                return new ToolResult
                {
                    Success = false,
                    Error = $"MCP 工具调用失败 [{_client.Name}/{_rawName}]: {ex.Message}"
                };
            }
        }
    }
}
