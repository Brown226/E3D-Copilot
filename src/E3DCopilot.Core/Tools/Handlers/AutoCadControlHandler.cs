using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using E3DCopilot.Core.Models.Geometry;
using E3DCopilot.Core.Services.Cad;
using E3DCopilot.Core.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// AutoCAD 直接控制工具 — 发命令、创建实体、管理图层、标注、保存
    ///
    /// 职责：通过 COM 接口直接操作运行中的 AutoCAD 实例
    /// 不负责导入（那是 autocad 工具的事）
    /// 不管理 AutoCAD 进程启停（用户自行管理）
    ///
    /// 前置条件：AutoCAD 已启动并打开了目标图纸
    /// 工具内部会自动尝试连接（无需先调 autocad(connect)）
    /// </summary>
    public class AutoCadControlHandler : IToolHandler
    {
        public string Name => "autocad_control";
        public bool IsReadOnly => false;

        public string Description => @"直接控制运行中的 AutoCAD — 发命令、创建实体、管理图层、标注、保存图纸。
前置条件：AutoCAD 已启动并打开了目标图纸（工具内部自动连接）。

操作类型：
- send_command: 发送 AutoCAD 命令行字符串（如 '_LINE 100,100 200,200 '）
- create_entity: 创建实体（Line/Circle/Polyline/Arc/Text）
- set_layer: 创建或修改图层（颜色/线型/冻结/锁定）
- add_text: 添加文字标注
- add_dimension: 添加尺寸标注（Aligned/Linear/Angular）
- save_drawing: 保存当前图纸（可另存为）
- get_layers: 获取所有图层名
- get_entity_properties: 获取实体属性（按 Handle）

安全约定：
- 不修改系统图层（0 层和 DEFPOINTS 层）
- 修改前建议先 save_drawing 备份
- 不提供删除实体操作（避免误删）";

        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""action"": {
      ""type"": ""string"",
      ""enum"": [""send_command"", ""create_entity"", ""set_layer"", ""add_text"",
               ""add_dimension"", ""save_drawing"", ""get_layers"", ""get_entity_properties""],
      ""description"": ""操作类型""
    },
    ""command"": {
      ""type"": ""string"",
      ""description"": ""AutoCAD 命令行字符串（send_command 用）。注意：命令参数用空格分隔，末尾自动补空格""
    },
    ""entity_type"": {
      ""type"": ""string"",
      ""enum"": [""Line"", ""Circle"", ""Polyline"", ""Arc"", ""Text""],
      ""description"": ""实体类型（create_entity 用）""
    },
    ""points"": {
      ""type"": ""array"",
      ""items"": {""type"": ""array"", ""items"": {""type"": ""number""}},
      ""description"": ""坐标点数组 [[x,y,z],...]（create_entity/add_dimension 用）""
    },
    ""layer"": {
      ""type"": ""string"",
      ""description"": ""图层名（create_entity/add_text/add_dimension 用，空则用当前图层）""
    },
    ""color"": {
      ""type"": ""integer"",
      ""description"": ""ACI 颜色号 1-256（set_layer 用）""
    },
    ""linetype"": {
      ""type"": ""string"",
      ""description"": ""线型名（set_layer 用）""
    },
    ""text"": {
      ""type"": ""string"",
      ""description"": ""文字内容（add_text 用）""
    },
    ""height"": {
      ""type"": ""number"",
      ""description"": ""文字高度mm（add_text 用，默认 3.0）或半径mm（create_entity Circle 用）""
    },
    ""rotation"": {
      ""type"": ""number"",
      ""description"": ""旋转角度（度，add_text 用，默认 0）""
    },
    ""dim_type"": {
      ""type"": ""string"",
      ""enum"": [""Aligned"", ""Linear"", ""Angular""],
      ""description"": ""标注类型（add_dimension 用，默认 Aligned）""
    },
    ""radius"": {
      ""type"": ""number"",
      ""description"": ""半径mm（create_entity Circle/Arc 用）""
    },
    ""start_angle"": {
      ""type"": ""number"",
      ""description"": ""起始角度（弧度，create_entity Arc 用）""
    },
    ""end_angle"": {
      ""type"": ""number"",
      ""description"": ""终止角度（弧度，create_entity Arc 用）""
    },
    ""file_path"": {
      ""type"": ""string"",
      ""description"": ""保存路径（save_drawing 用，空则保存到原路径）""
    },
    ""element_handle"": {
      ""type"": ""string"",
      ""description"": ""实体 Handle（get_entity_properties 用）""
    },
    ""frozen"": {
      ""type"": ""boolean"",
      ""description"": ""是否冻结（set_layer 用）""
    },
    ""locked"": {
      ""type"": ""boolean"",
      ""description"": ""是否锁定（set_layer 用）""
    }
  },
  ""required"": [""action""]
}";

        private readonly AutoCadComService _cadService;

        public AutoCadControlHandler(IToolDispatcher dispatcher = null)
        {
            _cadService = new AutoCadComService();
        }

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                var json = JObject.Parse(args ?? "{}");
                string action = json["action"]?.ToString()?.ToLower() ?? "";

                // 自动连接检查（如果未连接，尝试连接）
                if (_cadService.Status != AutoCadConnectionStatus.Connected)
                {
                    if (!AutoCadComService.IsAutoCadRunning())
                    {
                        return ToolResult.Fail("AutoCAD 未运行，请先启动 AutoCAD 并打开目标图纸");
                    }
                    if (!_cadService.Connect())
                    {
                        return ToolResult.Fail("连接 AutoCAD 失败，请确保 AutoCAD 正在运行并打开了图纸");
                    }
                }

                switch (action)
                {
                    case "send_command":
                        return HandleSendCommand(json);
                    case "create_entity":
                        return HandleCreateEntity(json);
                    case "set_layer":
                        return HandleSetLayer(json);
                    case "add_text":
                        return HandleAddText(json);
                    case "add_dimension":
                        return HandleAddDimension(json);
                    case "save_drawing":
                        return HandleSaveDrawing(json);
                    case "get_layers":
                        return HandleGetLayers();
                    case "get_entity_properties":
                        return HandleGetEntityProperties(json);
                    default:
                        return ToolResult.Fail($"未知操作: {action}，支持: send_command, create_entity, set_layer, add_text, add_dimension, save_drawing, get_layers, get_entity_properties");
                }
            }
            catch (JsonException ex)
            {
                return ToolResult.Fail($"参数 JSON 解析错误: {ex.Message}");
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"AutoCAD 控制操作失败: {ex.Message}");
            }
        }

        private ToolResult HandleSendCommand(JObject json)
        {
            string command = json["command"]?.ToString();
            if (string.IsNullOrWhiteSpace(command))
                return ToolResult.Fail("command 参数为空（send_command 需要 command 参数）");

            var result = _cadService.SendCommand(command);
            return result.Success
                ? ToolResult.Ok($"✅ 命令已发送: {command}", new { command, handle = result.Handle })
                : ToolResult.Fail(result.Error);
        }

        private ToolResult HandleCreateEntity(JObject json)
        {
            string entityType = json["entity_type"]?.ToString();
            if (string.IsNullOrWhiteSpace(entityType))
                return ToolResult.Fail("entity_type 参数为空（create_entity 需要 entity_type 参数）");

            var points = ParsePoints(json["points"]);
            if (points == null || points.Count == 0)
                return ToolResult.Fail("points 参数为空或格式错误（需要 [[x,y,z],...] 格式）");

            string layer = json["layer"]?.ToString();
            var properties = new Dictionary<string, object>();

            // Circle/Arc 的半径
            if (json["radius"] != null)
                properties["Radius"] = json["radius"].Value<double>();
            else if (json["height"] != null && entityType.Equals("Circle", StringComparison.OrdinalIgnoreCase))
                properties["Radius"] = json["height"].Value<double>();

            // Arc 的角度
            if (json["start_angle"] != null)
                properties["StartAngle"] = json["start_angle"].Value<double>();
            if (json["end_angle"] != null)
                properties["EndAngle"] = json["end_angle"].Value<double>();

            // Text 的内容（create_entity 也支持 Text 类型）
            if (json["text"] != null)
                properties["Text"] = json["text"].ToString();
            if (json["height"] != null && entityType.Equals("Text", StringComparison.OrdinalIgnoreCase))
                properties["Height"] = json["height"].Value<double>();

            var result = _cadService.CreateEntity(entityType, points, layer, properties);
            return result.Success
                ? ToolResult.Ok($"✅ 已创建 {entityType} 实体（Handle: {result.Handle}，图层: {layer ?? "当前"}）",
                    new { entityType, layer, handle = result.Handle })
                : ToolResult.Fail(result.Error);
        }

        private ToolResult HandleSetLayer(JObject json)
        {
            string name = json["layer"]?.ToString();
            if (string.IsNullOrWhiteSpace(name))
                return ToolResult.Fail("layer 参数为空（set_layer 需要 layer 参数作为图层名）");

            int? color = json["color"]?.Type == JTokenType.Integer ? json["color"].Value<int>() : (int?)null;
            string linetype = json["linetype"]?.ToString();
            bool? frozen = json["frozen"]?.Type == JTokenType.Boolean ? json["frozen"].Value<bool>() : (bool?)null;
            bool? locked = json["locked"]?.Type == JTokenType.Boolean ? json["locked"].Value<bool>() : (bool?)null;

            var result = _cadService.SetLayer(name, color, linetype, frozen, locked);
            return result.Success
                ? ToolResult.Ok($"✅ 图层 '{name}' 已设置", new { layer = name, color, linetype, frozen, locked })
                : ToolResult.Fail(result.Error);
        }

        private ToolResult HandleAddText(JObject json)
        {
            var position = ParseSinglePoint(json["points"]);
            if (position == null)
                return ToolResult.Fail("points 参数为空或格式错误（add_text 需要 points 里的第一个点作为插入位置）");

            string text = json["text"]?.ToString();
            if (string.IsNullOrEmpty(text))
                return ToolResult.Fail("text 参数为空（add_text 需要 text 参数）");

            double height = json["height"]?.Value<double>() ?? 3.0;
            string layer = json["layer"]?.ToString();
            double rotation = json["rotation"]?.Value<double>() ?? 0;

            var result = _cadService.AddText(position, text, height, layer, rotation);
            return result.Success
                ? ToolResult.Ok($"✅ 已添加文字 '{text}'（Handle: {result.Handle}，高度: {height}mm）",
                    new { text, height, layer, rotation, handle = result.Handle })
                : ToolResult.Fail(result.Error);
        }

        private ToolResult HandleAddDimension(JObject json)
        {
            var points = ParsePoints(json["points"]);
            if (points == null || points.Count < 3)
                return ToolResult.Fail("points 参数需要 3 个点：起点、终点、尺寸线位置（[[x,y,z],[x,y,z],[x,y,z]]）");

            string dimType = json["dim_type"]?.ToString() ?? "Aligned";
            string layer = json["layer"]?.ToString();

            var result = _cadService.AddDimension(dimType, points[0], points[1], points[2], layer);
            return result.Success
                ? ToolResult.Ok($"✅ 已添加 {dimType} 标注（Handle: {result.Handle}）",
                    new { dimType, layer, handle = result.Handle })
                : ToolResult.Fail(result.Error);
        }

        private ToolResult HandleSaveDrawing(JObject json)
        {
            string path = json["file_path"]?.ToString();
            var result = _cadService.SaveDrawing(path);
            return result.Success
                ? ToolResult.Ok(string.IsNullOrEmpty(path)
                    ? "✅ 图纸已保存到原路径"
                    : $"✅ 图纸已另存为: {path}",
                    new { savedPath = path })
                : ToolResult.Fail(result.Error);
        }

        private ToolResult HandleGetLayers()
        {
            var result = _cadService.GetLayers();
            if (!result.Success)
                return ToolResult.Fail(result.Error);

            var layers = result.Data.ContainsKey("Layers")
                ? (List<string>)result.Data["Layers"]
                : new List<string>();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"📋 图层列表（共 {layers.Count} 个）:");
            foreach (var name in layers)
            {
                sb.AppendLine($"  - {name}");
            }
            return ToolResult.Ok(sb.ToString(), new { count = layers.Count, layers });
        }

        private ToolResult HandleGetEntityProperties(JObject json)
        {
            string handle = json["element_handle"]?.ToString();
            if (string.IsNullOrWhiteSpace(handle))
                return ToolResult.Fail("element_handle 参数为空（get_entity_properties 需要 element_handle 参数）");

            var result = _cadService.GetEntityProperties(handle);
            if (!result.Success)
                return ToolResult.Fail(result.Error);

            return ToolResult.Ok($"✅ 实体属性（Handle: {handle}）", result.Data);
        }

        #region 参数解析辅助

        private List<Point3D> ParsePoints(JToken token)
        {
            if (token == null || token.Type != JTokenType.Array)
                return null;

            var points = new List<Point3D>();
            foreach (var item in token)
            {
                if (item.Type != JTokenType.Array) continue;
                var arr = item.ToObject<double[]>();
                if (arr == null || arr.Length < 2) continue;
                double z = arr.Length >= 3 ? arr[2] : 0;
                points.Add(new Point3D(arr[0], arr[1], z));
            }
            return points.Count > 0 ? points : null;
        }

        private Point3D ParseSinglePoint(JToken token)
        {
            var points = ParsePoints(token);
            return points?.Count > 0 ? points[0] : null;
        }

        #endregion
    }
}
