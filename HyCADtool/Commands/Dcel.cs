using Autodesk.AutoCAD.Runtime;
using HyCADTool.HelpClass.DCEL;
namespace HyCADTool.Commands
{
    public static partial class HyCommand
    {
        [CommandMethod("hyDcel")]
        ///选择yjk墙体水平配筋 《输入值的处理
        public static void Dcel()
        {
            DrawDCEL.CreateDCELPolylinesFromCurves();
        }
    }
}
