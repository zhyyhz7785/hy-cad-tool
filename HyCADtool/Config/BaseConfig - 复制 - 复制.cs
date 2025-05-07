using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Tools;
using System;
namespace HyCADTool.Config
{
    // 添加在namespace级别
    //public enum CellAlignment
    //{
    //    TopLeft = 1,
    //    TopCenter = 2,
    //    TopRight = 3,
    //    MiddleLeft = 4,
    //    MiddleCenter = 5,
    //    MiddleRight = 6,
    //    BottomLeft = 7,
    //    BottomCenter = 8,
    //    BottomRight = 9
    //}
    public static class BaseConfig
    {
        public static double ToleranceDouble { get; } = 1e-6;
        public static Tolerance ToleranceVec { get; } = new Tolerance(Tolerance.Global.EqualVector, 1e-6);
        public static Tolerance TolerancePoint { get; } = new Tolerance(Tolerance.Global.EqualPoint, 1e-6);
        public static double ElevationLength { get; set; } = 2;//标高三角形长度，2等边的边长
        private static double _scale = 50; // 默认值 40
        public static double Scale
        {
            get => _scale;
            set
            {
                if (value > 0 && Math.Abs(_scale - value) > 1e-9)
                {
                    _scale = value;
                    // 不再需要 UpdateStyleNames，名称动态计算
                }
            }
        }
        public static ObjectId TextStyleId { get; set; } = new ObjectId();
        public static ObjectId DimStyleID { get; set; } = new ObjectId();
        public static ObjectId MleaderStyleId { get; set; } = new ObjectId();
        public static ObjectId TableStyleId { get; set; } = new ObjectId();
        public static void InitializeStyle()
        {
            // 注意：此方法应由 ReinPanel 或 Reinforcement 在需要时调用
            TextStyleId = EtGpt.CreateTextStyle(TextStyleConfig.Name);
            DimStyleID = EtGpt.CreateDimStyle(DimStyleConfig.Name);
            MleaderStyleId = EtGpt.CreateMLeaderStyle(MLeaderStyleConfig.Name);
            //TableStyleId = EtGpt.CreateTableStyle(TableStyleConfig.Name);
            LayerConfigManager.ImportLayersFromMarkdown();
        }
        public static class TextStyleConfig
        {
            public static string Name => $"0_Hy_{BaseConfig.Scale}";
            public static string BigFontFileName { get; } = "hztxt.shx";
            public static string FontFileName { get; } = "tssdeng.shx";
            public static double TextSize { get; } = 2.5;
            public static double TextXScale { get; } = 0.7;
        }
        public static class DimStyleConfig
        {
            public static string Name => $"0_Hy_{BaseConfig.Scale}_Dim";
            public static string TextStyleName => $"0_Hy_{BaseConfig.Scale}";
            public static int Dimtdec { get; } = 0;
            public static double Dimexo { get; } = 1.0;
            public static double Dimexe { get; } = 1.0;
            public static double Dimdle { get; } = 0.5;
            public static double Dimtxt { get; } = 2.5;
            public static double Dimgap { get; } = 1.0;
            public static double Dimasz { get; } = 1.0;
            public static int Dimdec { get; } = 0;
        }
        public static class MLeaderStyleConfig
        {
            public static string Name => $"0_Hy_{BaseConfig.Scale}_Mleader";
            public static string TextStyleName => $"0_Hy_{BaseConfig.Scale}";
        }
        public static class TableStyleConfig
        {
            public static string Name => $"0_Hy_{BaseConfig.Scale}_Table";
            public static string TextStyleName => TextStyleConfig.Name;

            // 表格行设置
            public static double TitleRowHeight { get; } = 8;   // 标题行高度
            public static double DataRowHeight { get; } = 6;     // 数据行高度

            // 颜色索引 (ACI颜色)
            public static int TitleRowColorIndex { get; } = 1;   // 红色标题行
            public static int DataRowColorIndex { get; } = 7;   // 白色数据行

            // 对齐方式
            public static CellAlignment TitleHorizontalAlignment { get; } = CellAlignment.MiddleCenter;
            public static CellAlignment DataHorizontalAlignment { get; } = CellAlignment.MiddleLeft;

            // 边距设置
            public static double CellHorizontalMargin { get; } = 0.5;
            public static double CellVerticalMargin { get; } = 0.3;

            // 网格线设置
            public static double GridLineWeight { get; } = 0.15; // 线宽(mm)
            public static int GridColorIndex { get; } = 7;       // 白色网格线
        }
    }
}