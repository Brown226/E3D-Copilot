using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace E3DCopilot.Core.Tools.Handlers
{
    /// <summary>
    /// 几何计算工具 — 纯数学实现（不依赖 E3D API）
    /// 支持：距离、角度、向量运算（点积、叉积、模长）
    /// 坐标单位：毫米（E3D 默认单位）
    /// </summary>
    public class CalculateHandler : IToolHandler
    {
        public string Name => "calculate";
        public string Description => "Perform geometry calculations: distance between points, angle between vectors, vector operations (magnitude, dot_product, cross_product). Coordinates are in millimeters (E3D default unit).";
        public string ParameterSchema => @"{
  ""type"": ""object"",
  ""properties"": {
    ""operation"": {
      ""type"": ""string"",
      ""description"": ""Calculation type: distance, angle, vector, magnitude, dot_product, cross_product"",
      ""enum"": [""distance"", ""angle"", ""vector"", ""magnitude"", ""dot_product"", ""cross_product""]
    },
    ""point1"": {
      ""type"": ""array"",
      ""items"": { ""type"": ""number"" },
      ""description"": ""First point [x, y, z] in mm""
    },
    ""point2"": {
      ""type"": ""array"",
      ""items"": { ""type"": ""number"" },
      ""description"": ""Second point [x, y, z] in mm""
    },
    ""vector1"": {
      ""type"": ""array"",
      ""items"": { ""type"": ""number"" },
      ""description"": ""First vector [x, y, z]""
    },
    ""vector2"": {
      ""type"": ""array"",
      ""items"": { ""type"": ""number"" },
      ""description"": ""Second vector [x, y, z]""
    }
  },
  ""required"": [""operation""]
}";
        public bool IsReadOnly => true;

        public async Task<ToolResult> ExecuteAsync(string args, CancellationToken ct = default)
        {
            try
            {
                var json = JObject.Parse(args);
                string operation = json["operation"]?.Value<string>();

                if (string.IsNullOrEmpty(operation))
                    return ToolResult.Fail("Missing 'operation' parameter");

                ToolResult calcResult;
                switch (operation.ToLower())
                {
                    case "distance":
                        calcResult = CalculateDistance(json); break;
                    case "angle":
                        calcResult = CalculateAngle(json); break;
                    case "vector":
                        calcResult = CalculateVector(json); break;
                    case "magnitude":
                        calcResult = CalculateMagnitude(json); break;
                    case "dot_product":
                        calcResult = CalculateDotProduct(json); break;
                    case "cross_product":
                        calcResult = CalculateCrossProduct(json); break;
                    default:
                        return ToolResult.Fail($"Unknown operation: {operation}. Supported: distance, angle, vector, magnitude, dot_product, cross_product");
                }
                // 最小安全方案：Text 不变，Data 放结构化 meta 供前端渲染
                if (calcResult.Success)
                {
                    calcResult.Data = new JObject
                    {
                        ["tool"] = "calculate",
                        ["coreTool"] = "calculate",
                        ["summary"] = $"{operation} 计算完成",
                    };
                }
                return calcResult;
            }
            catch (Exception ex)
            {
                return ToolResult.Fail($"Calculation failed: {ex.Message}");
            }
        }

        private ToolResult CalculateDistance(JObject json)
        {
            double[] p1 = ParsePoint(json, "point1");
            double[] p2 = ParsePoint(json, "point2");

            if (p1 == null || p2 == null)
                return ToolResult.Fail("distance requires 'point1' and 'point2' arrays [x, y, z]");

            double dx = p2[0] - p1[0];
            double dy = p2[1] - p1[1];
            double dz = p2[2] - p1[2];
            double distance = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            var result = new JObject
            {
                ["operation"] = "distance",
                ["point1"] = JArray.FromObject(p1),
                ["point2"] = JArray.FromObject(p2),
                ["distance_mm"] = Math.Round(distance, 3),
                ["distance_m"] = Math.Round(distance / 1000.0, 6),
                ["delta_x"] = Math.Round(dx, 3),
                ["delta_y"] = Math.Round(dy, 3),
                ["delta_z"] = Math.Round(dz, 3)
            };

            return ToolResult.Ok(result.ToString(), null);
        }

        private ToolResult CalculateAngle(JObject json)
        {
            double[] v1 = ParseVector(json, "vector1");
            double[] v2 = ParseVector(json, "vector2");

            if (v1 == null || v2 == null)
                return ToolResult.Fail("angle requires 'vector1' and 'vector2' arrays [x, y, z]");

            double dot = v1[0] * v2[0] + v1[1] * v2[1] + v1[2] * v2[2];
            double mag1 = Math.Sqrt(v1[0] * v1[0] + v1[1] * v1[1] + v1[2] * v1[2]);
            double mag2 = Math.Sqrt(v2[0] * v2[0] + v2[1] * v2[1] + v2[2] * v2[2]);

            if (mag1 < 1e-10 || mag2 < 1e-10)
                return ToolResult.Fail("Cannot calculate angle: one of the vectors is zero-length");

            double cosAngle = dot / (mag1 * mag2);
            cosAngle = Math.Max(-1.0, Math.Min(1.0, cosAngle));
            double angleRad = Math.Acos(cosAngle);
            double angleDeg = angleRad * 180.0 / Math.PI;

            var result = new JObject
            {
                ["operation"] = "angle",
                ["vector1"] = JArray.FromObject(v1),
                ["vector2"] = JArray.FromObject(v2),
                ["angle_degrees"] = Math.Round(angleDeg, 3),
                ["angle_radians"] = Math.Round(angleRad, 6),
                ["dot_product"] = Math.Round(dot, 6)
            };

            return ToolResult.Ok(result.ToString(), null);
        }

        private ToolResult CalculateVector(JObject json)
        {
            double[] p1 = ParsePoint(json, "point1");
            double[] p2 = ParsePoint(json, "point2");

            if (p1 == null || p2 == null)
                return ToolResult.Fail("vector requires 'point1' (start) and 'point2' (end) arrays [x, y, z]");

            double vx = p2[0] - p1[0];
            double vy = p2[1] - p1[1];
            double vz = p2[2] - p1[2];
            double magnitude = Math.Sqrt(vx * vx + vy * vy + vz * vz);

            var result = new JObject
            {
                ["operation"] = "vector",
                ["from_point"] = JArray.FromObject(p1),
                ["to_point"] = JArray.FromObject(p2),
                ["vector"] = JArray.FromObject(new double[] { Math.Round(vx, 3), Math.Round(vy, 3), Math.Round(vz, 3) }),
                ["magnitude"] = Math.Round(magnitude, 3),
                ["unit_vector"] = magnitude > 1e-10
                    ? JArray.FromObject(new double[] { Math.Round(vx / magnitude, 6), Math.Round(vy / magnitude, 6), Math.Round(vz / magnitude, 6) })
                    : JArray.FromObject(new double[] { 0, 0, 0 })
            };

            return ToolResult.Ok(result.ToString(), null);
        }

        private ToolResult CalculateMagnitude(JObject json)
        {
            double[] v = ParseVector(json, "vector1");
            if (v == null)
                return ToolResult.Fail("magnitude requires 'vector1' array [x, y, z]");

            double mag = Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);

            var result = new JObject
            {
                ["operation"] = "magnitude",
                ["vector"] = JArray.FromObject(v),
                ["magnitude"] = Math.Round(mag, 3)
            };

            return ToolResult.Ok(result.ToString(), null);
        }

        private ToolResult CalculateDotProduct(JObject json)
        {
            double[] v1 = ParseVector(json, "vector1");
            double[] v2 = ParseVector(json, "vector2");

            if (v1 == null || v2 == null)
                return ToolResult.Fail("dot_product requires 'vector1' and 'vector2' arrays [x, y, z]");

            double dot = v1[0] * v2[0] + v1[1] * v2[1] + v1[2] * v2[2];

            var result = new JObject
            {
                ["operation"] = "dot_product",
                ["vector1"] = JArray.FromObject(v1),
                ["vector2"] = JArray.FromObject(v2),
                ["dot_product"] = Math.Round(dot, 6),
                ["is_perpendicular"] = Math.Abs(dot) < 1e-10,
                ["is_parallel"] = Math.Abs(Math.Abs(dot) - Magnitude(v1) * Magnitude(v2)) < 1e-6
            };

            return ToolResult.Ok(result.ToString(), null);
        }

        private ToolResult CalculateCrossProduct(JObject json)
        {
            double[] v1 = ParseVector(json, "vector1");
            double[] v2 = ParseVector(json, "vector2");

            if (v1 == null || v2 == null)
                return ToolResult.Fail("cross_product requires 'vector1' and 'vector2' arrays [x, y, z]");

            double cx = v1[1] * v2[2] - v1[2] * v2[1];
            double cy = v1[2] * v2[0] - v1[0] * v2[2];
            double cz = v1[0] * v2[1] - v1[1] * v2[0];

            var result = new JObject
            {
                ["operation"] = "cross_product",
                ["vector1"] = JArray.FromObject(v1),
                ["vector2"] = JArray.FromObject(v2),
                ["cross_product"] = JArray.FromObject(new double[] { Math.Round(cx, 6), Math.Round(cy, 6), Math.Round(cz, 6) }),
                ["magnitude"] = Math.Round(Math.Sqrt(cx * cx + cy * cy + cz * cz), 3),
                ["note"] = "Cross product vector is perpendicular to both input vectors"
            };

            return ToolResult.Ok(result.ToString(), null);
        }

        private double[] ParsePoint(JObject json, string key)
        {
            var token = json[key];
            if (token == null || token.Type != JTokenType.Array) return null;
            var arr = token.ToObject<double[]>();
            if (arr == null || arr.Length != 3) return null;
            return arr;
        }

        private double[] ParseVector(JObject json, string key)
        {
            return ParsePoint(json, key);
        }

        private double Magnitude(double[] v)
        {
            return Math.Sqrt(v[0] * v[0] + v[1] * v[1] + v[2] * v[2]);
        }
    }
}
