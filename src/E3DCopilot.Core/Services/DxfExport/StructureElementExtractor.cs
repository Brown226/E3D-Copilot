using System;
using System.Collections.Generic;
using System.Linq;
using E3DCopilot.Core.Models;
using E3DCopilot.Core.Models.Geometry;

namespace E3DCopilot.Core.Services.DxfExport
{
    /// <summary>
    /// 结构元素提取服务
    /// 从 E3D 数据库提取结构元素（SCTN/STWL/FRMW等）的几何数据
    /// </summary>
    public class StructureElementExtractor
    {
        private readonly bool _useRealE3DData;

        public StructureElementExtractor(bool useRealE3DData = false)
        {
            _useRealE3DData = useRealE3DData;
        }

        /// <summary>
        /// 根据元素名称提取结构元素
        /// </summary>
        public StructureElement Extract(string elementName)
        {
            try
            {
                if (_useRealE3DData)
                {
                    // 尝试从 E3D 提取真实数据
                    var realElement = ExtractFromE3D(elementName);
                    if (realElement != null)
                        return realElement;
                }

                // 回退到模拟数据
                return CreateMockElement(elementName);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"提取元素 {elementName} 失败: {ex.Message}");
                return CreateMockElement(elementName);
            }
        }

        /// <summary>
        /// 批量提取多个元素
        /// </summary>
        public List<StructureElement> ExtractMultiple(IEnumerable<string> elementNames)
        {
            var elements = new List<StructureElement>();
            foreach (var name in elementNames)
            {
                var element = Extract(name);
                if (element != null)
                {
                    elements.Add(element);
                }
            }
            return elements;
        }

        /// <summary>
        /// 从 E3D 提取真实数据
        /// </summary>
        private StructureElement ExtractFromE3D(string elementName)
        {
            try
            {
                // 使用反射动态加载 E3D API，避免编译时依赖
                var dbElementType = Type.GetType("Aveva.Core.Database.DbElement, Aveva.Core.Database");
                if (dbElementType == null)
                    return null;

                var getElementMethod = dbElementType.GetMethod("GetElement", new[] { typeof(string) });
                if (getElementMethod == null)
                    return null;

                var dbElement = getElementMethod.Invoke(null, new object[] { elementName });
                if (dbElement == null)
                    return null;

                // 检查元素是否有效
                var isValidProperty = dbElementType.GetProperty("IsValid");
                if (isValidProperty == null || !(bool)isValidProperty.GetValue(dbElement))
                    return null;

                // 获取元素类型
                var elementTypeProperty = dbElementType.GetProperty("ElementType");
                var elementType = elementTypeProperty?.GetValue(dbElement)?.ToString()?.ToUpper() ?? "UNKNOWN";

                var element = new StructureElement
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = elementName,
                    DbRef = elementName
                };

                // 根据元素类型提取
                switch (elementType)
                {
                    case "SCTN":
                        element.Type = StructureElementType.Sctn;
                        ExtractSctnData(dbElement, element);
                        break;
                    case "STWL":
                        element.Type = StructureElementType.Stwl;
                        ExtractStwlData(dbElement, element);
                        break;
                    case "FRMW":
                        element.Type = StructureElementType.Frmw;
                        ExtractFrmwData(dbElement, element);
                        break;
                    default:
                        element.Type = StructureElementType.Other;
                        ExtractGenericData(dbElement, element);
                        break;
                }

                return element;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"从 E3D 提取 {elementName} 失败: {ex.Message}");
                return null;
            }
        }

        private void ExtractSctnData(object dbElement, StructureElement element)
        {
            try
            {
                // 使用反射获取位置数据
                var dbElementType = dbElement.GetType();

                // 尝试获取起点和终点位置
                var getPositionMethod = dbElementType.GetMethod("GetPosition");
                if (getPositionMethod != null)
                {
                    // 获取 StartPosition
                    var startPos = getPositionMethod.Invoke(dbElement, new object[] { GetDbAttribute("StartPosition") });
                    if (startPos != null)
                    {
                        var pos = ExtractPosition(startPos);
                        element.StartPoint = pos;
                    }

                    // 获取 EndPosition
                    var endPos = getPositionMethod.Invoke(dbElement, new object[] { GetDbAttribute("EndPosition") });
                    if (endPos != null)
                    {
                        var pos = ExtractPosition(endPos);
                        element.EndPoint = pos;
                    }
                }

                // 获取截面尺寸
                element.Width = GetDoubleAttribute(dbElement, "Width", 300);
                element.Height = GetDoubleAttribute(dbElement, "Depth", 500);

                // 计算包围盒
                element.BoundingBox = CalculateBoundingBox(element);
            }
            catch
            {
                // 使用默认值
                element.StartPoint = new Point3D(0, 0, 0);
                element.EndPoint = new Point3D(5000, 0, 0);
                element.Width = 300;
                element.Height = 500;
                element.BoundingBox = new BoundingBox(
                    new Point3D(-150, -250, -250),
                    new Point3D(5150, 250, 250));
            }
        }

        private void ExtractStwlData(object dbElement, StructureElement element)
        {
            try
            {
                // 墙的边界点
                element.BoundaryPoints = new List<Point3D>();

                // 尝试获取世界位置
                var dbElementType = dbElement.GetType();
                var getPositionMethod = dbElementType.GetMethod("GetPosition");
                if (getPositionMethod != null)
                {
                    var pos = getPositionMethod.Invoke(dbElement, new object[] { GetDbAttribute("WorldPosition") });
                    if (pos != null)
                    {
                        var position = ExtractPosition(pos);
                        // 默认 5m x 0.2m 的墙
                        element.BoundaryPoints = new List<Point3D>
                        {
                            new Point3D(position.X - 2500, position.Y - 100, position.Z),
                            new Point3D(position.X + 2500, position.Y - 100, position.Z),
                            new Point3D(position.X + 2500, position.Y + 100, position.Z),
                            new Point3D(position.X - 2500, position.Y + 100, position.Z)
                        };
                    }
                }

                if (element.BoundaryPoints.Count < 3)
                {
                    // 使用默认值
                    element.BoundaryPoints = new List<Point3D>
                    {
                        new Point3D(0, 0, 0),
                        new Point3D(5000, 0, 0),
                        new Point3D(5000, 200, 0),
                        new Point3D(0, 200, 0)
                    };
                }

                element.BoundingBox = CalculateBoundingBoxFromPoints(element.BoundaryPoints);
            }
            catch
            {
                element.BoundaryPoints = new List<Point3D>
                {
                    new Point3D(0, 0, 0),
                    new Point3D(5000, 0, 0),
                    new Point3D(5000, 200, 0),
                    new Point3D(0, 200, 0)
                };
                element.BoundingBox = new BoundingBox(
                    new Point3D(0, 0, 0),
                    new Point3D(5000, 200, 0));
            }
        }

        private void ExtractFrmwData(object dbElement, StructureElement element)
        {
            try
            {
                var dbElementType = dbElement.GetType();
                var getPositionMethod = dbElementType.GetMethod("GetPosition");
                if (getPositionMethod != null)
                {
                    var startPos = getPositionMethod.Invoke(dbElement, new object[] { GetDbAttribute("StartPosition") });
                    var endPos = getPositionMethod.Invoke(dbElement, new object[] { GetDbAttribute("EndPosition") });

                    if (startPos != null)
                        element.StartPoint = ExtractPosition(startPos);
                    if (endPos != null)
                        element.EndPoint = ExtractPosition(endPos);
                }

                element.Width = 50;
                element.Height = 50;
                element.BoundingBox = CalculateBoundingBox(element);
            }
            catch
            {
                element.StartPoint = new Point3D(0, 0, 0);
                element.EndPoint = new Point3D(5000, 5000, 0);
                element.Width = 50;
                element.Height = 50;
                element.BoundingBox = new BoundingBox(
                    new Point3D(-25, -25, -25),
                    new Point3D(5025, 5025, 25));
            }
        }

        private void ExtractGenericData(object dbElement, StructureElement element)
        {
            try
            {
                var dbElementType = dbElement.GetType();
                var getPositionMethod = dbElementType.GetMethod("GetPosition");
                if (getPositionMethod != null)
                {
                    var pos = getPositionMethod.Invoke(dbElement, new object[] { GetDbAttribute("WorldPosition") });
                    if (pos != null)
                    {
                        var position = ExtractPosition(pos);
                        element.StartPoint = position;
                        element.EndPoint = position;
                    }
                }

                element.BoundingBox = new BoundingBox(element.StartPoint, element.EndPoint);
            }
            catch
            {
                element.StartPoint = new Point3D(0, 0, 0);
                element.EndPoint = new Point3D(1000, 1000, 0);
                element.BoundingBox = new BoundingBox(
                    new Point3D(0, 0, 0),
                    new Point3D(1000, 1000, 0));
            }
        }

        private Point3D ExtractPosition(object positionObj)
        {
            try
            {
                var posType = positionObj.GetType();
                var eastProp = posType.GetProperty("East") ?? posType.GetProperty("X");
                var northProp = posType.GetProperty("North") ?? posType.GetProperty("Y");
                var upProp = posType.GetProperty("Up") ?? posType.GetProperty("Z");

                double east = eastProp != null ? Convert.ToDouble(eastProp.GetValue(positionObj)) : 0;
                double north = northProp != null ? Convert.ToDouble(northProp.GetValue(positionObj)) : 0;
                double up = upProp != null ? Convert.ToDouble(upProp.GetValue(positionObj)) : 0;

                return new Point3D(east, north, up);
            }
            catch
            {
                return new Point3D(0, 0, 0);
            }
        }

        private object GetDbAttribute(string name)
        {
            try
            {
                var dbAttributeType = Type.GetType("Aveva.Core.Database.DbAttribute, Aveva.Core.Database");
                if (dbAttributeType == null)
                    return null;

                var getAttributeMethod = dbAttributeType.GetMethod("GetAttribute");
                if (getAttributeMethod == null)
                    return null;

                return getAttributeMethod.Invoke(null, new object[] { name });
            }
            catch
            {
                return null;
            }
        }

        private double GetDoubleAttribute(object dbElement, string attrName, double defaultValue)
        {
            try
            {
                var dbElementType = dbElement.GetType();
                var getAsDoubleMethod = dbElementType.GetMethod("GetAsDouble");
                if (getAsDoubleMethod == null)
                    return defaultValue;

                var attr = GetDbAttribute(attrName);
                if (attr == null)
                    return defaultValue;

                var result = getAsDoubleMethod.Invoke(dbElement, new object[] { attr });
                return result != null ? Convert.ToDouble(result) * 1000 : defaultValue; // 转换为mm
            }
            catch
            {
                return defaultValue;
            }
        }

        /// <summary>
        /// 创建模拟元素（用于测试或 E3D 不可用时）
        /// </summary>
        private StructureElement CreateMockElement(string elementName)
        {
            var element = new StructureElement
            {
                Id = Guid.NewGuid().ToString(),
                Name = elementName,
                DbRef = elementName
            };

            string upperName = elementName.ToUpper();
            if (upperName.Contains("SCTN"))
            {
                element.Type = StructureElementType.Sctn;
                element.StartPoint = new Point3D(0, 0, 0);
                element.EndPoint = new Point3D(5000, 0, 0);
                element.Width = 300;
                element.Height = 500;
                element.BoundingBox = new BoundingBox(
                    new Point3D(-150, -250, -250),
                    new Point3D(5150, 250, 250));
            }
            else if (upperName.Contains("STWL"))
            {
                element.Type = StructureElementType.Stwl;
                element.BoundaryPoints = new List<Point3D>
                {
                    new Point3D(0, 0, 0),
                    new Point3D(5000, 0, 0),
                    new Point3D(5000, 200, 0),
                    new Point3D(0, 200, 0)
                };
                element.BoundingBox = new BoundingBox(
                    new Point3D(0, 0, 0),
                    new Point3D(5000, 200, 0));
            }
            else if (upperName.Contains("FRMW"))
            {
                element.Type = StructureElementType.Frmw;
                element.StartPoint = new Point3D(0, 0, 0);
                element.EndPoint = new Point3D(5000, 5000, 0);
                element.Width = 50;
                element.Height = 50;
                element.BoundingBox = new BoundingBox(
                    new Point3D(-25, -25, -25),
                    new Point3D(5025, 5025, 25));
            }
            else
            {
                element.Type = StructureElementType.Other;
                element.StartPoint = new Point3D(0, 0, 0);
                element.EndPoint = new Point3D(1000, 1000, 0);
                element.BoundingBox = new BoundingBox(
                    new Point3D(0, 0, 0),
                    new Point3D(1000, 1000, 0));
            }

            return element;
        }

        #region 辅助方法

        private BoundingBox CalculateBoundingBox(StructureElement element)
        {
            var min = new Point3D(
                Math.Min(element.StartPoint.X, element.EndPoint.X) - element.Width / 2,
                Math.Min(element.StartPoint.Y, element.EndPoint.Y) - element.Height / 2,
                Math.Min(element.StartPoint.Z, element.EndPoint.Z) - element.Height / 2
            );

            var max = new Point3D(
                Math.Max(element.StartPoint.X, element.EndPoint.X) + element.Width / 2,
                Math.Max(element.StartPoint.Y, element.EndPoint.Y) + element.Height / 2,
                Math.Max(element.StartPoint.Z, element.EndPoint.Z) + element.Height / 2
            );

            return new BoundingBox(min, max);
        }

        private BoundingBox CalculateBoundingBoxFromPoints(List<Point3D> points)
        {
            if (points == null || points.Count == 0)
                return new BoundingBox();

            double minX = points.Min(p => p.X);
            double minY = points.Min(p => p.Y);
            double minZ = points.Min(p => p.Z);
            double maxX = points.Max(p => p.X);
            double maxY = points.Max(p => p.Y);
            double maxZ = points.Max(p => p.Z);

            return new BoundingBox(
                new Point3D(minX, minY, minZ),
                new Point3D(maxX, maxY, maxZ));
        }

        #endregion
    }
}
