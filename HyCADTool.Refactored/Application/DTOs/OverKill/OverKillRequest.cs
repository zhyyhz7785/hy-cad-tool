using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Application.DTOs.OverKill
{
    /// <summary>
    /// OverKill 功能的请求参数
    /// </summary>
    public class OverKillRequest
    {
        /// <summary>
        /// 待处理的线段集合
        /// </summary>
        public List<Line2D> Lines { get; set; }
        
        /// <summary>
        /// 几何容差（默认 1e-6）
        /// </summary>
        public double Tolerance { get; set; } = 1e-6;
        
        /// <summary>
        /// 端点延伸距离（默认 10.0）
        /// </summary>
        public double ExtensionDistance { get; set; } = 10.0;
    }
}

