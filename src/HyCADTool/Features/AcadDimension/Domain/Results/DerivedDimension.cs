using HyCAD.Geometry;

namespace HyCADTool.Features.AcadDimension.Domain.Results
{
    /// <summary>
    /// Domain 层标注 DTO（独立于 AutoCAD RotatedDimension）。
    /// Renderer 在 Infrastructure 层把 DerivedDimension 转换为 RotatedDimension 写入模型空间。
    /// </summary>
    public sealed class DerivedDimension
    {
        /// <summary>第一界线点（XLine1Point）。</summary>
        public Point2D ExtensionLine1Point { get; set; }

        /// <summary>第二界线点（XLine2Point）。</summary>
        public Point2D ExtensionLine2Point { get; set; }

        /// <summary>尺寸线点（DimLinePoint）。</summary>
        public Point2D DimensionLinePoint { get; set; }

        /// <summary>旋转角度（弧度）。0 = 水平标注；π/2 = 竖直标注。</summary>
        public double Rotation { get; set; }

        /// <summary>来源分类（Outside-Left/Right/Up/Down/Total，Inside-LR/UD）。供 Q 函数与去重逻辑使用。</summary>
        public DimensionSource Source { get; set; }
    }

    public enum DimensionSource
    {
        OutsideLeft,
        OutsideRight,
        OutsideUp,
        OutsideDown,
        OutsideTotalLeft,
        OutsideTotalRight,
        OutsideTotalUp,
        OutsideTotalDown,
        InsideLeftRight,
        InsideUpDown
    }
}
