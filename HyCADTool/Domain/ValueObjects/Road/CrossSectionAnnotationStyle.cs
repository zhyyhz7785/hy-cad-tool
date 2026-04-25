using HyCADTool.Shared.AutoCAD.Xdata;

namespace HyCADTool.Domain.ValueObjects.Road
{
    /// <summary>
    /// rCs 标注绘制样式快照（由设置面板派生，供出图服务消费）。
    /// 样式以「名称键」表达，避免在 Domain 中持有 AutoCAD <c>ObjectId</c>（防跨 Database / 锁外句柄失效）。
    /// </summary>
    public sealed class CrossSectionAnnotationStyle
    {
        /// <summary>主注记文字样式名（如 0-hy-说明-S）；空则出图时不强制设置 DBText.TextStyleId。</summary>
        public string TextStyleName { get; }

        /// <summary>为 true 时，尺寸实体使用当前图 <c>Database.Dimstyle</c>（在绘图事务内读取）。</summary>
        public bool ApplyCurrentDocumentDimStyle { get; }

        /// <summary>标高引线等多行 MLeader 的样式字典名；空则不强制样式。</summary>
        public string MLeaderStyleName { get; }

        public double TextHeightModel { get; }
        public double MLeaderLandingGapModel { get; }
        public double MLeaderArrowSizeModel { get; }

        public string TextLayerName { get; }
        public string DimensionLayerName { get; }
        public string TitleLayerName { get; }

        public int Precision { get; }

        /// <summary>顶部板块竖排名、道路轴线字、北南字等使用的 TrueType 样式名（如 0-hy-说明-T）；空则回退为 <see cref="TextStyleName"/> 解析结果。</summary>
        public string RcsTopStripTextStyleName { get; }

        /// <summary>顶部专用文字的模型高度；≤0 时回退为当前上下文字高。</summary>
        public double RcsTopStripTextHeightModel { get; }

        public CrossSectionAnnotationStyle(
            string textStyleName,
            bool applyCurrentDocumentDimStyle,
            string mLeaderStyleName,
            double textHeightModel,
            double mleaderLandingGapModel,
            double mleaderArrowSizeModel,
            string textLayerName = null,
            string dimensionLayerName = null,
            string titleLayerName = null,
            int precision = 0,
            string rcsTopStripTextStyleName = null,
            double rcsTopStripTextHeightModel = 0)
        {
            TextStyleName = string.IsNullOrWhiteSpace(textStyleName) ? null : textStyleName.Trim();
            ApplyCurrentDocumentDimStyle = applyCurrentDocumentDimStyle;
            MLeaderStyleName = string.IsNullOrWhiteSpace(mLeaderStyleName) ? null : mLeaderStyleName.Trim();
            TextHeightModel = textHeightModel;
            MLeaderLandingGapModel = mleaderLandingGapModel;
            MLeaderArrowSizeModel = mleaderArrowSizeModel;
            TextLayerName = string.IsNullOrWhiteSpace(textLayerName)
                ? HyRoadLayers.CrossSectionAnnotationLayer
                : textLayerName;
            DimensionLayerName = string.IsNullOrWhiteSpace(dimensionLayerName)
                ? HyRoadLayers.CrossSectionDimensionLayer
                : dimensionLayerName;
            TitleLayerName = string.IsNullOrWhiteSpace(titleLayerName)
                ? HyRoadLayers.CrossSectionTitleLayer
                : titleLayerName;
            Precision = precision < 0 ? 0 : precision;
            RcsTopStripTextStyleName = string.IsNullOrWhiteSpace(rcsTopStripTextStyleName)
                ? null
                : rcsTopStripTextStyleName.Trim();
            RcsTopStripTextHeightModel = rcsTopStripTextHeightModel;
        }
    }
}
