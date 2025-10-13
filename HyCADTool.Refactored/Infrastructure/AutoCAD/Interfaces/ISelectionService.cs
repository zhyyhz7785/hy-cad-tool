using Autodesk.AutoCAD.DatabaseServices;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Interfaces
{
    /// <summary>
    /// 选择服务接口
    /// 提供实体选择、过滤和获取功能
    /// </summary>
    public interface ISelectionService
    {
        /// <summary>
        /// 选择实体
        /// </summary>
        ObjectId[] SelectEntities(string prompt, params string[] entityTypes);

        /// <summary>
        /// 选择所有指定类型的实体
        /// </summary>
        ObjectId[] SelectAll(params string[] entityTypes);

        /// <summary>
        /// 选择直线
        /// </summary>
        List<Line> SelectLines(string prompt = null);

        /// <summary>
        /// 选择圆
        /// </summary>
        List<Circle> SelectCircles(string prompt = null);

        /// <summary>
        /// 选择多段线
        /// </summary>
        List<Polyline> SelectPolylines(string prompt = null);

        /// <summary>
        /// 选择单行文本
        /// </summary>
        List<DBText> SelectTexts(string prompt = null);

        /// <summary>
        /// 选择多行文本
        /// </summary>
        List<MText> SelectMTexts(string prompt = null);

        /// <summary>
        /// 按图层过滤
        /// </summary>
        ObjectId[] FilterByLayer(ObjectId[] baseIds, string layerName);

        /// <summary>
        /// 按颜色过滤
        /// </summary>
        ObjectId[] FilterByColor(ObjectId[] baseIds, short colorIndex);

        /// <summary>
        /// 按线型过滤
        /// </summary>
        ObjectId[] FilterByLineType(ObjectId[] baseIds, string lineTypeName);

        /// <summary>
        /// 按线宽过滤
        /// </summary>
        ObjectId[] FilterByLineWeight(ObjectId[] baseIds, LineWeight lineWeight);

        /// <summary>
        /// 按类型过滤
        /// </summary>
        ObjectId[] FilterByType(ObjectId[] baseIds, string entityType);

        /// <summary>
        /// 获取当前选择集
        /// </summary>
        ObjectId[] GetCurrentSelection();

        /// <summary>
        /// 高亮实体
        /// </summary>
        void HighlightEntities(ObjectId[] objectIds);

        /// <summary>
        /// 取消高亮实体
        /// </summary>
        void UnhighlightEntities(ObjectId[] objectIds);

        /// <summary>
        /// 使用过滤器选择所有实体
        /// </summary>
        ObjectId[] SelectAllWithFilter(string dxfType = null, string layerName = null, short? colorIndex = null, string linetypeName = null, LineWeight? lineWeight = null);

        /// <summary>
        /// 使用过滤器选择实体
        /// </summary>
        ObjectId[] SelectEntitiesWithFilter(string prompt, string dxfType = null, string layerName = null, short? colorIndex = null, string linetypeName = null, LineWeight? lineWeight = null);
    }
}
