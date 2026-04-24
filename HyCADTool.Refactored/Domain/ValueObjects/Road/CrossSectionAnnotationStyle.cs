using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata;

namespace HyCADTool.Refactored.Domain.ValueObjects.Road
{
    /// <summary>
    /// rCs 标注绘制样式快照（由设置面板派生，供出图服务直接消费）。
    /// </summary>
    public sealed class CrossSectionAnnotationStyle
    {
        public ObjectId TextStyleId { get; }
        public ObjectId DimensionStyleId { get; }
        public ObjectId MLeaderStyleId { get; }

        public double TextHeightModel { get; }
        public double MLeaderLandingGapModel { get; }
        public double MLeaderArrowSizeModel { get; }

        public string TextLayerName { get; }
        public string DimensionLayerName { get; }
        public string TitleLayerName { get; }

        public int Precision { get; }

        /// <summary>标准横断面顶部「板块竖排名 + 道路左/中/右轴线字 + 北南字」专用文字样式（如 0-hy-说明-T）。为 <see cref="ObjectId.Null"/> 时出图回退为 <see cref="TextStyleId"/>。</summary>
        public ObjectId RcsTopStripTextStyleId { get; }

        /// <summary>上述顶部专用文字的模型空间高度；由纸面高 3mm 经 UnitFactor×主比例 换算。≤0 时回退为 <see cref="TextHeightModel"/>。</summary>
        public double RcsTopStripTextHeightModel { get; }

        public CrossSectionAnnotationStyle(
            ObjectId textStyleId,
            ObjectId dimensionStyleId,
            ObjectId mleaderStyleId,
            double textHeightModel,
            double mleaderLandingGapModel,
            double mleaderArrowSizeModel,
            string textLayerName = null,
            string dimensionLayerName = null,
            string titleLayerName = null,
            int precision = 0,
            ObjectId rcsTopStripTextStyleId = default,
            double rcsTopStripTextHeightModel = 0)
        {
            TextStyleId = textStyleId;
            DimensionStyleId = dimensionStyleId;
            MLeaderStyleId = mleaderStyleId;
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
            RcsTopStripTextStyleId = rcsTopStripTextStyleId;
            RcsTopStripTextHeightModel = rcsTopStripTextHeightModel;
        }
    }
}
