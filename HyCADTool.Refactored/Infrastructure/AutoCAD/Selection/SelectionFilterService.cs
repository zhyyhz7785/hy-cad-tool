using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Refactored.Domain.Interfaces;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Selection
{
    /// <summary>
    /// 选择集过滤服务的 AutoCAD 实现
    /// </summary>
    public class SelectionFilterService : ISelectionFilterService
    {
        public SelectionFilter Build(string dxfType = null, string layerName = null, short? colorIndex = null, string linetypeName = null, LineWeight? lineWeight = null)
        {
            var builder = new SelectionFilterBuilder();
            if (!string.IsNullOrEmpty(dxfType)) builder.ByType(dxfType);
            if (!string.IsNullOrEmpty(layerName)) builder.ByLayer(layerName);
            if (colorIndex.HasValue) builder.ByColorIndex(colorIndex.Value);
            if (!string.IsNullOrEmpty(linetypeName)) builder.ByLinetype(linetypeName);
            if (lineWeight.HasValue) builder.ByLineWeight(lineWeight.Value);
            return builder.BuildFilter();
        }

        public SelectionFilter BuildFromEntity(Entity prototype, bool byLayer = false, bool byColor = false, bool byLinetype = false, bool byLineWeight = false, bool byType = false)
        {
            var builder = new SelectionFilterBuilder();
            if (prototype == null)
            {
                return builder.BuildFilter();
            }

            if (byType)
            {
                var dxf = prototype.GetRXClass().DxfName;
                if (!string.IsNullOrEmpty(dxf)) builder.ByType(dxf);
            }
            if (byLayer)
            {
                if (!string.IsNullOrEmpty(prototype.Layer)) builder.ByLayer(prototype.Layer);
            }
            if (byColor)
            {
                builder.ByColorIndex((short)prototype.ColorIndex);
            }
            if (byLinetype)
            {
                if (!string.IsNullOrEmpty(prototype.Linetype)) builder.ByLinetype(prototype.Linetype);
            }
            if (byLineWeight)
            {
                builder.ByLineWeight(prototype.LineWeight);
            }

            return builder.BuildFilter();
        }
    }
}


