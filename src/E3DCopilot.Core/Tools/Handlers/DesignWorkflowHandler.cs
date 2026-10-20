using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Models.Building;
using E3DCopilot.Core.Services.Cad;
using E3DCopilot.Core.Tools.Mcp;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// design_workflow — 设计全流程的确定性编排。
    ///
    /// 为什么需要这个工具：
    /// “解析 → 登记 → 建模 → 回写模型 → 比对版本 → 回写差异”这五六步如果交给 LLM 自由编排，
    /// 演示时会跳步、乱序、参数填错，且平台侧台账会缺记录。这里把顺序固定下来，
    /// LLM 只负责提供「图纸 A、图纸 B、图号」三个输入。
    ///
    /// 设计取舍：
    /// - 不引入新的流程引擎，直接按顺序 await 既有工具（cad_import / dwg_diff / MCP 工具）。
    /// - 平台工具用名称在运行期查找（MCP 工具是异步注册的，构造时还拿不到）。
    /// - 平台不可达时**不中断**：本地步骤照常完成，只在结果里标注平台侧缺失，
    ///   保证演示时断掉平台仍能继续讲。
    /// </summary>
    public class DesignWorkflowHandler : IToolHandler
    {
        private readonly ToolExecutor _executor;
        private readonly IToolDispatcher _dispatcher;

        /// <summary>平台 MCP server 的配置名（见 config.json 的 McpServers[].Name）</summary>
        public string PlatformServerName { get; set; } = "e3d-data-platform";

        public DesignWorkflowHandler(ToolExecutor executor, IToolDispatcher dispatcher = null)
        {
            _executor = executor ?? throw new ArgumentNullException(nameof(executor));
            _dispatcher = dispatcher;
        }

        public string Name => "design_workflow";

        bool IToolHandler.IsReadOnly => false;

        public string Description => @"执行「设计全流程管理」的固定编排，六步顺序执行，不需要模型自行决定调用顺序：
1) 解析并登记 A 版图纸基础信息 → 平台 record_drawing
2) 解析 A 版图纸 → 在 E3D 中建模（cad_import import + auto_execute）
3) 建模结果回写平台 record_model
4) 解析并登记 B 版图纸基础信息 → 平台 record_drawing
5) 比对 A/B 两版几何差异 → dwg_diff
6) 差异清单回写平台 record_diff

每一步的成功/失败都会写入操作留痕 log_operation。
平台侧不可达时不会中断，本地步骤照常完成，结果中标注平台缺失项。

只提供三个必要输入：A 版路径、B 版路径（可选，不填则只走 A 版流程）、图号。";

        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""drawing_no"": {
      ""type"": ""string"",
      ""description"": ""图号，如 JG-2026-001，作为平台台账的主键之一""
    },
    ""file_a"": {
      ""type"": ""string"",
      ""description"": ""A 版（基准版）DWG 图纸绝对路径""
    },
    ""version_a"": {
      ""type"": ""string"",
      ""description"": ""A 版版本号，默认 A""
    },
    ""file_b"": {
      ""type"": ""string"",
      ""description"": ""B 版（改版后）DWG 图纸绝对路径。不填则只执行 A 版流程""
    },
    ""version_b"": {
      ""type"": ""string"",
      ""description"": ""B 版版本号，默认 B""
    },
    ""layers"": {
      ""type"": ""array"",
      ""items"": { ""type"": ""string"" },
      ""description"": ""参与解析/建模/比对的图层名列表。强烈建议指定，否则轴线/标注也会被当成构件""
    },
    ""wall_height"": {
      ""type"": ""number"",
      ""description"": ""默认墙高(mm)，默认 3000""
    },
    ""wall_thickness"": {
      ""type"": ""number"",
      ""description"": ""默认墙厚(mm)，默认 200""
    },
    ""auto_execute"": {
      ""type"": ""boolean"",
      ""description"": ""是否实际在 E3D 中执行建模，默认 true。设为 false 只生成 PML 不执行""
    },
    ""operator_name"": {
      ""type"": ""string"",
      ""description"": ""操作人（写入平台操作留痕）""
    }
  },
  ""required"": [""drawing_no"", ""file_a""]
}";

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            JObject json;
            try { json = JObject.Parse(args ?? "{}"); }
            catch (JsonException ex) { return ToolResult.Fail("参数 JSON 解析错误: " + ex.Message); }

            string drawingNo = json["drawing_no"]?.ToString();
            string fileA = json["file_a"]?.ToString();
            if (string.IsNullOrWhiteSpace(drawingNo) || string.IsNullOrWhiteSpace(fileA))
                return ToolResult.Fail("需要提供 drawing_no 与 file_a");

            string versionA = json["version_a"]?.ToString();
            if (string.IsNullOrWhiteSpace(versionA)) versionA = "A";

            string fileB = json["file_b"]?.ToString();
            string versionB = json["version_b"]?.ToString();
            if (string.IsNullOrWhiteSpace(versionB)) versionB = "B";

            bool hasB = !string.IsNullOrWhiteSpace(fileB);
            bool autoExecute = json["auto_execute"]?.Value<bool>() ?? true;
            string operatorName = json["operator_name"]?.ToString();
            var layers = (json["layers"] as JArray)?
                .Select(t => t?.ToString())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .ToList();

            var ctx = new WorkflowContext();
            var log = ctx.Log;
            var steps = ctx.Steps;

            log.AppendLine("🏗 设计全流程编排");
            log.AppendLine(new string('=', 50));
            log.AppendLine("图号: " + drawingNo + " | A 版: " + versionA +
                           (hasB ? " | B 版: " + versionB : " | 仅 A 版"));
            if (layers != null && layers.Count > 0)
                log.AppendLine("图层过滤: " + string.Join(", ", layers));
            log.AppendLine();

            // ── 步骤 1：解析并登记 A 版图纸 ──
            var parseA = RunParseStep(fileA, layers);
            RecordStep(steps, 1, "解析 A 版图纸", parseA.Success, parseA.Summary);
            AppendStepLog(log, 1, "解析 A 版图纸", parseA.Success, parseA.Summary);

            bool ok = await PlatformRecordDrawingAsync(drawingNo, versionA, fileA,
                parseA, operatorName, ctx, ct);

            if (!parseA.Success)
            {
                log.AppendLine("❌ A 版解析失败，流程终止（后续步骤依赖图纸几何）");
                log.AppendLine("   原因: " + parseA.Error);
                return BuildResult(log, steps, false, "A 版图纸解析失败: " + parseA.Error);
            }

            // ── 步骤 2：A 版建模 ──
            var buildA = await BuildModelAsync(fileA, layers, json, autoExecute, ct);
            RecordStep(steps, 2, "A 版建模", buildA.Success, buildA.Summary);
            AppendStepLog(log, 2, "A 版建模", buildA.Success, buildA.Summary);
            await LogOperationAsync(drawingNo, versionA, "build_model", buildA.Summary,
                operatorName, buildA.Success ? "success" : "failed", ctx, ct);

            if (!buildA.Success)
                log.AppendLine("⚠ A 版建模未完成，继续执行后续登记与比对");

            // ── 步骤 3：模型清单回写平台 ──
            if (buildA.Success)
            {
                await PlatformRecordModelAsync(drawingNo, versionA, buildA, ctx, ct);
            }

            // ── 无 B 版：到此结束 ──
            if (!hasB)
            {
                log.AppendLine();
                log.AppendLine("已按 A 版单版流程完成。提供 file_b 可继续比对改版差异。");
                return BuildResult(log, steps, ok, null);
            }

            // ── 步骤 4：解析并登记 B 版图纸 ──
            var parseB = RunParseStep(fileB, layers);
            RecordStep(steps, 4, "解析 B 版图纸", parseB.Success, parseB.Summary);
            AppendStepLog(log, 4, "解析 B 版图纸", parseB.Success, parseB.Summary);
            await PlatformRecordDrawingAsync(drawingNo, versionB, fileB,
                parseB, operatorName, ctx, ct);

            if (!parseB.Success)
            {
                log.AppendLine("❌ B 版解析失败，跳过差异比对");
                log.AppendLine("   原因: " + parseB.Error);
                return BuildResult(log, steps, ok, "B 版图纸解析失败: " + parseB.Error);
            }

            // ── 步骤 5：A/B 差异比对 ──
            var diff = await RunDiffAsync(fileA, fileB, layers, ct);
            RecordStep(steps, 5, "A/B 版本差异比对", diff.Success, diff.Summary);
            AppendStepLog(log, 5, "A/B 版本差异比对", diff.Success, diff.Summary);
            await LogOperationAsync(drawingNo, versionB, "diff_version", diff.Summary,
                operatorName, diff.Success ? "success" : "failed", ctx, ct);

            if (diff.Success)
            {
                // ── 步骤 6：差异清单回写平台 ──
                await PlatformRecordDiffAsync(drawingNo, versionA, versionB, diff, ctx, ct);
            }

            log.AppendLine();
            log.AppendLine(ctx.Failures == 0
                ? "✅ 全流程完成，平台记录已同步。"
                : "⚠ 全流程本地步骤已完成；平台侧有 " + ctx.Failures + " 项未写入（见上方标记）。");

            return BuildResult(log, steps, ok, null);
        }

        // ─────────────────────────── 步骤实现 ───────────────────────────

        /// <summary>用 sidecar 解析图纸，拿基础统计（图层数/实体数/线段数）。</summary>
        private ParseStepResult RunParseStep(string filePath, List<string> layers)
        {
            var r = new ParseStepResult();
            try
            {
                var parser = new SidecarCadParserService();
                if (layers != null && layers.Count > 0) parser.Layers = layers;

                var parsed = parser.ParseFile(filePath);
                if (!parsed.Success)
                {
                    r.Error = parsed.Error;
                    r.Summary = "解析失败: " + parsed.Error;
                    return r;
                }

                r.Success = true;
                r.LayerCount = parsed.Layers?.Count ?? 0;
                r.EntityCount = parsed.TotalEntities;
                r.SegmentCount = parsed.WallSegments?.Count ?? 0;
                r.Summary = "图层 " + r.LayerCount + " 个 / 实体 " + r.EntityCount +
                            " 个 / 有效线段 " + r.SegmentCount + " 条（耗时 " +
                            parsed.ParseDuration.TotalMilliseconds.ToString("F0") + "ms）";
                return r;
            }
            catch (Exception ex)
            {
                r.Error = ex.Message;
                r.Summary = "解析异常: " + ex.Message;
                return r;
            }
        }

        /// <summary>调用 cad_import 建模。</summary>
        private async Task<BuildStepResult> BuildModelAsync(string filePath, List<string> layers,
            JObject rootJson, bool autoExecute, CancellationToken ct)
        {
            var r = new BuildStepResult();
            try
            {
                var call = new JObject
                {
                    ["action"] = "import",
                    ["file_path"] = filePath,
                    ["auto_execute"] = autoExecute
                };
                if (layers != null && layers.Count > 0) call["layers"] = new JArray(layers);
                if (rootJson["wall_height"] != null) call["wall_height"] = rootJson["wall_height"];
                if (rootJson["wall_thickness"] != null) call["wall_thickness"] = rootJson["wall_thickness"];

                var res = await _executor.ExecuteAsync("cad_import", call.ToString(Formatting.None), ct);
                r.Success = res.Success;
                if (!res.Success)
                {
                    r.Error = res.Error;
                    r.Summary = "建模失败: " + res.Error;
                    return r;
                }

                var data = res.Data as JObject;
                if (data == null && res.Data != null)
                    data = JToken.FromObject(res.Data) as JObject;

                r.SegmentCount = data?["segmentCount"]?.Value<int>() ?? 0;
                r.ElementCount = data?["elementCount"]?.Value<int>() ?? 0;
                r.Executed = data?["executed"]?.Value<bool>() ?? autoExecute;
                r.Summary = "线段 " + r.SegmentCount + " 条 → 元素 " + r.ElementCount +
                            " 个，" + (r.Executed ? "已在 E3D 中执行" : "仅生成 PML 未执行");
                return r;
            }
            catch (Exception ex)
            {
                r.Error = ex.Message;
                r.Summary = "建模异常: " + ex.Message;
                return r;
            }
        }

        /// <summary>调用 dwg_diff 比对两版图纸。</summary>
        private async Task<DiffStepResult> RunDiffAsync(string fileA, string fileB,
            List<string> layers, CancellationToken ct)
        {
            var r = new DiffStepResult();
            try
            {
                var call = new JObject { ["file_a"] = fileA, ["file_b"] = fileB };
                if (layers != null && layers.Count > 0) call["layers"] = new JArray(layers);

                var res = await _executor.ExecuteAsync("dwg_diff", call.ToString(Formatting.None), ct);
                r.Success = res.Success;
                if (!res.Success)
                {
                    r.Error = res.Error;
                    r.Summary = "比对失败: " + res.Error;
                    return r;
                }

                var data = res.Data as JObject;
                if (data == null && res.Data != null)
                    data = JToken.FromObject(res.Data) as JObject;

                r.AddedCount = data?["addedCount"]?.Value<int>() ?? 0;
                r.RemovedCount = data?["removedCount"]?.Value<int>() ?? 0;
                r.ModifiedCount = data?["modifiedCount"]?.Value<int>() ?? 0;
                r.UnchangedCount = data?["unchangedCount"]?.Value<int>() ?? 0;
                r.Tolerance = data?["tolerance"]?.Value<double>() ?? 0;
                r.Records = BuildDiffRecords(data);
                r.Summary = "新增 " + r.AddedCount + " / 删除 " + r.RemovedCount +
                            " / 修改 " + r.ModifiedCount + "，未变 " + r.UnchangedCount +
                            "（容差 " + r.Tolerance.ToString("F0") + "mm）";
                return r;
            }
            catch (Exception ex)
            {
                r.Error = ex.Message;
                r.Summary = "比对异常: " + ex.Message;
                return r;
            }
        }

        /// <summary>把 dwg_diff 的三张清单压平成平台要求的 records[]（changeType 为 added/removed/modified）。</summary>
        private static JArray BuildDiffRecords(JObject data)
        {
            var records = new JArray();
            if (data == null) return records;

            AppendRecords(records, data["added"] as JArray, "added");
            AppendRecords(records, data["removed"] as JArray, "removed");
            AppendRecords(records, data["modified"] as JArray, "modified");
            return records;
        }

        private static void AppendRecords(JArray target, JArray source, string changeType)
        {
            if (source == null) return;
            foreach (var item in source)
            {
                var obj = item as JObject;
                if (obj == null) continue;
                // 浅拷贝，避免修改 dwg_diff 的返回对象
                var copy = new JObject(obj) { ["changeType"] = changeType };
                target.Add(copy);
            }
        }

        // ─────────────────────── 平台回写（MCP） ───────────────────────

        private async Task<bool> PlatformRecordDrawingAsync(string drawingNo, string version,
            string filePath, ParseStepResult parse, string operatorName,
            WorkflowContext ctx, CancellationToken ct)
        {
            var call = new JObject
            {
                ["drawingNo"] = drawingNo,
                ["version"] = version,
                ["filePath"] = filePath,
                ["layerCount"] = parse.LayerCount,
                ["entityCount"] = parse.EntityCount
            };
            if (!string.IsNullOrWhiteSpace(operatorName)) call["submittedBy"] = operatorName;

            bool ok = await CallPlatformAsync("record_drawing", call,
                "登记 " + version + " 版图纸台账", ctx, ct);

            if (ok)
            {
                await LogOperationAsync(drawingNo, version, "parse_drawing", parse.Summary,
                    operatorName, "success", ctx, ct);
            }
            return ok;
        }

        private async Task PlatformRecordModelAsync(string drawingNo, string version,
            BuildStepResult build, WorkflowContext ctx,
            CancellationToken ct)
        {
            var components = new JArray
            {
                new JObject
                {
                    ["type"] = "构件",
                    ["count"] = build.ElementCount,
                    ["note"] = "由 cad_import 从图纸线段生成"
                }
            };

            var call = new JObject
            {
                ["drawingNo"] = drawingNo,
                ["version"] = version,
                ["modelName"] = "IMPORT_ZONE",
                ["components"] = components
            };

            await CallPlatformAsync("record_model", call,
                "回写 " + version + " 版模型清单", ctx, ct);
        }

        private async Task PlatformRecordDiffAsync(string drawingNo, string versionA,
            string versionB, DiffStepResult diff, WorkflowContext ctx,
            CancellationToken ct)
        {
            var call = new JObject
            {
                ["drawingNo"] = drawingNo,
                ["versionA"] = versionA,
                ["versionB"] = versionB,
                ["tol"] = diff.Tolerance,
                ["records"] = diff.Records
            };

            await CallPlatformAsync("record_diff", call,
                "回写 " + versionA + "→" + versionB + " 差异清单", ctx, ct);
        }

        private async Task LogOperationAsync(string drawingNo, string version, string operation,
            string detail, string operatorName, string result,
            WorkflowContext ctx, CancellationToken ct)
        {
            var call = new JObject
            {
                ["drawingNo"] = drawingNo,
                ["version"] = version,
                ["operation"] = operation,
                ["detail"] = detail,
                ["result"] = result
            };
            if (!string.IsNullOrWhiteSpace(operatorName)) call["operator"] = operatorName;

            await CallPlatformAsync("log_operation", call,
                "留痕 " + operation, ctx, ct, quiet: true);
        }

        /// <summary>
        /// 调用平台 MCP 工具。找不到 MCP 工具（平台未启动/握手未完成）时不抛异常，
        /// 只在日志里标注，保证本地流程能继续。
        ///
        /// 名称解析用前缀匹配而不是精确拼接：
        /// McpToolNaming.NormalizeName 对含非法字符的名称会追加哈希后缀，
        /// "e3d-data-platform" 会被规范化成 "e3d-data-platform"（连字符是合法字符），
        /// 但为保证兼容任何命名规则，这里按 "mcp__" + server 前缀查找，再按 raw 名匹配后缀。
        /// </summary>
        private async Task<bool> CallPlatformAsync(string rawToolName, JObject args,
            string label, WorkflowContext ctx,
            CancellationToken ct, bool quiet = false)
        {
            var handler = ResolvePlatformTool(rawToolName);
            if (handler == null)
            {
                ctx.Failures++;
                ctx.Log.AppendLine("  ⚠ 平台未就绪，跳过「" + label + "」(" +
                                   McpToolNaming.ToolName(PlatformServerName, rawToolName) + " 未注册)");
                if (!quiet)
                {
                    ctx.Steps.Add(new JObject
                    {
                        ["step"] = label,
                        ["success"] = false,
                        ["summary"] = "平台工具未注册"
                    });
                }
                return false;
            }

            try
            {
                var res = await handler.ExecuteAsync(args.ToString(Formatting.None), ct);
                if (res.Success)
                {
                    ctx.Log.AppendLine("  ✅ " + label);
                    if (!quiet)
                    {
                        ctx.Steps.Add(new JObject
                        {
                            ["step"] = label,
                            ["success"] = true,
                            ["summary"] = FirstLine(res.Text)
                        });
                    }
                    return true;
                }

                ctx.Failures++;
                ctx.Log.AppendLine("  ⚠ " + label + " 失败: " + res.Error);
                if (!quiet)
                {
                    ctx.Steps.Add(new JObject
                    {
                        ["step"] = label,
                        ["success"] = false,
                        ["summary"] = res.Error
                    });
                }
                return false;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                ctx.Failures++;
                ctx.Log.AppendLine("  ⚠ " + label + " 异常: " + ex.Message);
                if (!quiet)
                {
                    ctx.Steps.Add(new JObject
                    {
                        ["step"] = label,
                        ["success"] = false,
                        ["summary"] = ex.Message
                    });
                }
                return false;
            }
        }

        // ─────────────────────────── 辅助 ───────────────────────────

        /// <summary>
        /// 按「MCP 命名空间前缀 + 原始工具名」查找平台工具。
        /// 先试标准拼接名，再按 server 前缀枚举，避免命名规范化规则变化导致查不到。
        /// </summary>
        private IToolHandler ResolvePlatformTool(string rawToolName)
        {
            var exact = _executor.GetHandler(McpToolNaming.ToolName(PlatformServerName, rawToolName));
            if (exact != null) return exact;

            string prefix = "mcp__";
            foreach (var h in _executor.GetAllHandlers())
            {
                if (h.Name == null || !h.Name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    continue;
                // 末尾按原始工具名匹配：mcp__xxx__record_drawing
                if (h.Name.EndsWith("__" + rawToolName, StringComparison.OrdinalIgnoreCase))
                    return h;
            }
            return null;
        }

        /// <summary>把步骤结果同时写进人读日志（Data 里的 steps 只给前端/程序用）。</summary>
        private static void AppendStepLog(StringBuilder log, int index, string name, bool success, string summary)
        {
            log.AppendLine((success ? "  ✅ " : "  ❌ ") + "[" + index + "] " + name + " — " + summary);
        }

        private static void RecordStep(JArray steps, int index, string name, bool success, string summary)
        {
            steps.Add(new JObject
            {
                ["step"] = index,
                ["name"] = name,
                ["success"] = success,
                ["summary"] = summary
            });
        }

        private static string FirstLine(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            int i = text.IndexOf('\n');
            return (i < 0 ? text : text.Substring(0, i)).Trim();
        }

        private static ToolResult BuildResult(StringBuilder log, JArray steps, bool platformOk, string error)
        {
            var data = new
            {
                platformSynced = platformOk,
                steps
            };

            if (!string.IsNullOrEmpty(error))
                return new ToolResult { Success = false, Error = error, Text = log.ToString(), Data = data };

            return ToolResult.Ok(log.ToString(), data);
        }

        // ─────────────────────────── 结果类型 ───────────────────────────

        /// <summary>
        /// 编排过程中的可变状态。async 方法不能带 ref 参数，
        /// 因此把「日志 + 步骤清单 + 平台失败计数」收进一个上下文对象。
        /// </summary>
        private class WorkflowContext
        {
            public readonly StringBuilder Log = new StringBuilder();
            public readonly JArray Steps = new JArray();
            public int Failures;
        }

        private class ParseStepResult
        {
            public bool Success;
            public string Error;
            public string Summary;
            public int LayerCount;
            public int EntityCount;
            public int SegmentCount;
        }

        private class BuildStepResult
        {
            public bool Success;
            public string Error;
            public string Summary;
            public int SegmentCount;
            public int ElementCount;
            public bool Executed;
        }

        private class DiffStepResult
        {
            public bool Success;
            public string Error;
            public string Summary;
            public int AddedCount;
            public int RemovedCount;
            public int ModifiedCount;
            public int UnchangedCount;
            public double Tolerance;
            public JArray Records;
        }
    }
}
