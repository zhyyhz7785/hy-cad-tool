using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
[assembly: CommandClass(typeof(HyCADTool.Command.DrawBaseReinforcement))]
namespace HyCADTool.Command
{
    public static partial class DrawBaseReinforcement
    {
        // CommandMethod 特性表明这是一个 AutoCAD 命令
        [CommandMethod("hyb4")]
        public static void BR04()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                BaseRein.SourceTextAndFinitePoly = BaseRein.SelectFiniteElementGrid();
                BaseRein.SourceTextAndEnvelopePoly = BaseRein.DrawBoundingPolyline(BaseRein.SourceTextAndFinitePoly);//4a;
                BaseRein.GroupByTextAndEnvelopePoly = BaseRein.GroupBySpatialProximity(BaseRein.SourceTextAndEnvelopePoly, BaseRein.proximityThreshold);//4b;
                BaseRein.CreateOptimizedBoundingPolygonFromPolygons(BaseRein.GroupByTextAndEnvelopePoly, "00_hy_调整配筋轮廓");//4c;     
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                ed.WriteMessage($"\n{ex}\n");
            }
        }
    }
}
