using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.HelpClass.Jig;
using HyCADTool.Tools;
using System;
using static HyCADTool.Tools.ZTools;
[assembly: CommandClass(typeof(HyCADTool.Commands.HyCommand))]
namespace HyCADTool.Commands
{
    public static partial class HyCommand
    {
        [CommandMethod("ggj")]
        public static void ReinOutside()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var poly = db.SelectAEntity<Polyline>();
            if (poly == null)
            {
                ed.WriteMessage("\n请重新选择 Polyline.");
                return;
            }

            Reinforcement.GenerateReinforcementOutside(poly);
            
        }


    }
}
