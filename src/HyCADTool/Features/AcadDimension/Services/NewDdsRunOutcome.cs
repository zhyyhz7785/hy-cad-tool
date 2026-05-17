using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Features.AcadDimension.Domain.Boundary;
using HyCADTool.Features.AcadDimension.Domain.Config;
using HyCADTool.Features.AcadDimension.Domain.Context;
using HyCADTool.Features.AcadDimension.Domain.Results;

namespace HyCADTool.Features.AcadDimension.Services
{
    /// <summary>
    /// <see cref="NewDdsService.RunInTransaction"/> 的返回值；nddsR 用其 <see cref="FeatureSignature"/> 与
    /// <see cref="Written"/> 做命令行汇报。
    /// </summary>
    public sealed class NewDdsRunOutcome
    {
        public Polyline AcadPolyline { get; set; }
        public int Polyline2DVertexCount { get; set; }
        public NewDdsConfig Config { get; set; }
        public NewDdsContext Context { get; set; }
        public BoundaryFeatures Features { get; set; }
        public int RawCount { get; set; }
        public NewDdsResult Result { get; set; }
        public string FeatureSignature { get; set; }
        public int Written { get; set; }
    }
}
