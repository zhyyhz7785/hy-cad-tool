using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Tools;
using System;

namespace HyCADTool.Config
{
    public static class BaseConfig
    {
        public static double ToleranceDouble { get; } = 1e-2;
        public static Tolerance ToleranceVec { get; } = new Tolerance(Tolerance.Global.EqualVector, 1e-2);
        public static Tolerance TolerancePoint { get; } = new Tolerance(Tolerance.Global.EqualPoint, 1e-2);
        public static double ElevationLength { get; set; } = 2;

        private static double _scale = 50;
        public static double Scale
        {
            get => _scale;
            set
            {
                if (value > 0 && Math.Abs(_scale - value) > 1e-9)
                {
                    _scale = value;
                }
            }
        }

        public static ObjectId TextStyleId { get; set; } = new ObjectId();
        public static ObjectId DimStyleID { get; set; } = new ObjectId();
        public static ObjectId MleaderStyleId { get; set; } = new ObjectId();
        public static ObjectId TableStyleId { get; set; } = new ObjectId();

        public static void InitializeStyle()
        {
            TextStyleId = Et.CreateTextStyle(Et.TextStyleConfig.Name);
            DimStyleID = Et.CreateDimStyle(Et.DimStyleConfig.Name);
            MleaderStyleId = Et.CreateMLeaderStyle(Et.MLeaderStyleConfig.Name);
            TableStyleId = Et.CreateTableStyle(Et.TableStyleConfig.Name);
            ConfigManager.ImportConfigFromCsv("Layer", "Common");
            // 添加默认图层（例如 Common 类别）

        }
    }
}
