using HyCAD.Geometry;
using System;
using System.Collections.Generic;

namespace HyCAD.Geometry.Offset
{
    /// <summary>
    /// 多边形偏移核心构建器
    /// 代码逻辑源自 Clipper2 库
    /// Original: Clipper2.ClipperOffset.Execute()
    /// License: Boost Software License 1.0
    /// Author: Angus Johnson (Clipper2)
    /// Adapted by: HyCADTool Team
    /// Date: 2025-10-27
    /// </summary>
    internal static class OffsetBuilder
    {
        /// <summary>
        /// 构建偏移多边形
        /// 参考：Clipper2.ClipperOffset.Execute() 主流程
        /// </summary>
        public static List<Point2D> BuildOffset(
            List<Point2D> vertices, 
            double distance, 
            bool isOutward,
            OffsetJoinType joinType)
        {
            if (vertices.Count < 3)
                throw new ArgumentException("多边形至少需要3个顶点");
            
            // 1. 确定偏移方向（基于多边形顺逆时针）
            double signedArea = CalculateSignedArea(vertices);
            bool isCCW = signedArea > 0;
            
            // 调整偏移距离符号
            // Clipper2 逻辑：逆时针多边形，正值=外扩；顺时针多边形，正值=内缩
            double offsetDist = distance;
            if (!isOutward) offsetDist = -offsetDist;
            if (!isCCW) offsetDist = -offsetDist;
            
            // 2. 对每个顶点计算偏移
            var offsetPoints = new List<Point2D>();
            
            for (int i = 0; i < vertices.Count; i++)
            {
                int prevIdx = (i - 1 + vertices.Count) % vertices.Count;
                int nextIdx = (i + 1) % vertices.Count;
                
                var prev = vertices[prevIdx];
                var curr = vertices[i];
                var next = vertices[nextIdx];
                
                // 计算角平分线偏移
                var (bisector, factor) = OffsetNormals.CalculateBisector(
                    prev, curr, next, offsetDist);
                
                // 偏移点
                var offsetPt = new Point2D(
                    curr.X + bisector.X * factor,
                    curr.Y + bisector.Y * factor);
                
                offsetPoints.Add(offsetPt);
                
                // 根据 JoinType 添加额外点（圆角/直角）
                // TODO: Round Join 实现（后续）
                // if (joinType == OffsetJoinType.Round)
                // {
                //     // 在凸角处插入圆弧点
                // }
            }
            
            return offsetPoints;
        }
        
        /// <summary>
        /// 计算有向面积（Shoelace formula）
        /// 参考：Clipper2 内部实现
        /// 
        /// 逻辑：
        /// - 正值：逆时针（CCW）
        /// - 负值：顺时针（CW）
        /// </summary>
        private static double CalculateSignedArea(List<Point2D> vertices)
        {
            double area = 0;
            for (int i = 0; i < vertices.Count; i++)
            {
                int j = (i + 1) % vertices.Count;
                area += (vertices[j].X - vertices[i].X) * (vertices[j].Y + vertices[i].Y);
            }
            return area;
        }
    }
}












