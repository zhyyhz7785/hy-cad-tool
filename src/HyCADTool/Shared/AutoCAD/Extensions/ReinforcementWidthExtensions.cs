using Autodesk.AutoCAD.DatabaseServices;

namespace HyCADTool.Shared.AutoCAD.Extensions
{
    /// <summary>
    /// 钢筋多段线宽度统一处理。
    /// 所有钢筋相关命令最终生成的线宽都应来自钢筋面板参数：PolylineWidth * Scale。
    /// </summary>
    public static class ReinforcementWidthExtensions
    {
        public static void ApplyReinforcementWidth(this Polyline polyline, double width)
        {
            if (polyline == null || width <= 0)
                return;

            int segmentCount = polyline.Closed
                ? polyline.NumberOfVertices
                : polyline.NumberOfVertices - 1;

            polyline.ConstantWidth = width;

            for (int i = 0; i < segmentCount; i++)
            {
                polyline.SetStartWidthAt(i, width);
                polyline.SetEndWidthAt(i, width);
            }
        }
    }
}
