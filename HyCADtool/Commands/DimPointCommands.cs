using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Models.Cluster;
using HyCADTool.Models.Annotation;
using HyCADTool.Tools;

namespace HyCADTool.Commands
{
    public static partial class HyCommand
    {
        /// <summary>
        /// 命令：选择图元并绘制分类点集
        /// </summary>
        [CommandMethod("HY_DrawDimPoints")]
        public static void DrawDimensionInputPoints()
        {
            DimPointsAndAxisConfig.IncludeBPs = false;
            DimPointsAndAxisConfig.IncludeABs = false;
            DimPointsAndAxisConfig.IncludeBAPs = false;
            DimPointsAndAxisConfig.IncludeAAPs = true;
            DimPointsAndAxisConfig.IncludeSteelPlatePs = true;
            var data = DimPointsAndAxis.GetInput();
            if (data == null)
            {
                Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n未获取到有效输入。");
                return;
            }
            var dims= DimHelper.Run(data.AllPoints);
            dims.ToSpace();
            //DimPointDrawer.Draw(data);
            Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage("\n分类点集已绘制完成。");
        }
    }
}
