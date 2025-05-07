using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using CadUtils;

//   ^\s*(?=\r?$)\n   (删除空行正则表达式）
//Cad查询数据命令   (setq ent (entsel)) (setq ent_data (car ent)) (setq ent_data (entget ent_data))
[assembly: CommandClass(typeof(EquipmentFoundation.TestCommand))]
namespace EquipmentFoundation
{
    public static partial class TestCommand
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
                    AddVertexAtIntersections();
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
