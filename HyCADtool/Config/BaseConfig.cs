using Autodesk.AutoCAD.ApplicationServices;
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
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            using (doc.LockDocument()) // 锁定当前文档，避免 eLockViolation
            {
                var db = doc.Database;

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    // 注册文字样式
                    TextStyleId = Tools.ZTools.CreateTextStyle(Tools.ZTools.TextStyleConfig.Name);

                    // 注册标注样式
                    DimStyleID = Tools.ZTools.CreateDimStyle(Tools.ZTools.DimStyleConfig.Name);

                    // 注册多重引线样式
                    MleaderStyleId = Tools.ZTools.CreateMLeaderStyle(Tools.ZTools.MLeaderStyleConfig.Name);

                    // 注册表格样式
                    TableStyleId = Tools.ZTools.CreateTableStyle(Tools.ZTools.TableStyleConfig.Name);

                    // 注册默认线型（点划线、虚线等）
                    Tools.ZTools.RegisterStandardLinetypes();

                    // 导入图层配置
                    //ConfigManager.ImportConfigFromCsv( "Layer", "Common");
                    ConfigManager.ImportConfigFromCsv();

                    tr.Commit(); // 提交所有更改
                }
            }
        }

    }
}
