using Autodesk.AutoCAD.DatabaseServices;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Domain.Interfaces
{
    /// <summary>
    /// 选择服务接口 - 提供统一的实体选择和过滤功能
    /// </summary>
    /// <remarks>
    /// 该接口封装了 AutoCAD 的选择操作，提供类型安全和平台无关的选择方法。
    /// 设计原则：
    /// 1. 简化选择操作 - 提供类型化的选择方法
    /// 2. 支持链式过滤 - 可对选择结果进行多条件过滤
    /// 3. 平台无关 - 领域层不直接依赖 AutoCAD API
    /// </remarks>
    public interface ISelectionService
    {
        #region 基础选择方法

        /// <summary>
        /// 提示用户选择实体（带过滤条件）
        /// </summary>
        /// <param name="prompt">提示信息</param>
        /// <param name="entityTypes">实体类型过滤（如 "LINE", "CIRCLE", "LWPOLYLINE"）</param>
        /// <returns>选中的实体 ObjectId 数组，取消则返回空数组</returns>
        ObjectId[] SelectEntities(string prompt, params string[] entityTypes);

        /// <summary>
        /// 选择所有符合条件的实体（不提示用户）
        /// </summary>
        /// <param name="entityTypes">实体类型过滤</param>
        /// <returns>符合条件的实体 ObjectId 数组</returns>
        ObjectId[] SelectAll(params string[] entityTypes);

        #endregion

        #region 类型化选择方法

        /// <summary>
        /// 选择线段（Line）
        /// </summary>
        /// <param name="prompt">提示信息，默认为 "请选择线段"</param>
        /// <returns>选中的 Line 实体列表</returns>
        List<Line> SelectLines(string prompt = null);

        /// <summary>
        /// 选择圆（Circle）
        /// </summary>
        /// <param name="prompt">提示信息，默认为 "请选择圆"</param>
        /// <returns>选中的 Circle 实体列表</returns>
        List<Circle> SelectCircles(string prompt = null);

        /// <summary>
        /// 选择多段线（Polyline/LWPolyline）
        /// </summary>
        /// <param name="prompt">提示信息，默认为 "请选择多段线"</param>
        /// <returns>选中的 Polyline 实体列表</returns>
        List<Polyline> SelectPolylines(string prompt = null);

        /// <summary>
        /// 选择文本（DBText）
        /// </summary>
        /// <param name="prompt">提示信息，默认为 "请选择文本"</param>
        /// <returns>选中的 DBText 实体列表</returns>
        List<DBText> SelectTexts(string prompt = null);

        /// <summary>
        /// 选择多行文本（MText）
        /// </summary>
        /// <param name="prompt">提示信息，默认为 "请选择多行文本"</param>
        /// <returns>选中的 MText 实体列表</returns>
        List<MText> SelectMTexts(string prompt = null);

        #endregion

        #region 过滤方法

        /// <summary>
        /// 按图层过滤实体
        /// </summary>
        /// <param name="baseIds">基础实体 ObjectId 数组</param>
        /// <param name="layerName">图层名称</param>
        /// <returns>过滤后的 ObjectId 数组</returns>
        ObjectId[] FilterByLayer(ObjectId[] baseIds, string layerName);

        /// <summary>
        /// 按颜色索引过滤实体
        /// </summary>
        /// <param name="baseIds">基础实体 ObjectId 数组</param>
        /// <param name="colorIndex">颜色索引（1-255）</param>
        /// <returns>过滤后的 ObjectId 数组</returns>
        ObjectId[] FilterByColor(ObjectId[] baseIds, short colorIndex);

        /// <summary>
        /// 按线型过滤实体
        /// </summary>
        /// <param name="baseIds">基础实体 ObjectId 数组</param>
        /// <param name="lineTypeName">线型名称（如 "Continuous", "Dashed"）</param>
        /// <returns>过滤后的 ObjectId 数组</returns>
        ObjectId[] FilterByLineType(ObjectId[] baseIds, string lineTypeName);

        /// <summary>
        /// 按线宽过滤实体
        /// </summary>
        /// <param name="baseIds">基础实体 ObjectId 数组</param>
        /// <param name="lineWeight">线宽值</param>
        /// <returns>过滤后的 ObjectId 数组</returns>
        ObjectId[] FilterByLineWeight(ObjectId[] baseIds, LineWeight lineWeight);

        /// <summary>
        /// 按实体类型过滤
        /// </summary>
        /// <param name="baseIds">基础实体 ObjectId 数组</param>
        /// <param name="entityType">实体类型名称（如 "LINE", "CIRCLE"）</param>
        /// <returns>过滤后的 ObjectId 数组</returns>
        ObjectId[] FilterByType(ObjectId[] baseIds, string entityType);

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取当前选择集中的实体 ObjectId 数组
        /// </summary>
        /// <returns>当前选择集的 ObjectId 数组，无选择则返回空数组</returns>
        ObjectId[] GetCurrentSelection();

        /// <summary>
        /// 高亮显示指定的实体
        /// </summary>
        /// <param name="objectIds">要高亮的实体 ObjectId 数组</param>
        void HighlightEntities(ObjectId[] objectIds);

        /// <summary>
        /// 取消高亮显示
        /// </summary>
        /// <param name="objectIds">要取消高亮的实体 ObjectId 数组</param>
        void UnhighlightEntities(ObjectId[] objectIds);

        #endregion
    }
}

