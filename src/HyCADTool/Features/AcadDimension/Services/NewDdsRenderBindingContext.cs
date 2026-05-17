using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Features.AcadDimension.Domain.Config;

namespace HyCADTool.Features.AcadDimension.Services
{
    /// <summary>
    /// Renderer 绑定上下文：由 <see cref="NewDdsService"/> 在单次 Execute 内填入，
    /// 供 <see cref="AcadDimensionRenderer"/> 写入扩展字典。
    /// </summary>
    public sealed class NewDdsRenderBindingContext
    {
        public ObjectId SourcePolylineId { get; set; }

        public string FeatureSignature { get; set; }

        public NewDdsConfig ConfigSnapshot { get; set; }

        public bool IsBindingEnabled =>
            !SourcePolylineId.IsNull && !SourcePolylineId.IsErased
            && !string.IsNullOrEmpty(FeatureSignature);
    }
}
