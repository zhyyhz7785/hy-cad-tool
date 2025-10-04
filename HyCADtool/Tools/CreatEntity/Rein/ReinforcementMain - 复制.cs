using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Config;
using HyCADTool.Log;
using HyCADTool.Tools;
using HyCADTool.Views;
using System.Collections.Generic;
namespace HyCADTool
{
    public static partial class Reinforcement
    {
        public static double ToleranceDouble => BaseConfig.ToleranceDouble;
        public static Tolerance ToleranceVec => BaseConfig.ToleranceVec;
        public static Tolerance TolerancePoint => BaseConfig.TolerancePoint;
        private static double _scale = 40.0;
        public static double Scale
        {
            get
            {
                if (ReinPanel.ActivePanel != null && double.TryParse(ReinPanel.ActivePanel.ScaleText.Text, out double value) && value > 0)
                {
                    return value;
                }
                return _scale;
            }
            set
            {
                _scale = value;
            }
        }
        #region 属性区域
        public static double RebarDiameter => ReinPanel.ActivePanel?.RebarDiameter ?? 14.0;
        public static double RebarSpacing => ReinPanel.ActivePanel?.RebarSpacing ?? 200.0;
        public static double AnchorageLength => ReinPanel.ActivePanel?.AnchorageLength ?? 500.0;
        public static double DotSeparation => ReinPanel.ActivePanel?.DotSeparation ?? 200.0;
        public static double BendingLineMinLength => ReinPanel.ActivePanel?.BendingLineMinLength ?? 150.0;
        public static double BendingLineLength { get; set; } = 200.0;
        public static double AnchorageJoinLength => ReinPanel.ActivePanel?.AnchorageJoinLength ?? 1500.0;
        public static double TextSize => (ReinPanel.ActivePanel?.TextSize ?? 3.0) * Scale;
        public static double TextXScale => ReinPanel.ActivePanel?.TextXScale ?? 0.7;
        public static double DimDistanceTolerance => ReinPanel.ActivePanel?.DimDistanceTolerance ?? 30.0;
        public static double DimensionDistanceOutside => (ReinPanel.ActivePanel?.DimensionDistanceOutside ?? 14.0) * Scale;
        //public static double DimensionDistance => (ReinPanel.ActivePanel?.DimensionDistance ?? 4.0) * Scale;
        public static double DimensionDistanceInside => (ReinPanel.ActivePanel?.DimensionDistanceInside ?? 6.0) * Scale;
        public static double DimensionDistanceWithDim => (ReinPanel.ActivePanel?.DimensionDistanceWithDim ?? 6.0) * Scale;
        public static double MleaderDistance => (ReinPanel.ActivePanel?.MleaderDistance ?? 6.0) * Scale;
        public static double ReinforcementDiameter => (ReinPanel.ActivePanel?.ReinforcementDiameter ?? 0.35) * Scale;
        public static double DotReinOffset => (ReinPanel.ActivePanel?.DotReinOffset ?? 1.35) * Scale;
        public static double DotReinOffsetOut => (ReinPanel.ActivePanel?.DotReinOffset-1 ?? 0.35) * Scale;
        public static double HookLength => (ReinPanel.ActivePanel?.HookLength ?? 1.0) * Scale;
        public static double ProtectionThickness => (ReinPanel.ActivePanel?.ProtectionThickness ?? 1.0) * Scale;
        public static string[] LayerNames { get; set; }
        private static ObjectId[] LayerIds { get; set; }
        #endregion
        #region 其他属性
        public static Polyline[] SubReinforcements { get; set; }
        public static Polyline[] SubReinforcementWithAnchors { get; set; }
        public static Polyline[] SubReinforcementWithAnchorsAddhooks { get; set; }
        public static List<Dictionary<int, bool>> IsReinforcementBending { get; set; }
        public static Polyline Boundary { get; set; }
        public static Polyline[] DotRein { get; set; }
        public static Polyline DotReinCenterPoly { get; set; }
        public static Polyline[] ReduceDotRein { get; set; }
        public static Point3d[] DotReinPoints { get; set; }
        public static Point3d[] ReduceDotReinPoints { get; set; }
        public static double DotStartDistance { get; set; }
        public static MLeader[] Mleaders { get; set; }
        #endregion
        static Reinforcement()
        {
        }
        public static void Rein()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            //if (ReinPanel.ActivePanel == null)
            //{
            //    ed.WriteMessage("\n未找到活动 ReinPanel。\n");
            //    return;
            //}
            if (Scale <= 0)
            {
                ed.WriteMessage("\n比例值无效，使用默认值 40。\n");
                // Scale 是只读，默认已在 getter 中处理
            }
            var poly = db.SelectAEntity<Polyline>();
            if (poly == null)
            {
                ed.WriteMessage("\n请重新选择 Polyline.");
                return;
            }
            SimpleLogger.Log("Starting Rein method");
            SimpleLogger.LogElapsedTime("GenerateReinforcement", () =>
            {
                Reinforcement.GenerateReinforcement(poly);
            });
            SimpleLogger.LogElapsedTime("GenerateDimension", () =>
            {
                DimensionForReinforcement.GenerateDimension(poly);
            });
            SimpleLogger.Log("Rein method completed");
        }
    }
}