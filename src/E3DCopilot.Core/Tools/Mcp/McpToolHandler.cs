using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools.Mcp
{
    /// <summary>管理多个只读 MCP server 客户端（按配置名索引）</summary>
    public class McpRegistry : IDisposable
    {
        private readonly Dictionary<string, IMcpClient> _clients =
            new Dictionary<string, IMcpClient>(StringComparer.OrdinalIgnoreCase);

        public void Register(IMcpClient client)
        {
            if (client == null) return;
            _clients[client.Name] = client;
        }

        public IMcpClient Get(string name)
        {
            _clients.TryGetValue(name, out var c);
            return c;
        }

        public IReadOnlyCollection<IMcpClient> All => _clients.Values;

        public void Dispose()
        {
            foreach (var c in _clients.Values)
            {
                try { c.Dispose(); } catch { }
            }
            _clients.Clear();
        }
    }

    /// <summary>
    /// mcp_knowledge — 只读知识检索工具，包装 MCP 的 resources/prompts。
    /// 强制 IsReadOnly=true，绝不暴露写操作。
    /// </summary>
    public class McpToolHandler : IToolHandler
    {
        private readonly McpRegistry _registry;

        public McpToolHandler(McpRegistry registry)
        {
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
        }

        public string Name => "mcp_knowledge";

        public string Description =>
            "Query read-only MCP servers for external knowledge. Actions: list (resources), " +
            "read_resource (uri), list_prompts, get_prompt (prompt + arguments). " +
            "Strictly read-only; cannot modify anything.";

        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""server"": { ""type"": ""string"", ""description"": ""MCP server name (from config)"" },
    ""action"": { ""type"": ""string"", ""enum"": [""list"",""read_resource"",""list_prompts"",""get_prompt""], ""description"": ""Action"" },
    ""uri"": { ""type"": ""string"", ""description"": ""Resource URI (for read_resource)"" },
    ""prompt"": { ""type"": ""string"", ""description"": ""Prompt name (for get_prompt)"" },
    ""arguments"": { ""type"": ""object"", ""description"": ""Prompt arguments (for get_prompt)"" }
  },
  ""required"": [""server"",""action""]
}";

        public bool IsReadOnly => true;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var json = JObject.Parse(args);
                string server = json.Value<string>("server");
                string action = json.Value<string>("action") ?? "list";
                if (string.IsNullOrWhiteSpace(server))
                    return ToolResult.Fail("'server' is required");

                var client = _registry.Get(server);
                if (client == null)
                    return ToolResult.Fail("MCP server not found: " + server);

                await client.InitializeAsync(ct);

                switch (action)
                {
                    case "list":
                        var res = await client.ListResourcesAsync(ct);
                        if (res.Length == 0) return ToolResult.Ok("(no resources)");
                        return ToolResult.Ok(string.Join("\n",
                            System.Array.ConvertAll(res, r => r.Uri + "  " + (r.Name ?? ""))));
                    case "read_resource":
                        var uri = json.Value<string>("uri");
                        if (string.IsNullOrWhiteSpace(uri)) return ToolResult.Fail("'uri' required for read_resource");
                        return ToolResult.Ok(await client.ReadResourceAsync(uri, ct));
                    case "list_prompts":
                        var ps = await client.ListPromptsAsync(ct);
                        if (ps.Length == 0) return ToolResult.Ok("(no prompts)");
                        return ToolResult.Ok(string.Join("\n",
                            System.Array.ConvertAll(ps, p => p.Name + "  " + (p.Description ?? ""))));
                    case "get_prompt":
                        var pname = json.Value<string>("prompt");
                        if (string.IsNullOrWhiteSpace(pname)) return ToolResult.Fail("'prompt' required for get_prompt");
                        var pargs = new Dictionary<string, string>();
                        var argTok = json["arguments"] as JObject;
                        if (argTok != null)
                            foreach (var kv in argTok)
                                pargs[kv.Key] = kv.Value?.Value<string>() ?? "";
                        return ToolResult.Ok(await client.GetPromptAsync(pname, pargs, ct));
                    default:
                        return ToolResult.Fail("unknown action: " + action);
                }
            }
            catch (OperationCanceledException)
            {
                return ToolResult.Fail("MCP query cancelled");
            }
            catch (Exception ex)
            {
                return ToolResult.Fail("MCP query failed: " + ex.Message);
            }
        }
    }
}
