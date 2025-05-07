using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
[assembly: CommandClass(typeof(HyCADTool.Command.DrawBaseReinforcement))]
namespace HyCADTool.Command
{
    public static partial class DrawBaseReinforcement
    {
        // CommandMethod 特性表明这是一个 AutoCAD 命令
        [CommandMethod("hyb3")]
        public static void BR03()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                BaseRein.SourceTextAndFinitePoly = BaseRein.SelectFiniteElementGrid();
            }
            catch (Autodesk.AutoCAD.Runtime.Exception ex)
            {
                ed.WriteMessage($"\n{ex}\n");
            }
        }
    }
}
