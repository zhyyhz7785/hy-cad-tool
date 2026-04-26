using HyCADTool.Domain.Models.Road;

namespace HyCADTool.Domain.Ports.Road
{
    /// <summary>
    /// v1 默认占位：任何调用返回失败结果。
    /// v2 启动（P7）时替换为真实 glTF 导出实现（Infrastructure 层）。
    /// </summary>
    public sealed class NotImplementedThreeDExportPort : IThreeDExportPort
    {
        public bool Supports(ThreeDExportFormat format) => false;

        public ThreeDExportResult Export(RoadDesign roadDesign, ThreeDExportOptions options)
        {
            return ThreeDExportResult.Fail(
                "三维导出功能在 v2（P7 Blender 同步阶段）上线。" +
                "v1 请使用 AutoCAD 平面出图；如需 3D 模型请等待 Blender 插件发布。");
        }
    }
}
