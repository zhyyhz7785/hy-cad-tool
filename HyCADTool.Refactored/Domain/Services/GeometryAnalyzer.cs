using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Refactored.Domain.ValueObjects;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;

namespace HyCADTool.Refactored.Domain.Services
{
    /// <summary>
    /// 几何分析服务
    /// 负责分析多边形的边界条件、墙体段分类等
    /// 核心业务逻辑，保持原算法的正确性
    /// </summary>
    public class GeometryAnalyzer
    {
        /// <summary>
        /// 分析多边形的边界条件
        /// 判断每条边是否为土壤边界、是否有相邻多边形、是否为墙体
        /// </summary>
        /// <param name="polygon">待分析的多边形</param>
        /// <param name="polygonElevation">多边形标高</param>
        /// <param name="outerContours">外包轮廓（土壤边界）</param>
        /// <param name="allPolygons">所有多边形（用于查找相邻多边形）</param>
        /// <param name="elevationMap">多边形标高映射</param>
        /// <returns>边界条件列表</returns>
        public List<BoundaryCondition> AnalyzeBoundaryConditions(
            Polygon2D polygon,
            Elevation polygonElevation,
            List<Polygon2D> outerContours,
            Dictionary<Polygon2D, Elevation> elevationMap)
        {
            var conditions = new List<BoundaryCondition>();
            var tolerance = ToleranceSettings.Instance.EdgeCoincidenceTolerance;
            
            // 遍历多边形的每条边
            foreach (var edge in polygon.GetEdges())
            {
                bool isSoilBoundary = false;
                Polygon2D adjacentPolygon = null;
                Elevation adjacentElevation = null;
                Line2D? coincidentEdge = null;
                
                // 步骤1：检查是否为土壤边界
                foreach (var outerContour in outerContours)
                {
                    foreach (var outerEdge in outerContour.GetEdges())
                    {
                        if (IsLineEqual(edge, outerEdge, tolerance) || 
                            IsLineEqual(edge, ReverseEdge(outerEdge), tolerance))
                        {
                            isSoilBoundary = true;
                            break;
                        }
                    }
                    
                    if (isSoilBoundary) break;
                }
                
                // 步骤2：如果不是土壤边界，检查相邻多边形
                if (!isSoilBoundary)
                {
                    foreach (var kvp in elevationMap)
                    {
                        var otherPolygon = kvp.Key;
                        var otherElevation = kvp.Value;
                        
                        // 跳过自己
                        if (otherPolygon == polygon) continue;
                        
                        foreach (var otherEdge in otherPolygon.GetEdges())
                        {
                            if (IsLineEqual(edge, otherEdge, tolerance) || 
                                IsLineEqual(edge, ReverseEdge(otherEdge), tolerance))
                            {
                                adjacentPolygon = otherPolygon;
                                adjacentElevation = otherElevation;
                                coincidentEdge = otherEdge;
                                break;
                            }
                        }
                        
                        if (adjacentPolygon != null) break;
                    }
                }
                
                // 步骤3：判断是否为墙体
                bool isWall = DetermineIsWall(
                    polygonElevation,
                    isSoilBoundary,
                    adjacentElevation);
                
                // 步骤4：创建边界条件
                BoundaryCondition condition;
                if (isSoilBoundary)
                {
                    condition = BoundaryCondition.CreateSoilBoundary(edge, isWall);
                }
                else if (adjacentPolygon != null)
                {
                    condition = BoundaryCondition.CreateAdjacentBoundary(
                        edge,
                        adjacentPolygon,
                        adjacentElevation,
                        isWall,
                        coincidentEdge);
                }
                else
                {
                    condition = BoundaryCondition.CreateNormalBoundary(edge, isWall);
                }
                
                conditions.Add(condition);
            }
            
            return conditions;
        }
        
        /// <summary>
        /// 判断边是否为墙体
        /// 核心规则（保持原逻辑）：
        /// 1. 标高 < 0（地下结构）→ 所有边都是墙体
        /// 2. 标高 ≥ 0 + 有相邻多边形 + 标高不同 → 墙体
        /// 3. 土壤边界 + 标高 ≥ 0 → 非墙体
        /// </summary>
        private bool DetermineIsWall(
            Elevation polygonElevation,
            bool isSoilBoundary,
            Elevation adjacentElevation)
        {
            var tolerance = ToleranceSettings.Instance.ElevationTolerance;
            
            // 规则1：地下结构，所有边都是墙体（包括土壤边界 = 挡土墙）
            if (polygonElevation.IsUnderground)
            {
                return true;
            }
            
            // 规则2：有相邻多边形且标高不同 → 墙体
            if (adjacentElevation != null)
            {
                double elevationDiff = Math.Abs(polygonElevation.Value - adjacentElevation.Value);
                if (elevationDiff > tolerance)
                {
                    return true;
                }
            }
            
            // 规则3：土壤边界 + 地上结构 → 非墙体
            // 或者：无相邻多边形 + 地上结构 → 非墙体
            return false;
        }
        
        /// <summary>
        /// 判断两条线是否相等（考虑容差）
        /// </summary>
        private bool IsLineEqual(Line2D line1, Line2D line2, double tolerance)
        {
            // 检查起点和终点是否在容差范围内相等
            return line1.StartPoint.DistanceTo(line2.StartPoint) < tolerance &&
                   line1.EndPoint.DistanceTo(line2.EndPoint) < tolerance;
        }
        
        /// <summary>
        /// 反转线的方向
        /// </summary>
        private Line2D ReverseEdge(Line2D line)
        {
            return new Line2D(line.EndPoint, line.StartPoint);
        }
        
        /// <summary>
        /// 分类墙体段
        /// 将墙体边分为封闭曲线或开放曲线
        /// </summary>
        public void ClassifyWallSegments(GeometryData geometryData)
        {
            // 只提取墙体边
            var walls = geometryData.WallsOnly.ToList();
            
            if (walls.Count == 0)
            {
                geometryData.SetNoWallSegment();
                return;
            }
            
            // 检查是否为封闭曲线
            bool isClosed = IsClosedCurve(walls);
            
            if (isClosed)
            {
                geometryData.SetClosedCurveSegment(walls.AsReadOnly());
            }
            else
            {
                // 构建开放曲线链
                var chains = BuildOpenCurveChains(walls);
                
                if (chains.Count == 1)
                {
                    geometryData.SetSingleOpenCurveSegment(chains[0]);
                }
                else
                {
                    geometryData.SetMultipleOpenCurveSegments(chains.AsReadOnly());
                }
            }
        }
        
        /// <summary>
        /// 检查墙体边是否形成封闭曲线
        /// </summary>
        private bool IsClosedCurve(List<WallData> walls)
        {
            if (walls.Count < 3) return false;
            
            var tolerance = ToleranceSettings.Instance.DistanceTolerance;
            
            // 检查所有边是否连续
            for (int i = 0; i < walls.Count; i++)
            {
                int nextIndex = (i + 1) % walls.Count;
                var currentEnd = walls[i].Edge.EndPoint;
                var nextStart = walls[nextIndex].Edge.StartPoint;
                
                if (currentEnd.DistanceTo(nextStart) > tolerance)
                {
                    return false; // 不连续
                }
            }
            
            // 检查首尾是否相连
            var firstStart = walls[0].Edge.StartPoint;
            var lastEnd = walls[walls.Count - 1].Edge.EndPoint;
            
            return firstStart.DistanceTo(lastEnd) < tolerance;
        }
        
        /// <summary>
        /// 构建开放曲线链
        /// </summary>
        private List<IReadOnlyList<WallData>> BuildOpenCurveChains(List<WallData> walls)
        {
            var chains = new List<IReadOnlyList<WallData>>();
            var used = new HashSet<int>();
            var tolerance = ToleranceSettings.Instance.DistanceTolerance;
            
            for (int i = 0; i < walls.Count; i++)
            {
                if (used.Contains(i)) continue;
                
                var chain = new List<WallData> { walls[i] };
                used.Add(i);
                
                // 向后查找连接的边
                Point2D currentEnd = walls[i].Edge.EndPoint;
                bool found = true;
                
                while (found)
                {
                    found = false;
                    
                    for (int j = 0; j < walls.Count; j++)
                    {
                        if (used.Contains(j)) continue;
                        
                        Point2D nextStart = walls[j].Edge.StartPoint;
                        
                        if (currentEnd.DistanceTo(nextStart) < tolerance)
                        {
                            chain.Add(walls[j]);
                            used.Add(j);
                            currentEnd = walls[j].Edge.EndPoint;
                            found = true;
                            break;
                        }
                    }
                }
                
                chains.Add(chain.AsReadOnly());
            }
            
            return chains;
        }
        
        /// <summary>
        /// 识别挡土墙
        /// 挡土墙定义：土壤边界 + 地下结构 → 挡土墙
        /// </summary>
        public IEnumerable<WallData> IdentifyRetainingWalls(GeometryData geometryData)
        {
            return geometryData.WallsOnly
                .Where(w => w.IsRetainingWall);
        }
    }
}

