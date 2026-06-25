using System;
using System.Runtime.Serialization;

namespace E3DCopilot.Core.Models.Geometry
{
    /// <summary>
    /// 包围盒类，用于碰撞检测（从 SmartDuct 项目移植）
    /// </summary>
    [DataContract]
    public class BoundingBox
    {
        /// <summary>
        /// 最小点
        /// </summary>
        [DataMember]
        public Point3D Min { get; set; }

        /// <summary>
        /// 最大点
        /// </summary>
        [DataMember]
        public Point3D Max { get; set; }

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public BoundingBox()
        {
            Min = new Point3D();
            Max = new Point3D();
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="min">最小点</param>
        /// <param name="max">最大点</param>
        public BoundingBox(Point3D min, Point3D max)
        {
            Min = min;
            Max = max;
        }

        /// <summary>
        /// 计算包围盒的中心
        /// </summary>
        public Point3D Center
        {
            get
            {
                return new Point3D(
                    (Min.X + Max.X) / 2,
                    (Min.Y + Max.Y) / 2,
                    (Min.Z + Max.Z) / 2
                );
            }
        }

        /// <summary>
        /// 计算包围盒的宽度
        /// </summary>
        public double Width => Max.X - Min.X;

        /// <summary>
        /// 计算包围盒的深度
        /// </summary>
        public double Depth => Max.Y - Min.Y;

        /// <summary>
        /// 计算包围盒的高度
        /// </summary>
        public double Height => Max.Z - Min.Z;

        /// <summary>
        /// 检测两个包围盒是否相交
        /// </summary>
        /// <param name="other">另一个包围盒</param>
        /// <returns>是否相交</returns>
        public bool Intersects(BoundingBox other)
        {
            return (Min.X <= other.Max.X && Max.X >= other.Min.X) &&
                   (Min.Y <= other.Max.Y && Max.Y >= other.Min.Y) &&
                   (Min.Z <= other.Max.Z && Max.Z >= other.Min.Z);
        }

        /// <summary>
        /// 检测点是否在包围盒内
        /// </summary>
        /// <param name="point">点</param>
        /// <returns>是否在包围盒内</returns>
        public bool Contains(Point3D point)
        {
            return (point.X >= Min.X && point.X <= Max.X) &&
                   (point.Y >= Min.Y && point.Y <= Max.Y) &&
                   (point.Z >= Min.Z && point.Z <= Max.Z);
        }

        /// <summary>
        /// 扩展包围盒以包含另一个点
        /// </summary>
        /// <param name="point">要包含的点</param>
        public void ExpandToInclude(Point3D point)
        {
            Min.X = Math.Min(Min.X, point.X);
            Min.Y = Math.Min(Min.Y, point.Y);
            Min.Z = Math.Min(Min.Z, point.Z);
            Max.X = Math.Max(Max.X, point.X);
            Max.Y = Math.Max(Max.Y, point.Y);
            Max.Z = Math.Max(Max.Z, point.Z);
        }

        /// <summary>
        /// 扩展包围盒以包含另一个包围盒
        /// </summary>
        /// <param name="other">要包含的包围盒</param>
        public void ExpandToInclude(BoundingBox other)
        {
            ExpandToInclude(other.Min);
            ExpandToInclude(other.Max);
        }
    }
}
