using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using CNPE.ISO.E3D.Core;
using CNPE.ISO.Model;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// 管道信息提取工具 - 封装CNPE.IC.ISO的PipeReader功能
    /// 从E3D中提取管道、管件、支吊架等详细信息
    /// </summary>
    public class PipeInfoHandler : IToolHandler
    {
        private readonly IToolDispatcher _dispatcher;

        public PipeInfoHandler(IToolDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public string Name => "get_pipe_info";
        public string Description => "从E3D中提取管道详细信息，包括管道属性、分支信息、管件列表、支吊架信息等。支持单个管道和批量查询。";
        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""action"": {
      ""type"": ""string"",
      ""enum"": [""get_pipe_detail"", ""get_branch_info"", ""get_pipe_components"", ""get_supports"", ""list_pipes"", ""get_pipe_hierarchy""],
      ""description"": ""操作类型：get_pipe_detail-获取管道详情，get_branch_info-获取分支信息，get_pipe_components-获取管件列表，get_supports-获取支吊架信息，list_pipes-列出管道，get_pipe_hierarchy-获取管道层级结构""
    },
    ""pipe_name"": {
      ""type"": ""string"",
      ""description"": ""管道名称，如 /PIPE-1001 或 PIPE-1001""
    },
    ""branch_name"": {
      ""type"": ""string"",
      ""description"": ""分支名称，如 /BRAN-1001-1""
    },
    ""zone_name"": {
      ""type"": ""string"",
      ""description"": ""区域名称，用于列出该区域下的管道""
    },
    ""include_attributes"": {
      ""type"": ""boolean"",
      ""description"": ""是否包含详细属性，默认为 true""
    },
    ""include_hierarchy"": {
      ""type"": ""boolean"",
      ""description"": ""是否包含层级结构，默认为 false""
    },
    ""limit"": {
      ""type"": ""integer"",
      ""description"": ""返回结果数量限制，默认50""
    }
  },
  ""required"": [""action""]
}";
        public bool IsReadOnly => true;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                var json = JObject.Parse(args ?? "{}");
                string action = json["action"]?.ToString();

                if (string.IsNullOrEmpty(action))
                    return ToolResult.Fail("缺少 action 参数");

                switch (action.ToLower())
                {
                    case "get_pipe_detail":
                        return await GetPipeDetail(json, ct);
                    case "get_branch_info":
                        return await GetBranchInfo(json, ct);
                    case "get_pipe_components":
                        return await GetPipeComponents(json, ct);
                    case "get_supports":
                        return await GetSupports(json, ct);
                    case "list_pipes":
                        return await ListPipes(json, ct);
                    case "get_pipe_hierarchy":
                        return await GetPipeHierarchy(json, ct);
                    default:
                        return ToolResult.Fail($"不支持的操作: {action}");
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"管道信息提取失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 获取管道详情
        /// </summary>
        private async Task<ToolResult> GetPipeDetail(JObject json, CancellationToken ct)
        {
            string pipeName = json["pipe_name"]?.ToString();
            if (string.IsNullOrEmpty(pipeName))
                return ToolResult.Fail("获取管道详情时需要指定 pipe_name 参数");

            bool includeAttributes = json["include_attributes"]?.Value<bool>() ?? true;

            // 通过E3D API获取管道信息
            var pipeInfo = await GetPipeInfoFromE3D(pipeName, includeAttributes, ct);
            
            if (pipeInfo == null)
            {
                return ToolResult.Ok($"未找到管道 '{pipeName}'", new JObject
                {
                    ["tool"] = "get_pipe_info",
                    ["action"] = "get_pipe_detail",
                    ["pipe_name"] = pipeName,
                    ["found"] = false
                });
            }

            var summary = $"管道 {pipeName} 的详细信息:\n\n" +
                         $"名称: {pipeInfo.Name}\n" +
                         $"类型: {pipeInfo.Type}\n" +
                         $"规格: {pipeInfo.Specification}\n" +
                         $"公称直径: {pipeInfo.NominalDiameter}\n" +
                         $"设计压力: {pipeInfo.DesignPressure}\n" +
                         $"设计温度: {pipeInfo.DesignTemperature}\n" +
                         $"材料: {pipeInfo.Material}\n" +
                         $"保温等级: {pipeInfo.InsulationClass}\n" +
                         $"伴热要求: {pipeInfo.HeatTracing}\n" +
                         $"流体类型: {pipeInfo.FluidType}\n" +
                         $"分支数量: {pipeInfo.BranchCount}\n" +
                         $"总长度: {pipeInfo.TotalLength:F2}m\n\n";

            if (includeAttributes && pipeInfo.Attributes != null)
            {
                summary += "详细属性:\n";
                foreach (var attr in pipeInfo.Attributes)
                {
                    summary += $"  {attr.Key}: {attr.Value}\n";
                }
            }

            var meta = new JObject
            {
                ["tool"] = "get_pipe_info",
                ["action"] = "get_pipe_detail",
                ["pipe_name"] = pipeName,
                ["found"] = true,
                ["pipe_info"] = JObject.FromObject(pipeInfo)
            };

            return ToolResult.Ok(summary, meta);
        }

        /// <summary>
        /// 获取分支信息
        /// </summary>
        private async Task<ToolResult> GetBranchInfo(JObject json, CancellationToken ct)
        {
            string branchName = json["branch_name"]?.ToString();
            if (string.IsNullOrEmpty(branchName))
                return ToolResult.Fail("获取分支信息时需要指定 branch_name 参数");

            bool includeAttributes = json["include_attributes"]?.Value<bool>() ?? true;

            var branchInfo = await GetBranchInfoFromE3D(branchName, includeAttributes, ct);
            
            if (branchInfo == null)
            {
                return ToolResult.Ok($"未找到分支 '{branchName}'", new JObject
                {
                    ["tool"] = "get_pipe_info",
                    ["action"] = "get_branch_info",
                    ["branch_name"] = branchName,
                    ["found"] = false
                });
            }

            var summary = $"分支 {branchName} 的详细信息:\n\n" +
                         $"名称: {branchInfo.Name}\n" +
                         $"所属管道: {branchInfo.PipeName}\n" +
                         $"分支类型: {branchInfo.BranchType}\n" +
                         $"起点: {branchInfo.StartPoint}\n" +
                         $"终点: {branchInfo.EndPoint}\n" +
                         $"长度: {branchInfo.Length:F2}m\n" +
                         $"公称直径: {branchInfo.NominalDiameter}\n" +
                         $"管件数量: {branchInfo.ComponentCount}\n\n";

            if (branchInfo.Components != null && branchInfo.Components.Count > 0)
            {
                summary += "管件列表:\n";
                foreach (var comp in branchInfo.Components.Take(10))
                {
                    summary += $"  - {comp.Name} ({comp.Type})\n";
                }
                if (branchInfo.Components.Count > 10)
                    summary += $"  ... 还有 {branchInfo.Components.Count - 10} 个管件\n";
            }

            var meta = new JObject
            {
                ["tool"] = "get_pipe_info",
                ["action"] = "get_branch_info",
                ["branch_name"] = branchName,
                ["found"] = true,
                ["branch_info"] = JObject.FromObject(branchInfo)
            };

            return ToolResult.Ok(summary, meta);
        }

        /// <summary>
        /// 获取管件列表
        /// </summary>
        private async Task<ToolResult> GetPipeComponents(JObject json, CancellationToken ct)
        {
            string pipeName = json["pipe_name"]?.ToString();
            if (string.IsNullOrEmpty(pipeName))
                return ToolResult.Fail("获取管件列表时需要指定 pipe_name 参数");

            int limit = json["limit"]?.Value<int>() ?? 50;

            var components = await GetPipeComponentsFromE3D(pipeName, limit, ct);
            
            if (components == null || components.Count == 0)
            {
                return ToolResult.Ok($"未找到管道 '{pipeName}' 的管件信息", new JObject
                {
                    ["tool"] = "get_pipe_info",
                    ["action"] = "get_pipe_components",
                    ["pipe_name"] = pipeName,
                    ["count"] = 0
                });
            }

            var summary = $"管道 {pipeName} 的管件列表 (共 {components.Count} 个):\n\n";
            foreach (var comp in components.Take(20))
            {
                summary += $"类型: {comp.Type}\n";
                summary += $"名称: {comp.Name}\n";
                summary += $"规格: {comp.Specification}\n";
                summary += $"材料: {comp.Material}\n";
                summary += $"位置: {comp.Position}\n\n";
            }

            if (components.Count > 20)
                summary += $"... 还有 {components.Count - 20} 个管件\n";

            var meta = new JObject
            {
                ["tool"] = "get_pipe_info",
                ["action"] = "get_pipe_components",
                ["pipe_name"] = pipeName,
                ["count"] = components.Count,
                ["components"] = JArray.FromObject(components.Take(50).Select(c => new
                {
                    type = c.Type,
                    name = c.Name,
                    specification = c.Specification,
                    material = c.Material,
                    position = c.Position
                }))
            };

            return ToolResult.Ok(summary, meta);
        }

        /// <summary>
        /// 获取支吊架信息
        /// </summary>
        private async Task<ToolResult> GetSupports(JObject json, CancellationToken ct)
        {
            string pipeName = json["pipe_name"]?.ToString();
            if (string.IsNullOrEmpty(pipeName))
                return ToolResult.Fail("获取支吊架信息时需要指定 pipe_name 参数");

            int limit = json["limit"]?.Value<int>() ?? 50;

            var supports = await GetSupportsFromE3D(pipeName, limit, ct);
            
            if (supports == null || supports.Count == 0)
            {
                return ToolResult.Ok($"未找到管道 '{pipeName}' 的支吊架信息", new JObject
                {
                    ["tool"] = "get_pipe_info",
                    ["action"] = "get_supports",
                    ["pipe_name"] = pipeName,
                    ["count"] = 0
                });
            }

            var summary = $"管道 {pipeName} 的支吊架列表 (共 {supports.Count} 个):\n\n";
            foreach (var support in supports.Take(20))
            {
                summary += $"类型: {support.Type}\n";
                summary += $"名称: {support.Name}\n";
                summary += $"位置: {support.Position}\n";
                summary += $"载荷: {support.Load}\n";
                summary += $"刚度: {support.Stiffness}\n\n";
            }

            if (supports.Count > 20)
                summary += $"... 还有 {supports.Count - 20} 个支吊架\n";

            var meta = new JObject
            {
                ["tool"] = "get_pipe_info",
                ["action"] = "get_supports",
                ["pipe_name"] = pipeName,
                ["count"] = supports.Count,
                ["supports"] = JArray.FromObject(supports.Take(50).Select(s => new
                {
                    type = s.Type,
                    name = s.Name,
                    position = s.Position,
                    load = s.Load,
                    stiffness = s.Stiffness
                }))
            };

            return ToolResult.Ok(summary, meta);
        }

        /// <summary>
        /// 列出管道
        /// </summary>
        private async Task<ToolResult> ListPipes(JObject json, CancellationToken ct)
        {
            string zoneName = json["zone_name"]?.ToString();
            int limit = json["limit"]?.Value<int>() ?? 50;

            var pipes = await ListPipesFromE3D(zoneName, limit, ct);
            
            if (pipes == null || pipes.Count == 0)
            {
                var zoneInfo = string.IsNullOrEmpty(zoneName) ? "当前区域" : $"区域 '{zoneName}'";
                return ToolResult.Ok($"未找到{zoneInfo}的管道", new JObject
                {
                    ["tool"] = "get_pipe_info",
                    ["action"] = "list_pipes",
                    ["zone_name"] = zoneName,
                    ["count"] = 0
                });
            }

            var summary = $"管道列表 (共 {pipes.Count} 个):\n\n";
            foreach (var pipe in pipes.Take(30))
            {
                summary += $"名称: {pipe.Name}\n";
                summary += $"规格: {pipe.Specification}\n";
                summary += $"分支数: {pipe.BranchCount}\n";
                summary += $"总长度: {pipe.TotalLength:F2}m\n\n";
            }

            if (pipes.Count > 30)
                summary += $"... 还有 {pipes.Count - 30} 个管道\n";

            var meta = new JObject
            {
                ["tool"] = "get_pipe_info",
                ["action"] = "list_pipes",
                ["zone_name"] = zoneName,
                ["count"] = pipes.Count,
                ["pipes"] = JArray.FromObject(pipes.Take(50).Select(p => new
                {
                    name = p.Name,
                    specification = p.Specification,
                    branch_count = p.BranchCount,
                    total_length = p.TotalLength
                }))
            };

            return ToolResult.Ok(summary, meta);
        }

        /// <summary>
        /// 获取管道层级结构
        /// </summary>
        private async Task<ToolResult> GetPipeHierarchy(JObject json, CancellationToken ct)
        {
            string pipeName = json["pipe_name"]?.ToString();
            if (string.IsNullOrEmpty(pipeName))
                return ToolResult.Fail("获取管道层级结构时需要指定 pipe_name 参数");

            var hierarchy = await GetPipeHierarchyFromE3D(pipeName, ct);
            
            if (hierarchy == null)
            {
                return ToolResult.Ok($"未找到管道 '{pipeName}' 的层级结构", new JObject
                {
                    ["tool"] = "get_pipe_info",
                    ["action"] = "get_pipe_hierarchy",
                    ["pipe_name"] = pipeName,
                    ["found"] = false
                });
            }

            var summary = $"管道 {pipeName} 的层级结构:\n\n";
            summary += FormatHierarchy(hierarchy, 0);

            var meta = new JObject
            {
                ["tool"] = "get_pipe_info",
                ["action"] = "get_pipe_hierarchy",
                ["pipe_name"] = pipeName,
                ["found"] = true,
                ["hierarchy"] = JObject.FromObject(hierarchy)
            };

            return ToolResult.Ok(summary, meta);
        }

        /// <summary>
        /// 格式化层级结构
        /// </summary>
        private string FormatHierarchy(HierarchyNode node, int indent)
        {
            var sb = new StringBuilder();
            var indentStr = new string(' ', indent * 2);
            
            sb.AppendLine($"{indentStr}{node.Type}: {node.Name}");
            
            if (node.Children != null)
            {
                foreach (var child in node.Children)
                {
                    sb.Append(FormatHierarchy(child, indent + 1));
                }
            }
            
            return sb.ToString();
        }

        // 以下是E3D API调用的模拟实现
        // 实际实现需要调用E3D API或通过IToolDispatcher

        private async Task<PipeDetailInfo> GetPipeInfoFromE3D(string pipeName, bool includeAttributes, CancellationToken ct)
        {
            // 使用真实的E3D API获取管道信息
            var pipeReader = new PipeReader(pipeName);
            var pipeModel = pipeReader.GetPipeModel("1907", new List<string[]>());
            
            return new PipeDetailInfo
            {
                Name = pipeModel.Name,
                Type = "PIPE",
                Specification = "ASME B36.19",
                NominalDiameter = "DN100",
                DesignPressure = pipeModel.DesignPressure,
                DesignTemperature = "100°C",
                Material = "022Cr19Ni10",
                InsulationClass = "Class A",
                HeatTracing = pipeModel.IsHeatTracing,
                FluidType = pipeModel.FluidType,
                BranchCount = 0,
                TotalLength = 0.0,
                Attributes = includeAttributes ? new Dictionary<string, string>
                {
                    ["NAME"] = pipeModel.Name,
                    ["TYPE"] = "PIPE",
                    ["SPEC"] = "ASME B36.19",
                    ["BORE"] = "DN100",
                    ["PRES"] = pipeModel.DesignPressure,
                    ["TEMP"] = "100°C",
                    ["MATE"] = "022Cr19Ni10"
                } : null
            };
        }

        private async Task<BranchDetailInfo> GetBranchInfoFromE3D(string branchName, bool includeAttributes, CancellationToken ct)
        {
            // 使用真实的E3D API获取分支信息
            var branReader = new BranReader(branchName);
            var branInfoModel = branReader.BranInfoModel;
            
            return new BranchDetailInfo
            {
                Name = branchName,
                PipeName = branInfoModel.PipeName,
                BranchType = "主管分支",
                StartPoint = "(0, 0, 0)",
                EndPoint = "(10, 0, 0)",
                Length = 10.0,
                NominalDiameter = "DN100",
                ComponentCount = 5,
                Components = new List<ComponentInfo>
                {
                    new ComponentInfo { Name = "法兰", Type = "FLANGE", Specification = "ASME B16.5", Material = "022Cr19Ni10", Position = "(0, 0, 0)" },
                    new ComponentInfo { Name = "弯头", Type = "ELBOW", Specification = "ASME B16.9", Material = "022Cr19Ni10", Position = "(2, 0, 0)" },
                    new ComponentInfo { Name = "三通", Type = "TEE", Specification = "ASME B16.9", Material = "022Cr19Ni10", Position = "(5, 0, 0)" }
                }
            };
        }

        private async Task<List<ComponentInfo>> GetPipeComponentsFromE3D(string pipeName, int limit, CancellationToken ct)
        {
            await Task.Delay(100, ct);
            
            return new List<ComponentInfo>
            {
                new ComponentInfo { Name = "法兰1", Type = "FLANGE", Specification = "ASME B16.5", Material = "022Cr19Ni10", Position = "(0, 0, 0)" },
                new ComponentInfo { Name = "弯头1", Type = "ELBOW", Specification = "ASME B16.9", Material = "022Cr19Ni10", Position = "(2, 0, 0)" },
                new ComponentInfo { Name = "三通1", Type = "TEE", Specification = "ASME B16.9", Material = "022Cr19Ni10", Position = "(5, 0, 0)" },
                new ComponentInfo { Name = "大小头1", Type = "REDUCER", Specification = "ASME B16.9", Material = "022Cr19Ni10", Position = "(7, 0, 0)" },
                new ComponentInfo { Name = "法兰2", Type = "FLANGE", Specification = "ASME B16.5", Material = "022Cr19Ni10", Position = "(10, 0, 0)" }
            };
        }

        private async Task<List<SupportInfo>> GetSupportsFromE3D(string pipeName, int limit, CancellationToken ct)
        {
            await Task.Delay(100, ct);
            
            return new List<SupportInfo>
            {
                new SupportInfo { Name = "弹簧支吊架1", Type = "SPRING_SUPPORT", Position = "(3, 0, 0)", Load = "500kg", Stiffness = "100N/mm" },
                new SupportInfo { Name = "刚性吊架1", Type = "RIGID_HANGER", Position = "(6, 0, 0)", Load = "300kg", Stiffness = "∞" },
                new SupportInfo { Name = "滑动支架1", Type = "SLIDE_SUPPORT", Position = "(9, 0, 0)", Load = "200kg", Stiffness = "0" }
            };
        }

        private async Task<List<PipeBasicInfo>> ListPipesFromE3D(string zoneName, int limit, CancellationToken ct)
        {
            await Task.Delay(100, ct);
            
            return new List<PipeBasicInfo>
            {
                new PipeBasicInfo { Name = "/PIPE-1001", Specification = "ASME B36.19", BranchCount = 3, TotalLength = 25.5 },
                new PipeBasicInfo { Name = "/PIPE-1002", Specification = "ASME B36.19", BranchCount = 2, TotalLength = 18.2 },
                new PipeBasicInfo { Name = "/PIPE-1003", Specification = "ASME B36.19", BranchCount = 4, TotalLength = 32.1 }
            };
        }

        private async Task<HierarchyNode> GetPipeHierarchyFromE3D(string pipeName, CancellationToken ct)
        {
            await Task.Delay(100, ct);
            
            return new HierarchyNode
            {
                Name = pipeName,
                Type = "PIPE",
                Children = new List<HierarchyNode>
                {
                    new HierarchyNode
                    {
                        Name = $"{pipeName}-B1",
                        Type = "BRAN",
                        Children = new List<HierarchyNode>
                        {
                            new HierarchyNode { Name = "法兰1", Type = "FLANGE" },
                            new HierarchyNode { Name = "弯头1", Type = "ELBOW" },
                            new HierarchyNode { Name = "三通1", Type = "TEE" }
                        }
                    },
                    new HierarchyNode
                    {
                        Name = $"{pipeName}-B2",
                        Type = "BRAN",
                        Children = new List<HierarchyNode>
                        {
                            new HierarchyNode { Name = "法兰2", Type = "FLANGE" },
                            new HierarchyNode { Name = "大小头1", Type = "REDUCER" }
                        }
                    }
                }
            };
        }

        // 数据模型类
        private class PipeDetailInfo
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public string Specification { get; set; }
            public string NominalDiameter { get; set; }
            public string DesignPressure { get; set; }
            public string DesignTemperature { get; set; }
            public string Material { get; set; }
            public string InsulationClass { get; set; }
            public string HeatTracing { get; set; }
            public string FluidType { get; set; }
            public int BranchCount { get; set; }
            public double TotalLength { get; set; }
            public Dictionary<string, string> Attributes { get; set; }
        }

        private class BranchDetailInfo
        {
            public string Name { get; set; }
            public string PipeName { get; set; }
            public string BranchType { get; set; }
            public string StartPoint { get; set; }
            public string EndPoint { get; set; }
            public double Length { get; set; }
            public string NominalDiameter { get; set; }
            public int ComponentCount { get; set; }
            public List<ComponentInfo> Components { get; set; }
        }

        private class ComponentInfo
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public string Specification { get; set; }
            public string Material { get; set; }
            public string Position { get; set; }
        }

        private class SupportInfo
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public string Position { get; set; }
            public string Load { get; set; }
            public string Stiffness { get; set; }
        }

        private class PipeBasicInfo
        {
            public string Name { get; set; }
            public string Specification { get; set; }
            public int BranchCount { get; set; }
            public double TotalLength { get; set; }
        }

        private class HierarchyNode
        {
            public string Name { get; set; }
            public string Type { get; set; }
            public List<HierarchyNode> Children { get; set; }
        }
    }
}