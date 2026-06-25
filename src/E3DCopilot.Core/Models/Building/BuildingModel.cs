using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using E3DCopilot.Core.Models.Geometry;

namespace E3DCopilot.Core.Models.Building
{
    /// <summary>
    /// 建筑元素类型枚举（从 SmartDuct 项目移植）
    /// </summary>
    [DataContract]
    public enum BuildingElementType
    {
        /// <summary>
        /// 墙体
        /// </summary>
        [EnumMember] Wall,
        /// <summary>
        /// 地板
        /// </summary>
        [EnumMember] Floor,
        /// <summary>
        /// 天花板
        /// </summary>
        [EnumMember] Ceiling,
        /// <summary>
        /// 窗户
        /// </summary>
        [EnumMember] Window,
        /// <summary>
        /// 门
        /// </summary>
        [EnumMember] Door,
        /// <summary>
        /// 柱子
        /// </summary>
        [EnumMember] Column,
        /// <summary>
        /// 进风口
        /// </summary>
        [EnumMember] AirInlet,
        /// <summary>
        /// 送风口
        /// </summary>
        [EnumMember] AirOutlet,
        /// <summary>
        /// 回风口
        /// </summary>
        [EnumMember] AirReturn,
        /// <summary>
        /// 风机
        /// </summary>
        [EnumMember] Fan,
        /// <summary>
        /// 房间
        /// </summary>
        [EnumMember] Room,
        /// <summary>
        /// 管道
        /// </summary>
        [EnumMember] Pipe,
        /// <summary>
        /// 梁
        /// </summary>
        [EnumMember] Beam,
        /// <summary>
        /// 设备
        /// </summary>
        [EnumMember] Equipment,
        /// <summary>
        /// 其他建筑元素
        /// </summary>
        [EnumMember] Other
    }

    /// <summary>
    /// 建筑元素类（从 SmartDuct 项目移植）
    /// </summary>
    [DataContract]
    public class BuildingElement
    {
        /// <summary>
        /// 建筑元素ID
        /// </summary>
        [DataMember] public string Id { get; set; }
        /// <summary>
        /// 建筑元素类型
        /// </summary>
        [DataMember] public BuildingElementType Type { get; set; }
        /// <summary>
        /// 建筑元素边界点
        /// </summary>
        [DataMember] public List<Point3D> Points { get; set; }
        /// <summary>
        /// 建筑元素包围盒
        /// </summary>
        [DataMember] public BoundingBox BoundingBox { get; set; }
        /// <summary>
        /// 楼层ID
        /// </summary>
        [DataMember] public string FloorId { get; set; }
        /// <summary>
        /// 关联到的房间名称
        /// </summary>
        [DataMember] public string AssignedRoomName { get; set; }
        /// <summary>
        /// 所属房间名称
        /// </summary>
        [DataMember] public string RoomName { get; set; }
        /// <summary>
        /// 是否已使用
        /// </summary>
        [DataMember] public bool IsUsed { get; set; }
        /// <summary>
        /// 扩展属性
        /// </summary>
        [DataMember] public Dictionary<string, object> Properties { get; set; }

        public BuildingElement()
        {
            Id = Guid.NewGuid().ToString();
            Points = new List<Point3D>();
            BoundingBox = new BoundingBox();
            FloorId = string.Empty;
            Properties = new Dictionary<string, object>();
            AssignedRoomName = string.Empty;
            RoomName = string.Empty;
            IsUsed = false;
        }

        public BuildingElement(string id, BuildingElementType type, List<Point3D> points, BoundingBox boundingBox, string floorId)
        {
            Id = id;
            Type = type;
            Points = points;
            BoundingBox = boundingBox;
            FloorId = floorId;
            Properties = new Dictionary<string, object>();
            AssignedRoomName = string.Empty;
            RoomName = string.Empty;
            IsUsed = false;
        }
    }

    /// <summary>
    /// 楼层类（从 SmartDuct 项目移植）
    /// </summary>
    [DataContract]
    public class Floor
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public double Height { get; set; }
        [DataMember] public double Z { get; set; }
        [DataMember] public List<BuildingElement> Elements { get; set; }

        public Floor()
        {
            Id = Guid.NewGuid().ToString();
            Name = string.Empty;
            Elements = new List<BuildingElement>();
        }

        public Floor(string id, string name, double height, double z)
        {
            Id = id;
            Name = name;
            Height = height;
            Z = z;
            Elements = new List<BuildingElement>();
        }
    }

    /// <summary>
    /// 建筑模型类（从 SmartDuct 项目移植）
    /// </summary>
    [DataContract]
    public class BuildingModel
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public List<Floor> Floors { get; set; }
        [DataMember] public List<Device> Devices { get; set; }

        public BoundingBox BoundingBox
        {
            get
            {
                if (Floors == null || Floors.Count == 0)
                    return new BoundingBox();

                var min = new Point3D(double.MaxValue, double.MaxValue, double.MaxValue);
                var max = new Point3D(double.MinValue, double.MinValue, double.MinValue);

                foreach (var floor in Floors)
                {
                    foreach (var element in floor.Elements)
                    {
                        if (element.BoundingBox != null)
                        {
                            min.X = Math.Min(min.X, element.BoundingBox.Min.X);
                            min.Y = Math.Min(min.Y, element.BoundingBox.Min.Y);
                            min.Z = Math.Min(min.Z, element.BoundingBox.Min.Z);
                            max.X = Math.Max(max.X, element.BoundingBox.Max.X);
                            max.Y = Math.Max(max.Y, element.BoundingBox.Max.Y);
                            max.Z = Math.Max(max.Z, element.BoundingBox.Max.Z);
                        }
                    }
                }

                return new BoundingBox(min, max);
            }
        }

        public BuildingModel()
        {
            Id = Guid.NewGuid().ToString();
            Name = string.Empty;
            Floors = new List<Floor>();
            Devices = new List<Device>();
        }

        public BuildingModel(string id, string name)
        {
            Id = id;
            Name = name;
            Floors = new List<Floor>();
            Devices = new List<Device>();
        }

        /// <summary>
        /// 获取所有元素数量
        /// </summary>
        public int TotalElementCount
        {
            get
            {
                int count = 0;
                if (Floors != null)
                {
                    foreach (var floor in Floors)
                    {
                        if (floor.Elements != null)
                            count += floor.Elements.Count;
                    }
                }
                return count;
            }
        }

        /// <summary>
        /// 按类型获取元素
        /// </summary>
        public List<BuildingElement> GetElementsByType(BuildingElementType type)
        {
            var result = new List<BuildingElement>();
            if (Floors != null)
            {
                foreach (var floor in Floors)
                {
                    if (floor.Elements != null)
                    {
                        foreach (var element in floor.Elements)
                        {
                            if (element.Type == type)
                                result.Add(element);
                        }
                    }
                }
            }
            return result;
        }
    }

    /// <summary>
    /// 设备类型枚举（从 SmartDuct 项目移植）
    /// </summary>
    [DataContract]
    public enum DeviceType
    {
        [EnumMember] Fan,
        [EnumMember] Vent,
        [EnumMember] AirInlet,
        [EnumMember] AirOutlet,
        [EnumMember] Valve,
        [EnumMember] Filter,
        [EnumMember] AirConditioner,
        [EnumMember] Other
    }

    /// <summary>
    /// 设备类（从 SmartDuct 项目移植）
    /// </summary>
    [DataContract]
    public class Device
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public DeviceType Type { get; set; }
        [DataMember] public Point3D Position { get; set; }
        [DataMember] public BoundingBox Size { get; set; }
        [DataMember] public double FlowRate { get; set; }
        [DataMember] public double Pressure { get; set; }
        [DataMember] public string Note { get; set; }

        public Device()
        {
            Id = Guid.NewGuid().ToString();
            Name = string.Empty;
            Position = new Point3D();
            Size = new BoundingBox();
            Note = string.Empty;
        }

        public Device(string id, string name, DeviceType type, Point3D position, BoundingBox size)
        {
            Id = id;
            Name = name;
            Type = type;
            Position = position;
            Size = size;
            Note = string.Empty;
        }
    }

    /// <summary>
    /// CAD 实体信息类
    /// </summary>
    public class CadEntityInfo
    {
        public string ObjectId { get; set; }
        public string EntityType { get; set; }
        public string Layer { get; set; }
        public string ColorName { get; set; }
        public int ColorIndex { get; set; }
        public double LineWeight { get; set; }
        public string LineType { get; set; }
        public Dictionary<string, object> Properties { get; set; }

        public CadEntityInfo()
        {
            Properties = new Dictionary<string, object>();
        }
    }

    /// <summary>
    /// CAD 图层信息类
    /// </summary>
    public class CadLayerInfo
    {
        public string Name { get; set; }
        public bool IsOn { get; set; }
        public bool IsFrozen { get; set; }
        public bool IsLocked { get; set; }
        public string ColorName { get; set; }
        public int ColorIndex { get; set; }
        public double LineWeight { get; set; }
        public string LineType { get; set; }
    }

    /// <summary>
    /// 线段数据类（用于 CAD 路径提取）
    /// </summary>
    public class LineSegment
    {
        public Point3D Start { get; set; }
        public Point3D End { get; set; }
        public string Layer { get; set; }
        public double Length => Start.DistanceTo(End);

        public LineSegment()
        {
            Start = new Point3D();
            End = new Point3D();
        }

        public LineSegment(Point3D start, Point3D end, string layer = "")
        {
            Start = start;
            End = end;
            Layer = layer;
        }
    }

    /// <summary>
    /// CAD 解析结果
    /// </summary>
    public class CadParseResult
    {
        public bool Success { get; set; }
        public BuildingModel Model { get; set; }
        public List<CadEntityInfo> Entities { get; set; }
        public List<CadLayerInfo> Layers { get; set; }
        public List<LineSegment> WallSegments { get; set; }
        public int TotalEntities { get; set; }
        public int WallCount { get; set; }
        public int RoomCount { get; set; }
        public string Error { get; set; }
        public TimeSpan ParseDuration { get; set; }

        public CadParseResult()
        {
            Model = new BuildingModel();
            Entities = new List<CadEntityInfo>();
            Layers = new List<CadLayerInfo>();
            WallSegments = new List<LineSegment>();
        }
    }
}
