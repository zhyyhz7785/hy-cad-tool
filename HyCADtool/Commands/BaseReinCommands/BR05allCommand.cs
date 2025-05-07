using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
[assembly: CommandClass(typeof(HyCADTool.Command.DrawBaseReinforcement))]
namespace HyCADTool.Command
{
    public static partial class DrawBaseReinforcement
    {
        // CommandMethod 特性表明这是一个 AutoCAD 命令
        [CommandMethod("hyb5a")]
        public static void BR05a()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                var a = BaseRein.ReinforcementStepA();
                BaseRein.ReinforcementAll(a);
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                ed.WriteMessage($"\n{ex}\n");
            }
        }
    }
}
