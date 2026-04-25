using HyCADTool.Domain.Models.Road;
using HyCADTool.Shared.Geometry;

namespace HyCADTool.Domain.Services.Road
{
    /// <summary>
    /// 走廊网格生成器（v1 占位 / v2 实现）。
    ///
    /// 职责：将 <see cref="Corridor"/>（Alignment + Profile + Template）按桩号离散化，
    /// 生成 <see cref="Mesh3D"/> 供 Blender 插件消费。
    ///
    /// v1 阶段：
    /// - 在 <c>AutofacModule</c> 中注册 <c>NotImplementedCorridorMeshBuilder</c>，任何调用抛出 <see cref="System.NotImplementedException"/>。
    /// - 允许 UI / 命令层引用本接口（方法签名就位），但不能调用。
    ///
    /// v2 阶段：
    /// - 实现 <c>AutoCad.Services.Road.CorridorMeshBuilder</c>，基于 Template.Points + SamplingStep 生成三角带。
    /// </summary>
    public interface ICorridorMeshBuilder
    {
        /// <summary>
        /// 为指定走廊生成三角网格。
        /// </summary>
        /// <param name="roadDesign">聚合根（用于解析 AlignmentId / TemplateId / ProfileId 引用）。</param>
        /// <param name="corridor">目标走廊。</param>
        /// <returns>三角网格；若无法生成返回 <see cref="Mesh3D.Empty"/>。</returns>
        Mesh3D Build(RoadDesign roadDesign, Corridor corridor);
    }

    /// <summary>
    /// v1 默认占位实现：不做实际计算，抛出 NotImplementedException。
    /// </summary>
    public sealed class NotImplementedCorridorMeshBuilder : ICorridorMeshBuilder
    {
        public Mesh3D Build(RoadDesign roadDesign, Corridor corridor)
        {
            throw new System.NotImplementedException(
                "CorridorMeshBuilder 将在 v2（P7 Blender 同步阶段）实现。" +
                "v1 请使用 AutoCAD 平面出图；3D 查看请等待 Blender 插件上线。");
        }
    }
}
