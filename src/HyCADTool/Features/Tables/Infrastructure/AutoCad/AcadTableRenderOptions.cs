using HyCAD.Tables.Structure;

namespace HyCADTool.Features.Tables.Infrastructure.AutoCad
{
    /// <summary>
    /// AutoCAD 表格定稿渲染选项（AC2 MVP + AC5 竖排/斜线/角色 + AC6 边框）。
    /// </summary>
    public sealed class AcadTableRenderOptions
    {
        /// <summary>默认选项。</summary>
        public static AcadTableRenderOptions Default { get; } = new AcadTableRenderOptions();

        /// <summary>网格边框图层。</summary>
        public string GridLayerName { get; set; } = "HyTable-Grid";

        /// <summary>文字图层。</summary>
        public string TextLayerName { get; set; } = "HyTable-Text";

        /// <summary>网格图层 ACI 颜色。</summary>
        public short GridLayerColor { get; set; } = 7;

        /// <summary>文字图层 ACI 颜色。</summary>
        public short TextLayerColor { get; set; } = 7;

        /// <summary>默认字高（mm）。</summary>
        public double DefaultTextHeightMm { get; set; } = 3.5;

        /// <summary>文字距单元格内缘（mm）。</summary>
        public double TextPaddingMm { get; set; } = 1.0;

        /// <summary>空值单元格是否仍创建 DBText。</summary>
        public bool DrawEmptyCellText { get; set; }

        /// <summary>模型空间 Z 高程（mm）。</summary>
        public double ZElevation { get; set; }

        // --- AC5 ---

        /// <summary>竖排相邻字中心间距增量（mm）。</summary>
        public double VerticalCharGapMm { get; set; } = 0.5;

        /// <summary>竖排柱在格内水平对齐。</summary>
        public TextAlign VerticalStackAlign { get; set; } = TextAlign.Center;

        /// <summary>竖排块在格内垂直对齐。</summary>
        public TextAlign VerticalBlockVAlign { get; set; } = TextAlign.Center;

        /// <summary>Title 字高乘数。</summary>
        public double TitleTextHeightScale { get; set; } = 1.15;

        /// <summary>Header 字高乘数。</summary>
        public double HeaderTextHeightScale { get; set; } = 1.0;

        /// <summary>Header 伪加粗 WidthFactor。</summary>
        public double HeaderWidthFactor { get; set; } = 1.05;

        /// <summary>Title 强制水平居中。</summary>
        public bool TitleForceCenter { get; set; } = true;

        /// <summary>斜线图层；默认与网格层相同。</summary>
        public string DiagonalLayerName { get; set; }

        /// <summary>斜线图层（未设则同 <see cref="GridLayerName"/>）。</summary>
        public string EffectiveDiagonalLayerName =>
            string.IsNullOrEmpty(DiagonalLayerName) ? GridLayerName : DiagonalLayerName;

        // --- AC6 ---

        /// <summary>BorderSet 边宽为 0 时的内框回退（mm）。</summary>
        public double DefaultInnerBorderWidthMm { get; set; } = 0.35;

        /// <summary>Line.ConstantWidth 下限（mm）。</summary>
        public double MinBorderWidthMm { get; set; } = 0.05;
    }
}
