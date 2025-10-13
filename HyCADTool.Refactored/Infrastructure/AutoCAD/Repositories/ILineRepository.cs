using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Repositories
{
    /// <summary>
    /// 线段仓储接口
    /// 负责线段的 AutoCAD 数据库交互
    /// </summary>
    public interface ILineRepository
    {
        /// <summary>
        /// 从选择集获取线段
        /// </summary>
        List<(Line2D Line, ObjectId OriginalId)> GetSelectedLines(SelectionSet selection);
        
        /// <summary>
        /// 替换线段（删除旧线段，添加新线段）
        /// </summary>
        void ReplaceLines(
            Transaction transaction,
            List<ObjectId> oldLineIds,
            List<Line2D> newLines);
        
        // TODO: Application层删除后暂时注释掉 WarningMarker 相关功能
        // /// <summary>
        // /// 创建警告标记（矩形）
        // /// </summary>
        // void CreateWarningMarkers(
        //     Transaction transaction,
        //     List<WarningMarker> markers);
    }
}

