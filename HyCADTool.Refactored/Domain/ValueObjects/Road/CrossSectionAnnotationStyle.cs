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
            int precision = 0)
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
        }
    }
}
