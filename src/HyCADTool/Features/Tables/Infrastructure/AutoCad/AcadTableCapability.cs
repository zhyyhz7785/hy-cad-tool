using HyCAD.Tables.Adapters;

namespace HyCADTool.Features.Tables.Infrastructure.AutoCad
{
    /// <summary>
    /// AutoCAD 线+文字渲染能力（对齐 HtmlTableAdapter / 005 §6.2）。
    /// </summary>
    public sealed class AcadTableCapability
    {
        public static AcadTableCapability LineFrameDefaults { get; } = new AcadTableCapability
        {
            VerticalStacked = AdapterFeatureLevel.Full,
            DiagonalSplit = AdapterFeatureLevel.Degraded,
            RichStyles = AdapterFeatureLevel.Degraded,
            // 内线框 + 居中标签；无图片/OLE/Hatch（Html role-photoslot 的 Degraded 含灰底填色）
            PhotoSlot = AdapterFeatureLevel.Degraded,
        };

        public AdapterFeatureLevel VerticalStacked { get; set; }
        public AdapterFeatureLevel DiagonalSplit { get; set; }
        public AdapterFeatureLevel RichStyles { get; set; }
        public AdapterFeatureLevel PhotoSlot { get; set; }
    }
}
