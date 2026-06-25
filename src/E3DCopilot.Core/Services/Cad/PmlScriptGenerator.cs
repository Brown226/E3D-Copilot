using System;
using System.Collections.Generic;
using System.Text;
using E3DCopilot.Core.Models.Building;
using E3DCopilot.Core.Models.Geometry;

namespace E3DCopilot.Core.Services.Cad
{
    /// <summary>
    /// PML 脚本生成器（从 SmartDuct 项目移植并扩展）
    /// 用于生成 AVEVA E3D/PDMS PML 宏脚本
    /// </summary>
    public class PmlScriptGenerator
    {
        /// <summary>
        /// 生成建筑元素的 PML 脚本
        /// </summary>
        /// <param name="elements">建筑元素列表</param>
        /// <param name="siteName">SITE 名称</param>
        /// <param name="zoneName">ZONE 名称</param>
        /// <returns>PML 脚本字符串</returns>
        public string GenerateBuildingScript(List<BuildingElement> elements, string siteName = "IMPORT_SITE", string zoneName = "IMPORT_ZONE")
        {
            var sb = new StringBuilder();

            // 头部和设置
            sb.AppendLine("$!ECHO 正在导入建筑模型...");
            sb.AppendLine("UNITS /MM");
            sb.AppendLine($"NEW SITE /{siteName}");
            sb.AppendLine($"NEW ZONE /{zoneName}");
            sb.AppendLine();

            int elementCounter = 1;

            foreach (var element in elements)
            {
                switch (element.Type)
                {
                    case BuildingElementType.Wall:
                        GenerateWallScript(sb, element, ref elementCounter);
                        break;
                    case BuildingElementType.Column:
                        GenerateColumnScript(sb, element, ref elementCounter);
                        break;
                    case BuildingElementType.Beam:
                        GenerateBeamScript(sb, element, ref elementCounter);
                        break;
                    case BuildingElementType.Equipment:
                    case BuildingElementType.Fan:
                        GenerateEquipmentScript(sb, element, ref elementCounter);
                        break;
                    default:
                        GenerateGenericElementScript(sb, element, ref elementCounter);
                        break;
                }
            }

            sb.AppendLine();
            sb.AppendLine("$!ECHO 导入完成！");
            return sb.ToString();
        }

        /// <summary>
        /// 生成墙体 PML 脚本
        /// </summary>
        private void GenerateWallScript(StringBuilder sb, BuildingElement wall, ref int counter)
        {
            if (wall.Points == null || wall.Points.Count < 2)
                return;

            string wallName = $"WALL_{counter:D4}";
            counter++;

            // 获取墙体参数
            double height = 3000; // 默认墙高
            double thickness = 200; // 默认墙厚

            if (wall.Properties.ContainsKey("Height"))
                double.TryParse(wall.Properties["Height"].ToString(), out height);
            if (wall.Properties.ContainsKey("Thickness"))
                double.TryParse(wall.Properties["Thickness"].ToString(), out thickness);

            // 对于直线墙体（2个点）
            if (wall.Points.Count == 2)
            {
                var start = wall.Points[0];
                var end = wall.Points[1];

                sb.AppendLine($"NEW STWALL /{wallName}");
                sb.AppendLine($"  DESP {thickness:F2} {height:F2}");
                sb.AppendLine($"  POSS E {start.X:F2} N {start.Y:F2} U {start.Z:F2}");
                sb.AppendLine($"  POSE E {end.X:F2} N {end.Y:F2} U {end.Z:F2}");
                sb.AppendLine("  SPRE SPCOMPONENT 3 of SELEC 1 of SPECIFICATION /Concrete_Wall-SPEC");
                sb.AppendLine();
            }
            // 对于多边形墙体（闭合环）
            else if (wall.Points.Count > 2 && wall.Properties.ContainsKey("IsPolygon"))
            {
                // 将多边形分解为多段直线墙体
                for (int i = 0; i < wall.Points.Count; i++)
                {
                    var start = wall.Points[i];
                    var end = wall.Points[(i + 1) % wall.Points.Count];

                    string segmentName = $"{wallName}_S{i + 1:D2}";

                    sb.AppendLine($"NEW STWALL /{segmentName}");
                    sb.AppendLine($"  DESP {thickness:F2} {height:F2}");
                    sb.AppendLine($"  POSS E {start.X:F2} N {start.Y:F2} U {start.Z:F2}");
                    sb.AppendLine($"  POSE E {end.X:F2} N {end.Y:F2} U {end.Z:F2}");
                    sb.AppendLine("  SPRE SPCOMPONENT 3 of SELEC 1 of SPECIFICATION /Concrete_Wall-SPEC");
                    sb.AppendLine();
                }
            }
        }

        /// <summary>
        /// 生成柱子 PML 脚本
        /// </summary>
        private void GenerateColumnScript(StringBuilder sb, BuildingElement column, ref int counter)
        {
            if (column.Points == null || column.Points.Count < 1)
                return;

            string columnName = $"COL_{counter:D4}";
            counter++;

            var position = column.Points[0];
            double width = 300;
            double depth = 300;
            double height = 3000;

            if (column.Properties.ContainsKey("Width"))
                double.TryParse(column.Properties["Width"].ToString(), out width);
            if (column.Properties.ContainsKey("Depth"))
                double.TryParse(column.Properties["Depth"].ToString(), out depth);
            if (column.Properties.ContainsKey("Height"))
                double.TryParse(column.Properties["Height"].ToString(), out height);

            sb.AppendLine($"NEW STCOLUMN /{columnName}");
            sb.AppendLine($"  DESP {width:F2} {depth:F2} {height:F2}");
            sb.AppendLine($"  POS E {position.X:F2} N {position.Y:F2} U {position.Z:F2}");
            sb.AppendLine("  SPRE SPCOMPONENT 1 of SELEC 1 of SPECIFICATION /Concrete_Column-SPEC");
            sb.AppendLine();
        }

        /// <summary>
        /// 生成梁 PML 脚本
        /// </summary>
        private void GenerateBeamScript(StringBuilder sb, BuildingElement beam, ref int counter)
        {
            if (beam.Points == null || beam.Points.Count < 2)
                return;

            string beamName = $"BEAM_{counter:D4}";
            counter++;

            var start = beam.Points[0];
            var end = beam.Points[1];
            double width = 300;
            double height = 600;

            if (beam.Properties.ContainsKey("Width"))
                double.TryParse(beam.Properties["Width"].ToString(), out width);
            if (beam.Properties.ContainsKey("Height"))
                double.TryParse(beam.Properties["Height"].ToString(), out height);

            sb.AppendLine($"NEW STBEAM /{beamName}");
            sb.AppendLine($"  DESP {width:F2} {height:F2}");
            sb.AppendLine($"  POSS E {start.X:F2} N {start.Y:F2} U {start.Z:F2}");
            sb.AppendLine($"  POSE E {end.X:F2} N {end.Y:F2} U {end.Z:F2}");
            sb.AppendLine("  SPRE SPCOMPONENT 2 of SELEC 1 of SPECIFICATION /Concrete_Beam-SPEC");
            sb.AppendLine();
        }

        /// <summary>
        /// 生成设备 PML 脚本
        /// </summary>
        private void GenerateEquipmentScript(StringBuilder sb, BuildingElement equipment, ref int counter)
        {
            if (equipment.Points == null || equipment.Points.Count < 1)
                return;

            string equipName = $"EQUIP_{counter:D4}";
            counter++;

            var position = equipment.Points[0];

            sb.AppendLine($"NEW EQUIP /{equipName}");
            sb.AppendLine($"  POS E {position.X:F2} N {position.Y:F2} U {position.Z:F2}");
            sb.AppendLine($"  DESC '{equipment.Type}'");
            sb.AppendLine();
        }

        /// <summary>
        /// 生成通用元素 PML 脚本
        /// </summary>
        private void GenerateGenericElementScript(StringBuilder sb, BuildingElement element, ref int counter)
        {
            if (element.Points == null || element.Points.Count < 1)
                return;

            string elemName = $"ELEM_{counter:D4}";
            counter++;

            var position = element.Points[0];

            sb.AppendLine($"$!ECHO 创建通用元素: {elemName} ({element.Type})");
            sb.AppendLine($"$!ECHO 位置: E {position.X:F2} N {position.Y:F2} U {position.Z:F2}");
            sb.AppendLine();
        }

        /// <summary>
        /// 生成管道系统 PML 脚本（从 SmartDuct 移植）
        /// </summary>
        /// <param name="paths">路径列表</param>
        /// <param name="siteName">SITE 名称</param>
        /// <param name="zoneName">ZONE 名称</param>
        /// <returns>PML 脚本字符串</returns>
        public string GeneratePipeScript(List<PipePath> paths, string siteName = "HVAC_SITE", string zoneName = "HVAC_ZONE")
        {
            var sb = new StringBuilder();

            sb.AppendLine("$!ECHO 生成管道系统...");
            sb.AppendLine("UNITS /MM");
            sb.AppendLine($"NEW SITE /{siteName}");
            sb.AppendLine($"NEW ZONE /{zoneName}");
            sb.AppendLine();

            int pipeCounter = 1;

            foreach (var path in paths)
            {
                if (path.Waypoints == null || path.Waypoints.Count < 2)
                    continue;

                string pipeName = $"PIPE_{pipeCounter:D4}";
                pipeCounter++;

                sb.AppendLine($"NEW PIPE /{pipeName}");
                sb.AppendLine($"  PSPE /A3B");
                sb.AppendLine("  ISPE /300");
                sb.AppendLine("  TEMP 20");

                var start = path.Waypoints[0];
                var end = path.Waypoints[path.Waypoints.Count - 1];

                sb.AppendLine($"  NEW BRAN /{pipeName}/B1");
                sb.AppendLine($"    HBOR 100");
                sb.AppendLine($"    TBOR 100");
                sb.AppendLine($"    HPOS E {start.X:F2} N {start.Y:F2} U {start.Z:F2}");
                sb.AppendLine($"    TPOS E {end.X:F2} N {end.Y:F2} U {end.Z:F2}");
                sb.AppendLine("    HCON OPEN");
                sb.AppendLine("    TCON OPEN");

                // 在转弯处创建弯头
                for (int i = 1; i < path.Waypoints.Count - 1; i++)
                {
                    var prev = path.Waypoints[i - 1];
                    var curr = path.Waypoints[i];
                    var next = path.Waypoints[i + 1];

                    if (IsTurn(prev, curr, next))
                    {
                        sb.AppendLine($"    NEW ELBO");
                        sb.AppendLine($"      AT E {curr.X:F2} N {curr.Y:F2} U {curr.Z:F2}");
                        string direction = GetDirection(curr, next);
                        sb.AppendLine($"      AXIS {direction}");
                    }
                }

                sb.AppendLine();
            }

            sb.AppendLine("$!ECHO 管道系统生成完成！");
            return sb.ToString();
        }

        /// <summary>
        /// 判断是否为转弯点
        /// </summary>
        private bool IsTurn(Point3D p1, Point3D p2, Point3D p3)
        {
            double v1x = p2.X - p1.X;
            double v1y = p2.Y - p1.Y;
            double v1z = p2.Z - p1.Z;

            double v2x = p3.X - p2.X;
            double v2y = p3.Y - p2.Y;
            double v2z = p3.Z - p2.Z;

            bool v1XAligned = Math.Abs(v1x) > 0.1;
            bool v1YAligned = Math.Abs(v1y) > 0.1;
            bool v1ZAligned = Math.Abs(v1z) > 0.1;

            bool v2XAligned = Math.Abs(v2x) > 0.1;
            bool v2YAligned = Math.Abs(v2y) > 0.1;
            bool v2ZAligned = Math.Abs(v2z) > 0.1;

            if (v1XAligned && v2XAligned) return false;
            if (v1YAligned && v2YAligned) return false;
            if (v1ZAligned && v2ZAligned) return false;

            return true;
        }

        /// <summary>
        /// 获取方向字符串（E/N/U/W/S/D）
        /// </summary>
        private string GetDirection(Point3D from, Point3D to)
        {
            double dx = to.X - from.X;
            double dy = to.Y - from.Y;
            double dz = to.Z - from.Z;

            if (Math.Abs(dx) > Math.Abs(dy) && Math.Abs(dx) > Math.Abs(dz))
                return dx > 0 ? "E" : "W";
            if (Math.Abs(dy) > Math.Abs(dx) && Math.Abs(dy) > Math.Abs(dz))
                return dy > 0 ? "N" : "S";
            return dz > 0 ? "U" : "D";
        }
    }

    /// <summary>
    /// 管道路径数据类
    /// </summary>
    public class PipePath
    {
        public string SystemType { get; set; }
        public string RoomName { get; set; }
        public List<Point3D> Waypoints { get; set; }

        public PipePath()
        {
            Waypoints = new List<Point3D>();
        }
    }
}
