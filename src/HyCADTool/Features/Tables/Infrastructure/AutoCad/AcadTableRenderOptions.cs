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

        // --- AC8 PhotoSlot ---

        /// <summary>PhotoSlot 占位居中标签。</summary>
        public string PhotoSlotLabelText { get; set; } = "照片";

        /// <summary>PhotoSlot 内框相对格边界内缩（mm）。</summary>
        public double PhotoSlotInsetMm { get; set; } = 2.0;

        /// <summary>PhotoSlot 内线框图层；空则同 <see cref="GridLayerName"/>。</summary>
        public string PhotoSlotInnerLayerName { get; set; }

        /// <summary>PhotoSlot 标签文字图层；空则同 <see cref="TextLayerName"/>。</summary>
        public string PhotoSlotTextLayerName { get; set; }

        /// <summary>PhotoSlot 标签字高（mm）；0 = <see cref="DefaultTextHeightMm"/>。</summary>
        public double PhotoSlotLabelTextHeightMm { get; set; }

        /// <summary>PhotoSlot 内框是否尝试虚线线型。</summary>
        public bool PhotoSlotUseDashedInnerBorder { get; set; }

        /// <summary>PhotoSlot 内框线宽（mm）；0 = <see cref="DefaultInnerBorderWidthMm"/>。</summary>
        public double PhotoSlotInnerBorderWidthMm { get; set; }

        /// <summary>PhotoSlot 内线框图层（有效值）。</summary>
        public string EffectivePhotoSlotInnerLayerName =>
            string.IsNullOrEmpty(PhotoSlotInnerLayerName) ? GridLayerName : PhotoSlotInnerLayerName;

        /// <summary>PhotoSlot 标签图层（有效值）。</summary>
        public string EffectivePhotoSlotTextLayerName =>
            string.IsNullOrEmpty(PhotoSlotTextLayerName) ? TextLayerName : PhotoSlotTextLayerName;

        /// <summary>PhotoSlot 内框线宽（有效值，mm）。</summary>
        public double EffectivePhotoSlotInnerBorderWidthMm =>
            PhotoSlotInnerBorderWidthMm > 0 ? PhotoSlotInnerBorderWidthMm : DefaultInnerBorderWidthMm;
    }
}
