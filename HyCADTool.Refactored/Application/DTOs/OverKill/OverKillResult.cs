using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Application.DTOs.OverKill
{
    /// <summary>
    /// OverKill 功能的处理结果
    /// </summary>
    public class OverKillResult
    {
        /// <summary>
        /// 处理后的线段集合
        /// </summary>
        public List<Line2D> ProcessedLines { get; set; }
        
        /// <summary>
        /// 警告标记集合（无法连接的端点）
        /// </summary>
        public List<WarningMarker> WarningMarkers { get; set; }
        
        /// <summary>
        /// 合并的线段数量
        /// </summary>
        public int MergedCount { get; set; }
        
        /// <summary>
        /// 延长的端点数量
        /// </summary>
        public int ExtendedCount { get; set; }
        
        /// <summary>
        /// 处理是否成功
        /// </summary>
        public bool Success { get; set; }
        
        /// <summary>
        /// 处理结果消息
        /// </summary>
        public string Message { get; set; }
    }

    /// <summary>
    /// 警告标记（用于标记无法连接的独立端点）
    /// </summary>
    public class WarningMarker
    {
        /// <summary>
        /// 标记位置（端点坐标）
        /// </summary>
        public Point2D Location { get; set; }
        
        /// <summary>
        /// 标记方向（线段延伸方向）
        /// </summary>
        public Vector2D Direction { get; set; }
        
        /// <summary>
        /// 标记大小（矩形长度）
        /// </summary>
        public double Size { get; set; }
    }
}

