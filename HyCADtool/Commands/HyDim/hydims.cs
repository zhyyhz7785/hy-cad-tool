using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.HelpClass;
using HyCADTool.Tools;
using System.Linq;
namespace HyCADTool.Commands
{
    public static partial class HyCommand
    {
        [CommandMethod("dds")]
        ///选择yjk墙体水平配筋 《输入值的处理
        public static void DimPoly()
        {
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            var poly = HyTool.SelectAEntity<Polyline>(db);
            DimensionForReinforcement.GenerateDimension(poly);
        }
        [CommandMethod("ddss")]
        public static void DimPolys()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            var doc = Application.DocumentManager.MdiActiveDocument;
            var filter = AcTv.Polyline.Getfilter();
            var polyIds = HyTool.SelectWithFilter(filter, doc, ed);
            if (polyIds == null || polyIds.Length == 0)
            {
                ed.WriteMessage("\nSelection canceled or no polylines selected. Exiting.\n");
                return;
            }
            foreach (var polyId in polyIds)
            {
                var poly = (Polyline)polyId.IdToEntity();
                DimensionForReinforcement.GenerateDimension(poly);
            }
        }
    }
}
