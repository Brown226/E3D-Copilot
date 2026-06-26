using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// 材料查询工具 - 封装CNPE.IC.ISO的材料库查询功能
    /// 支持查询管道材料、螺栓、支吊架等编码信息
    /// </summary>
    public class MaterialQueryHandler : IToolHandler
    {
        private readonly IToolDispatcher _dispatcher;
        private static Dictionary<string, List<MaterialItem>> _materialCache = new Dictionary<string, List<MaterialItem>>();
        private static readonly object _cacheLock = new object();

        public MaterialQueryHandler(IToolDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public string Name => "query_material";
        public string Description => "查询管道材料编码信息，包括管道、管件、螺栓、支吊架等材料规格和编码。支持按编码、类型、规格等条件查询。";
        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""action"": {
      ""type"": ""string"",
      ""enum"": [""search"", ""get_by_code"", ""get_by_type"", ""list_types"", ""list_projects""],
      ""description"": ""操作类型：search-搜索材料，get_by_code-按编码查询，get_by_type-按类型查询，list_types-列出材料类型，list_projects-列出支持的项目""
    },
    ""keyword"": {
      ""type"": ""string"",
      ""description"": ""搜索关键词，支持编码、名称、规格等""
    },
    ""material_code"": {
      ""type"": ""string"",
      ""description"": ""材料编码，如 SPC00025""
    },
    ""material_type"": {
      ""type"": ""string"",
      ""enum"": [""PIPE"", ""BOLT"", ""SCTN"", ""SUPP""],
      ""description"": ""材料类型：PIPE-管道材料，BOLT-螺栓，SCTN-型钢，SUPP-支吊架""
    },
    ""project_id"": {
      ""type"": ""string"",
      ""enum"": [""1907"", ""1916"", ""2016"", ""2026""],
      ""description"": ""项目编号，不同项目有不同的材料规格""
    },
    ""limit"": {
      ""type"": ""integer"",
      ""description"": ""返回结果数量限制，默认20""
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
                    case "search":
                        return await SearchMaterials(json, ct);
                    case "get_by_code":
                        return await GetMaterialByCode(json, ct);
                    case "get_by_type":
                        return await GetMaterialsByType(json, ct);
                    case "list_types":
                        return ListMaterialTypes(json);
                    case "list_projects":
                        return ListProjects(json);
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
                return ToolResult.Fail($"材料查询失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 搜索材料
        /// </summary>
        private async Task<ToolResult> SearchMaterials(JObject json, CancellationToken ct)
        {
            string keyword = json["keyword"]?.ToString();
            if (string.IsNullOrEmpty(keyword))
                return ToolResult.Fail("搜索时需要指定 keyword 参数");

            string projectId = json["project_id"]?.ToString() ?? "1907";
            int limit = json["limit"]?.Value<int>() ?? 20;

            var materials = await LoadMaterialsAsync(projectId, ct);
            var results = materials
                .Where(m => m.Code.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                           m.Name.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                           m.Specification.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0 ||
                           m.Material.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                .Take(limit)
                .ToList();

            if (results.Count == 0)
            {
                return ToolResult.Ok($"未找到匹配 '{keyword}' 的材料信息", new JObject
                {
                    ["tool"] = "query_material",
                    ["action"] = "search",
                    ["keyword"] = keyword,
                    ["project_id"] = projectId,
                    ["count"] = 0
                });
            }

            var summary = $"找到 {results.Count} 条匹配 '{keyword}' 的材料信息:\n\n";
            foreach (var item in results.Take(10))
            {
                summary += $"编码: {item.Code}\n";
                summary += $"名称: {item.Name}\n";
                summary += $"类型: {item.Type}\n";
                summary += $"规格: {item.Specification}\n";
                summary += $"材料: {item.Material}\n";
                summary += $"压力等级: {item.PressureRating}\n";
                summary += $"公称直径: {item.NominalDiameter}\n\n";
            }

            if (results.Count > 10)
                summary += $"... 还有 {results.Count - 10} 条结果\n";

            var meta = new JObject
            {
                ["tool"] = "query_material",
                ["action"] = "search",
                ["keyword"] = keyword,
                ["project_id"] = projectId,
                ["count"] = results.Count,
                ["results"] = JArray.FromObject(results.Take(20).Select(m => new
                {
                    code = m.Code,
                    name = m.Name,
                    type = m.Type,
                    specification = m.Specification,
                    material = m.Material,
                    pressure_rating = m.PressureRating,
                    nominal_diameter = m.NominalDiameter
                }))
            };

            return ToolResult.Ok(summary, meta);
        }

        /// <summary>
        /// 按编码查询材料
        /// </summary>
        private async Task<ToolResult> GetMaterialByCode(JObject json, CancellationToken ct)
        {
            string materialCode = json["material_code"]?.ToString();
            if (string.IsNullOrEmpty(materialCode))
                return ToolResult.Fail("按编码查询时需要指定 material_code 参数");

            string projectId = json["project_id"]?.ToString() ?? "1907";

            var materials = await LoadMaterialsAsync(projectId, ct);
            var material = materials.FirstOrDefault(m => 
                m.Code.Equals(materialCode, StringComparison.OrdinalIgnoreCase));

            if (material == null)
            {
                return ToolResult.Ok($"未找到编码为 '{materialCode}' 的材料", new JObject
                {
                    ["tool"] = "query_material",
                    ["action"] = "get_by_code",
                    ["material_code"] = materialCode,
                    ["project_id"] = projectId,
                    ["found"] = false
                });
            }

            var summary = $"材料编码 {materialCode} 的详细信息:\n\n" +
                         $"编码: {material.Code}\n" +
                         $"名称: {material.Name}\n" +
                         $"英文名: {material.EnglishName}\n" +
                         $"类型: {material.Type}\n" +
                         $"压力等级: {material.PressureRating}\n" +
                         $"制造形式: {material.ManufacturingForm}\n" +
                         $"连接形式: {material.ConnectionForm}\n" +
                         $"材料牌号: {material.Material}\n" +
                         $"材料标准: {material.MaterialStandard}\n" +
                         $"规格标准: {material.Specification}\n" +
                         $"RCC-M等级: {material.RccmLevel}\n" +
                         $"质保等级: {material.QualityLevel}\n" +
                         $"公称直径: {material.NominalDiameter}\n" +
                         $"外径/Φ: {material.OuterDiameter}\n" +
                         $"单位: {material.Unit}\n" +
                         $"单重: {material.UnitWeight} {material.WeightUnit}\n" +
                         $"备注: {material.Remark}";

            var meta = new JObject
            {
                ["tool"] = "query_material",
                ["action"] = "get_by_code",
                ["material_code"] = materialCode,
                ["project_id"] = projectId,
                ["found"] = true,
                ["material"] = JObject.FromObject(material)
            };

            return ToolResult.Ok(summary, meta);
        }

        /// <summary>
        /// 按类型查询材料
        /// </summary>
        private async Task<ToolResult> GetMaterialsByType(JObject json, CancellationToken ct)
        {
            string materialType = json["material_type"]?.ToString();
            if (string.IsNullOrEmpty(materialType))
                return ToolResult.Fail("按类型查询时需要指定 material_type 参数");

            string projectId = json["project_id"]?.ToString() ?? "1907";
            int limit = json["limit"]?.Value<int>() ?? 50;

            var materials = await LoadMaterialsAsync(projectId, ct);
            var results = materials
                .Where(m => m.Type.Equals(materialType, StringComparison.OrdinalIgnoreCase))
                .Take(limit)
                .ToList();

            if (results.Count == 0)
            {
                return ToolResult.Ok($"未找到类型为 '{materialType}' 的材料", new JObject
                {
                    ["tool"] = "query_material",
                    ["action"] = "get_by_type",
                    ["material_type"] = materialType,
                    ["project_id"] = projectId,
                    ["count"] = 0
                });
            }

            var summary = $"类型为 '{materialType}' 的材料列表 (共 {results.Count} 条):\n\n";
            foreach (var item in results.Take(20))
            {
                summary += $"{item.Code}: {item.Name} - {item.Specification}\n";
            }

            if (results.Count > 20)
                summary += $"\n... 还有 {results.Count - 20} 条结果\n";

            var meta = new JObject
            {
                ["tool"] = "query_material",
                ["action"] = "get_by_type",
                ["material_type"] = materialType,
                ["project_id"] = projectId,
                ["count"] = results.Count,
                ["results"] = JArray.FromObject(results.Take(50).Select(m => new
                {
                    code = m.Code,
                    name = m.Name,
                    specification = m.Specification,
                    material = m.Material,
                    nominal_diameter = m.NominalDiameter
                }))
            };

            return ToolResult.Ok(summary, meta);
        }

        /// <summary>
        /// 列出材料类型
        /// </summary>
        private ToolResult ListMaterialTypes(JObject json)
        {
            var types = new[]
            {
                new { Type = "PIPE", Name = "管道材料", Description = "管道、管件、法兰等" },
                new { Type = "BOLT", Name = "螺栓", Description = "螺栓、螺母、垫片等紧固件" },
                new { Type = "SCTN", Name = "型钢", Description = "角钢、槽钢、工字钢等型钢材料" },
                new { Type = "SUPP", Name = "支吊架", Description = "管道支吊架、弹簧支吊架等" }
            };

            var summary = "支持的材料类型:\n\n";
            foreach (var type in types)
            {
                summary += $"{type.Type}: {type.Name}\n";
                summary += $"  描述: {type.Description}\n\n";
            }

            var meta = new JObject
            {
                ["tool"] = "query_material",
                ["action"] = "list_types",
                ["types"] = JArray.FromObject(types)
            };

            return ToolResult.Ok(summary, meta);
        }

        /// <summary>
        /// 列出支持的项目
        /// </summary>
        private ToolResult ListProjects(JObject json)
        {
            var projects = new[]
            {
                new { Id = "1907", Name = "1907项目", Description = "核电项目1907" },
                new { Id = "1916", Name = "1916项目", Description = "核电项目1916" },
                new { Id = "2016", Name = "2016项目", Description = "核电项目2016" },
                new { Id = "2026", Name = "2026项目", Description = "核电项目2026" }
            };

            var summary = "支持的项目编号:\n\n";
            foreach (var project in projects)
            {
                summary += $"{project.Id}: {project.Name}\n";
                summary += $"  描述: {project.Description}\n\n";
            }

            var meta = new JObject
            {
                ["tool"] = "query_material",
                ["action"] = "list_projects",
                ["projects"] = JArray.FromObject(projects)
            };

            return ToolResult.Ok(summary, meta);
        }

        /// <summary>
        /// 加载材料数据
        /// </summary>
        private async Task<List<MaterialItem>> LoadMaterialsAsync(string projectId, CancellationToken ct)
        {
            lock (_cacheLock)
            {
                if (_materialCache.TryGetValue(projectId, out var cached))
                    return cached;
            }

            return await Task.Run(() =>
            {
                var materials = new List<MaterialItem>();
                
                // 从真实的CSV文件加载材料数据
                string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "lib", "iso");
                
                // 加载管道材料数据
                string pipeCsvPath = Path.Combine(basePath, $"{projectId}", "PIPE.csv");
                if (File.Exists(pipeCsvPath))
                {
                    materials.AddRange(LoadMaterialsFromCsv(pipeCsvPath, "PIPE"));
                }
                
                // 加载螺栓材料数据
                string boltCsvPath = Path.Combine(basePath, $"{projectId}", "BOLT.csv");
                if (File.Exists(boltCsvPath))
                {
                    materials.AddRange(LoadMaterialsFromCsv(boltCsvPath, "BOLT"));
                }
                
                // 加载型钢材料数据
                string sctnCsvPath = Path.Combine(basePath, $"{projectId}", "SCTN.csv");
                if (File.Exists(sctnCsvPath))
                {
                    materials.AddRange(LoadMaterialsFromCsv(sctnCsvPath, "SCTN"));
                }
                
                // 加载支吊架材料数据
                string suppCsvPath = Path.Combine(basePath, $"{projectId}", "SUPP.csv");
                if (File.Exists(suppCsvPath))
                {
                    materials.AddRange(LoadMaterialsFromCsv(suppCsvPath, "SUPP"));
                }

                // 如果没有找到材料数据，返回默认数据
                if (materials.Count == 0)
                {
                    materials.AddRange(GetDefaultMaterials());
                }

                lock (_cacheLock)
                {
                    _materialCache[projectId] = materials;
                }

                return materials;
            }, ct);
        }

        /// <summary>
        /// 从CSV文件加载材料数据
        /// </summary>
        private List<MaterialItem> LoadMaterialsFromCsv(string csvPath, string materialType)
        {
            var materials = new List<MaterialItem>();
            
            try
            {
                var lines = File.ReadAllLines(csvPath);
                if (lines.Length <= 1) return materials;
                
                // 跳过标题行
                for (int i = 1; i < lines.Length; i++)
                {
                    var line = lines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    
                    var parts = ParseCsvLine(line);
                    if (parts.Length < 10) continue;
                    
                    var material = new MaterialItem
                    {
                        Code = parts.Length > 1 ? parts[1].Trim('"') : "",
                        Name = parts.Length > 4 ? parts[4].Trim('"') : "",
                        EnglishName = parts.Length > 3 ? parts[3].Trim('"') : "",
                        Type = materialType,
                        PressureRating = parts.Length > 5 ? parts[5].Trim('"') : "",
                        ManufacturingForm = parts.Length > 6 ? parts[6].Trim('"') : "",
                        ConnectionForm = parts.Length > 7 ? parts[7].Trim('"') : "",
                        Material = parts.Length > 8 ? parts[8].Trim('"') : "",
                        MaterialStandard = parts.Length > 9 ? parts[9].Trim('"') : "",
                        Specification = parts.Length > 10 ? parts[10].Trim('"') : "",
                        RccmLevel = parts.Length > 11 ? parts[11].Trim('"') : "",
                        QualityLevel = parts.Length > 12 ? parts[12].Trim('"') : "",
                        NominalDiameter = parts.Length > 13 ? parts[13].Trim('"') : "",
                        OuterDiameter = parts.Length > 14 ? parts[14].Trim('"') : "",
                        Unit = parts.Length > 15 ? parts[15].Trim('"') : "",
                        UnitWeight = parts.Length > 16 ? parts[16].Trim('"') : "",
                        WeightUnit = parts.Length > 17 ? parts[17].Trim('"') : "",
                        Remark = parts.Length > 18 ? parts[18].Trim('"') : ""
                    };
                    
                    materials.Add(material);
                }
            }
            catch (Exception ex)
            {
                // 记录错误但继续处理
                Console.WriteLine($"加载材料数据失败 {csvPath}: {ex.Message}");
            }
            
            return materials;
        }

        /// <summary>
        /// 解析 CSV 行，支持引号内逗号
        /// </summary>
        private static string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            bool inQuotes = false;
            var sb = new System.Text.StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        sb.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(sb.ToString());
                    sb.Clear();
                }
                else
                {
                    sb.Append(c);
                }
            }
            fields.Add(sb.ToString());
            return fields.ToArray();
        }

        /// <summary>
        /// 获取默认材料数据
        /// </summary>
        private List<MaterialItem> GetDefaultMaterials()
        {
            return new List<MaterialItem>
            {
                new MaterialItem
                {
                    Code = "SPC00025",
                    Name = "异径承插管接头",
                    EnglishName = "RED.COUPLING",
                    Type = "PIPE",
                    PressureRating = "CL6000",
                    ManufacturingForm = "FO",
                    ConnectionForm = "SW",
                    Material = "022Cr19Ni10",
                    MaterialStandard = "CP05T3038",
                    Specification = "ASME B16.11",
                    RccmLevel = "RCC-M 1",
                    QualityLevel = "QA1",
                    NominalDiameter = "3/8\"x1/4\"",
                    OuterDiameter = "",
                    Unit = "个",
                    UnitWeight = "",
                    WeightUnit = "Kg",
                    Remark = ""
                },
                new MaterialItem
                {
                    Code = "SPC00026",
                    Name = "异径承插管接头",
                    EnglishName = "RED.COUPLING",
                    Type = "PIPE",
                    PressureRating = "CL3000",
                    ManufacturingForm = "FO",
                    ConnectionForm = "SW",
                    Material = "022Cr17Ni12Mo2",
                    MaterialStandard = "CP05T3038",
                    Specification = "ASME B16.11",
                    RccmLevel = "RCC-M 2",
                    QualityLevel = "QA1",
                    NominalDiameter = "3/8\"x1/4\"",
                    OuterDiameter = "",
                    Unit = "个",
                    UnitWeight = "",
                    WeightUnit = "Kg",
                    Remark = ""
                }
            };
        }

        /// <summary>
        /// 材料项
        /// </summary>
        private class MaterialItem
        {
            public string Code { get; set; }
            public string Name { get; set; }
            public string EnglishName { get; set; }
            public string Type { get; set; }
            public string PressureRating { get; set; }
            public string ManufacturingForm { get; set; }
            public string ConnectionForm { get; set; }
            public string Material { get; set; }
            public string MaterialStandard { get; set; }
            public string Specification { get; set; }
            public string RccmLevel { get; set; }
            public string QualityLevel { get; set; }
            public string NominalDiameter { get; set; }
            public string OuterDiameter { get; set; }
            public string Unit { get; set; }
            public string UnitWeight { get; set; }
            public string WeightUnit { get; set; }
            public string Remark { get; set; }
        }
    }
}