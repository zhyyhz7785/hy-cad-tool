using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.Windows.Data;
using HyCADTool.Log;
using HyCADTool;
using HyCADTool.Commands;
using HyRetainingWallSolver.Command;
//   ^\s*(?=\r?$)\n   (删除空行正则表达式）a
//Cad查询数据命令   (setq ent (entsel)) (setq ent_data (car ent)) (setq ent_data (entget ent_data))
[assembly: CommandClass(typeof(HyRetainingWallSolver.TestCommand))]
namespace HyRetainingWallSolver
{
    public   class TestCommand
    {
        [CommandMethod("te", CommandFlags.Modal | CommandFlags.UsePickSet | CommandFlags.Redraw)]
        public static void Test()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            var doc = Application.DocumentManager.MdiActiveDocument;
            try
            {
                SimpleLogger.LogElapsedTime("star1", () =>
                {
                    // var a = new ElevationModelGenerator();
                    //a.GenerateModel();
                    // HyCommand.DrawRaftThicknessText();
                    // Plugin.RunAnalysis();
                  //HyCommand.DefineWallBoundaries();
                   // HyCommand.ShowWallBoundaries();
                    //HyCommand.RefreshWallBoundaryGraphics();
                    HyCommands.DrawMeshQuads();
                   // HyCommand.TestFixedSymbol();
                    //HyCommand.DefineWallBoundaries();
                    ed.WriteMessage("\nab\n");
                });
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                ed.WriteMessage($"\n{ex}\n");
            }
        }
    }
}
