using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Models.Building;
using E3DCopilot.Core.Services.Cad;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// 两版 DWG 图纸的几何差异比对。
    ///
    /// 只做确定性比对：几何 + 图层，不做语义判断（不猜"这根线是不是那堵墙改造的"）。
    /// 比对口径见 DwgDiffService。
    ///
    /// 为什么单独成一个工具而不是塞进 cad_import：
    /// 差异比对是「设计全流程管理」的核心环节，需要把结果以结构化形式交给
    /// 数据管理平台（MCP 的 record_diff），与建模是两条独立的链。
    /// </summary>
    public class DwgDiffHandler : IToolHandler
    {
        public string Name => "dwg_diff";

        bool IToolHandler.IsReadOnly => true;

        public string Description => @"比对两版 DWG 图纸的几何差异，输出新增/删除/修改三张清单。
用独立进程 dwg-sidecar 读取全量真实几何（不按图层名猜构件），再按「图层 + 归一化端点」确定性匹配。
仅做几何比对，不做语义推断：匹配不上就如实归入新增或删除。

使用前提：两版图纸的原点和比例必须一致，否则结果会是「全删全增」。
建议先用 layers 参数限定参与比对的图层（如 [""土建"",""C1 柱墙剖线""]），
否则轴线、标注、文字图层也会被计入。";

        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""file_a"": {
      ""type"": ""string"",
      ""description"": ""A 版（基准/旧版）图纸绝对路径""
    },
    ""file_b"": {
      ""type"": ""string"",
      ""description"": ""B 版（目标/新版）图纸绝对路径""
    },
    ""tolerance"": {
      ""type"": ""number"",
      ""description"": ""几何匹配容差(mm)，默认 5。同版本重出图用 5，跨软件导出建议 10~50""
    },
    ""layers"": {
      ""type"": ""array"",
      ""items"": { ""type"": ""string"" },
      ""description"": ""参与比对的图层名列表。不填则比对全部图层（轴线/标注会被计入）""
    }
  },
  ""required"": [""file_a"", ""file_b""]
}";

        public Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                var json = JObject.Parse(args ?? "{}");
                string fileA = json["file_a"]?.ToString();
                string fileB = json["file_b"]?.ToString();

                if (string.IsNullOrWhiteSpace(fileA) || string.IsNullOrWhiteSpace(fileB))
                    return Task.FromResult(ToolResult.Fail("需要提供 file_a 和 file_b 两个图纸路径"));

                string envError = null;
                var segA = Parse(fileA, json, out envError);
                if (segA == null) return Task.FromResult(ToolResult.Fail(envError));

                var segB = Parse(fileB, json, out envError);
                if (segB == null) return Task.FromResult(ToolResult.Fail(envError));

                var service = new DwgDiffService();
                double tol = json["tolerance"]?.Value<double>() ?? 5;
                if (tol > 0) service.Tolerance = tol;

                var diff = service.Compare(segA, segB);

                var sb = new StringBuilder();
                sb.AppendLine("📐 图纸版本差异比对");
                sb.AppendLine(new string('=', 50));
                sb.AppendLine("A 版: " + fileA);
                sb.AppendLine("B 版: " + fileB);
                sb.AppendLine("A 版线段: " + segA.Count + " 条 / B 版线段: " + segB.Count + " 条");
                sb.AppendLine();
                sb.Append(DwgDiffService.FormatReport(diff));

                return Task.FromResult(ToolResult.Ok(sb.ToString(), new
                {
                    fileA,
                    fileB,
                    segmentCountA = segA.Count,
                    segmentCountB = segB.Count,
                    tolerance = diff.Tolerance,
                    unchangedCount = diff.UnchangedCount,
                    addedCount = diff.Added.Count,
                    removedCount = diff.Removed.Count,
                    modifiedCount = diff.Modified.Count,
                    added = diff.Added,
                    removed = diff.Removed,
                    modified = diff.Modified
                }));
            }
            catch (JsonException ex)
            {
                return Task.FromResult(ToolResult.Fail("参数 JSON 解析错误: " + ex.Message));
            }
            catch (Exception ex)
            {
                return Task.FromResult(ToolResult.Fail("图纸比对失败: " + ex.Message));
            }
        }

        /// <summary>解析单张图纸；失败时返回 null 并给出可用于提示的错误文本。</summary>
        private static List<LineSegment> Parse(string filePath, JObject json, out string error)
        {
            error = null;

            var parser = new SidecarCadParserService();

            // layers 过滤：只有显式给了非空列表才启用，避免"全部图层"被误当成过滤条件
            var layersToken = json["layers"] as JArray;
            if (layersToken != null && layersToken.Count > 0)
            {
                parser.Layers = layersToken
                    .Select(t => t?.ToString())
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .ToList();
            }

            var result = parser.ParseFile(filePath);
            if (!result.Success)
            {
                error = "解析失败: " + filePath + " → " + result.Error;
                return null;
            }
            if (result.WallSegments == null || result.WallSegments.Count == 0)
            {
                error = "解析结果为空: " + filePath +
                        "（检查图层过滤条件是否命中，或图纸是否为纯块/外部参照）";
                return null;
            }
            return result.WallSegments;
        }
    }
}
