using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
[assembly: CommandClass(typeof(HyCADTool.Commands.DrawBaseReinforcement))]
namespace HyCADTool.Commands
{
    public static partial class DrawBaseReinforcement
    {
        // CommandMethod 特性表明这是一个 AutoCAD 命令
        [CommandMethod("hyb6")]
        public static void BR06()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                BaseRein.Poly4Dim();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                ed.WriteMessage($"\n{ex}\n");
            }
        }
    }
}
