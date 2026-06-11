using Autodesk.AutoCAD.DatabaseServices;

namespace HyCADTool.Shared.AutoCAD.Extensions
{
    /// <summary>
    /// 钢筋多段线宽度统一处理。
    /// 所有钢筋相关命令最终生成的线宽都应来自钢筋面板参数：PolylineWidth * Scale。
    /// </summary>
    public static class ReinforcementWidthExtensions
    {
        /// <summary>解析多段线当前有效线宽（ConstantWidth 或首段 Start/End 宽）。</summary>
        public static double ResolveEffectiveWidth(Polyline polyline)
        {
            if (polyline == null || polyline.NumberOfVertices < 1)
                return 0;

            double constantWidth = TryGetConstantWidth(polyline);
            if (constantWidth > 0)
                return constantWidth;

            int segIdx = polyline.NumberOfVertices >= 2 ? 0 : 0;
            double endWidth = polyline.GetEndWidthAt(segIdx);
            if (endWidth > 0)
                return endWidth;

            double startWidth = polyline.GetStartWidthAt(segIdx);
            if (startWidth > 0)
                return startWidth;

            return 0;
        }

        private static double TryGetConstantWidth(Polyline polyline)
        {
            try
            {
                return polyline.ConstantWidth;
            }
            catch (System.Exception)
            {
                return 0;
            }
        }

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
