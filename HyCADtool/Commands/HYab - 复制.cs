using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.HelpClass;
namespace HyCADTool.Command
{
    public static partial class HyCommand
    {
        [CommandMethod("abdr_DrawRectCluster")]
        public static void HY_DrawRectCluster()
        {
            var points = PointClusterHelper.SelectPointsOrCircles();
            if (points.Count == 0) return;
            var helper = PointClusterHelper.CreateWithStaticConfig(points);
            helper.DrawExpandedEnvelopes();
        }
        [CommandMethod("abdh_DrawHullCluster")]
        public static void HY_DrawHullCluster()
        {
            var points = PointClusterHelper.SelectPointsOrCircles();
            if (points.Count == 0) return;
            var helper = PointClusterHelper.CreateWithStaticConfig(points);
            helper.DrawClusterConvexHulls();
        }
        [CommandMethod("abdd_DimBolts")]
        public static void HY_DimBolts()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            using (doc.LockDocument())
            {
                PointClusterHelper.GenerateBoltDimensions();
            }
        }
    }
}
