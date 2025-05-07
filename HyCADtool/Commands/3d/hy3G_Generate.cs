using Autodesk.AutoCAD.Runtime;
using HyCADTool.HelpClass.CreatBase;
namespace HyCADTool.Command
{
    public static partial class HyCommand
    {
        [CommandMethod("hy3_Generate")]
        ///选择yjk墙体水平配筋 《输入值的处理
        public static void ElevationModelGenerato()
        {
            var a = new ElevationModelGenerator();

            a.GenerateWalls();
            a.GenerateRaftAndBase();
        }
    }
}
