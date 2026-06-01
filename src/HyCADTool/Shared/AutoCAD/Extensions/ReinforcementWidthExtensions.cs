using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Shared.AutoCAD.Utilities;

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
                    constantWidthBefore = SafeConstantWidth(polyline)
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
                    constantWidthAfter = SafeConstantWidth(polyline),
                    firstStartWidth = segmentCount > 0 ? polyline.GetStartWidthAt(0) : 0.0,
                    firstEndWidth = segmentCount > 0 ? polyline.GetEndWidthAt(0) : 0.0
                });
            #endregion
        }

        /// <summary>
        /// 安全读取 <see cref="Polyline.ConstantWidth"/>。
        /// 多段线含逐段宽度（钢筋弯钩/双线常用 SetStartWidthAt/SetEndWidthAt 设宽）而非统一宽时，
        /// 其 getter 抛 <c>eInvalidInput</c>；此处吞掉返回 0，仅供调试日志使用，避免提交流程被中断。
        /// </summary>
        private static double SafeConstantWidth(Polyline polyline)
        {
            try
            {
                return polyline.ConstantWidth;
            }
            catch (System.Exception)
            {
                return 0.0;
            }
        }
    }
}
