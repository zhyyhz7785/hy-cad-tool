using Autodesk.AutoCAD.DatabaseServices;

namespace HyCADTool.Shared.AutoCAD.Selection.Filters
{
    /// <summary>
    /// 实体过滤器接口
    /// Entity Filter Interface
    /// </summary>
    public interface IEntityFilter
    {
        /// <summary>
        /// 过滤器键（唯一标识）
        /// Filter key (unique identifier)
        /// </summary>
        string Key { get; }

        /// <summary>
        /// 应用过滤器
        /// Apply filter
        /// </summary>
        /// <param name="selectedEntity">选中的实体 Selected entity</param>
        /// <param name="baseIds">基础实体集 Base entity IDs</param>
        /// <returns>过滤后的实体集 Filtered entity IDs</returns>
        ObjectId[] Apply(Entity selectedEntity, ObjectId[] baseIds);
    }
}
