using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Config;
namespace HyCADTool.Commands
{
    public static partial class HyCommand
    {
        [CommandMethod("gj")]
        public static void DrawReinforcement()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            var doc = Application.DocumentManager.MdiActiveDocument;
            try
            {
                // 更新 ReinPanel 数据
                Reinforcement.Scale = BaseConfig.Scale;
                // 调用 Reinforcement.Rein，传递 ReinPanel 实例
                ed.WriteMessage($"\n比例为{Reinforcement.Scale}");
                Reinforcement.Rein();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                ed.WriteMessage($"\n{ex}\n");
            }
        }
    }
}