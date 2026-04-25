using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Shared.AutoCAD.Selection.Filters;
using System.Collections.Generic;

namespace HyCADTool.Shared.AutoCAD.Selection
{
    /// <summary>
    /// 过滤器管理服务接口
    /// Filter Manager Service Interface
    /// </summary>
    public interface IFilterManagerService
    {
        /// <summary>
        /// 注册一个过滤器
        /// Register a filter
        /// </summary>
        /// <param name="filter">要注册的过滤器 Filter to register</param>
        void RegisterFilter(IEntityFilter filter);

        /// <summary>
        /// 应用过滤器并更新当前选择集
        /// Apply filter and update current selection
        /// </summary>
        /// <param name="filterKey">过滤器键 Filter key</param>
        /// <param name="isEnabled">是否启用 Whether enabled</param>
        /// <param name="selectedEntity">选中的实体 Selected entity</param>
        /// <param name="baseIds">基础实体集 Base entity IDs</param>
        /// <param name="currentIds">当前实体集（输出） Current entity IDs (output)</param>
        void ApplyFilter(string filterKey, bool isEnabled, Entity selectedEntity, ObjectId[] baseIds, out ObjectId[] currentIds);

        /// <summary>
        /// 重置过滤器状态
        /// Reset filter state
        /// </summary>
        void Reset();

        /// <summary>
        /// 获取所有已注册的过滤器键
        /// Get all registered filter keys
        /// </summary>
        IEnumerable<string> GetRegisteredFilterKeys();

        /// <summary>
        /// 获取当前选择集
        /// Get current selection
        /// </summary>
        ObjectId[] GetCurrentSelection();

        /// <summary>
        /// 设置基础选择集
        /// Set base selection
        /// </summary>
        /// <param name="baseIds">基础实体集 Base entity IDs</param>
        void SetBaseSelection(ObjectId[] baseIds);
    }
}

