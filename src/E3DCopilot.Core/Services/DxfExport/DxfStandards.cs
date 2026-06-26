using System.Collections.Generic;
using netDxf;
using netDxf.Tables;

namespace E3DCopilot.Core.Services.DxfExport
{
    /// <summary>
    /// DXF 出图标准定义
    /// </summary>
    public static class DxfStandards
    {
        #region 图层名称

        public const string LAYER_STRUCTURE = "STRUCTURE";
        public const string LAYER_STRUCTURE_HIDDEN = "STRUCTURE-HIDDEN";
        public const string LAYER_CENTERLINE = "CENTERLINE";
        public const string LAYER_DIMENSION = "DIMENSION";
        public const string LAYER_TEXT = "TEXT";
        public const string LAYER_TITLE_BLOCK = "TITLE-BLOCK";
        public const string LAYER_FRAME = "FRAME";

        #endregion

        #region 文字高度

        public const double TEXT_HEIGHT_TITLE = 5.0;
        public const double TEXT_HEIGHT_NORMAL = 2.5;
        public const double TEXT_HEIGHT_SMALL = 2.0;

        #endregion

        #region 线宽值（单位：1/100 mm）

        public const short LINEWEIGHT_NORMAL = 25;
        public const short LINEWEIGHT_BOLD = 50;
        public const short LINEWEIGHT_THIN = 13;

        #endregion

        #region 颜色定义

        public static readonly AciColor COLOR_WHITE = new AciColor(7);
        public static readonly AciColor COLOR_RED = new AciColor(1);
        public static readonly AciColor COLOR_GREEN = new AciColor(3);
        public static readonly AciColor COLOR_GRAY = new AciColor(8);

        #endregion

        #region 线型定义

        public const string LINETYPE_CONTINUOUS = "Continuous";
        public const string LINETYPE_CENTER = "CENTER";
        public const string LINETYPE_HIDDEN = "HIDDEN";

        #endregion

        #region 图层创建

        public static Layer CreateLayer(string name, AciColor color)
        {
            return new Layer(name) { Color = color };
        }

        public static List<Layer> GetStandardLayers()
        {
            return new List<Layer>
            {
                CreateLayer(LAYER_STRUCTURE, COLOR_WHITE),
                CreateLayer(LAYER_STRUCTURE_HIDDEN, COLOR_GRAY),
                CreateLayer(LAYER_CENTERLINE, COLOR_RED),
                CreateLayer(LAYER_DIMENSION, COLOR_GREEN),
                CreateLayer(LAYER_TEXT, COLOR_WHITE),
                CreateLayer(LAYER_TITLE_BLOCK, COLOR_WHITE)
            };
        }

        #endregion

        #region 比例换算

        public static double ParseScale(string scale)
        {
            if (string.IsNullOrEmpty(scale))
                return 1.0;

            var parts = scale.Split(':');
            if (parts.Length == 2 &&
                double.TryParse(parts[0], out double numerator) &&
                double.TryParse(parts[1], out double denominator))
            {
                return numerator / denominator;
            }

            if (double.TryParse(scale, out double directScale))
                return directScale;

            return 1.0;
        }

        #endregion
    }
}
