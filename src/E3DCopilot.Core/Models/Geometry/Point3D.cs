using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace E3DCopilot.Core.Models.Geometry
{
    /// <summary>
    /// 三维点坐标类（从 SmartDuct 项目移植）
    /// </summary>
    [DataContract]
    public class Point3D
    {
        /// <summary>
        /// X坐标
        /// </summary>
        [DataMember]
        public double X { get; set; }

        /// <summary>
        /// Y坐标
        /// </summary>
        [DataMember]
        public double Y { get; set; }

        /// <summary>
        /// Z坐标
        /// </summary>
        [DataMember]
        public double Z { get; set; }

        /// <summary>
        /// 默认构造函数
        /// </summary>
        public Point3D()
        {
            X = 0;
            Y = 0;
            Z = 0;
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="x">X坐标</param>
        /// <param name="y">Y坐标</param>
        /// <param name="z">Z坐标</param>
        public Point3D(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        /// <summary>
        /// 计算两点之间的距离
        /// </summary>
        /// <param name="other">另一个点</param>
        /// <returns>距离</returns>
        public double DistanceTo(Point3D other)
        {
            double dx = X - other.X;
            double dy = Y - other.Y;
            double dz = Z - other.Z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>
        /// 从字符串解析点坐标，格式：(x, y, z)
        /// </summary>
        public static Point3D Parse(string str)
        {
            if (string.IsNullOrWhiteSpace(str))
                return new Point3D();

            str = str.Trim('(', ')', ' ');
            var parts = str.Split(',');
            if (parts.Length >= 3)
            {
                double.TryParse(parts[0].Trim(), out double x);
                double.TryParse(parts[1].Trim(), out double y);
                double.TryParse(parts[2].Trim(), out double z);
                return new Point3D(x, y, z);
            }
            return new Point3D();
        }

        /// <summary>
        /// 重写ToString方法
        /// </summary>
        /// <returns>点的字符串表示</returns>
        public override string ToString()
        {
            return $"({X:0.00}, {Y:0.00}, {Z:0.00})";
        }

        /// <summary>
        /// 转换为数组 [x, y, z]
        /// </summary>
        public double[] ToArray()
        {
            return new double[] { X, Y, Z };
        }

        /// <summary>
        /// 从数组创建 Point3D
        /// </summary>
        public static Point3D FromArray(double[] arr)
        {
            if (arr == null || arr.Length < 3)
                return new Point3D();
            return new Point3D(arr[0], arr[1], arr[2]);
        }
    }
}
