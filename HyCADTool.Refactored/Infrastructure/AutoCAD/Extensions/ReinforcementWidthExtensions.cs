using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Utilities;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Extensions
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
            {
                #region agent log
                AgentDebugLogger.Log("initial", "H2", "ReinforcementWidthExtensions.ApplyReinforcementWidth", "skip apply width",
                    new
                    {
                        isPolylineNull = polyline == null,
                        inputWidth = width
                    });
                #endregion
                return;
            }

            int segmentCount = polyline.Closed
                ? polyline.NumberOfVertices
                : polyline.NumberOfVertices - 1;

            #region agent log
            AgentDebugLogger.Log("initial", "H2", "ReinforcementWidthExtensions.ApplyReinforcementWidth", "before apply width",
                new
                {
                    inputWidth = width,
                    polyline.Closed,
                    polyline.NumberOfVertices,
                    segmentCount,
                    constantWidthBefore = polyline.ConstantWidth
                });
            #endregion

            polyline.ConstantWidth = width;

            for (int i = 0; i < segmentCount; i++)
            {
                polyline.SetStartWidthAt(i, width);
                polyline.SetEndWidthAt(i, width);
            }

            #region agent log
            AgentDebugLogger.Log("initial", "H4", "ReinforcementWidthExtensions.ApplyReinforcementWidth", "after apply width",
                new
                {
                    inputWidth = width,
                    constantWidthAfter = polyline.ConstantWidth,
                    firstStartWidth = segmentCount > 0 ? polyline.GetStartWidthAt(0) : 0.0,
                    firstEndWidth = segmentCount > 0 ? polyline.GetEndWidthAt(0) : 0.0
                });
            #endregion
        }
    }
}
