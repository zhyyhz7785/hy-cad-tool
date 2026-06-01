using System.Collections.Generic;
using System.Linq;
using HyCAD.Geometry;
using HyCAD.Geometry.Algorithms;
using HyCADTool.Features.Elevation.Domain.Entities;

namespace HyCADTool.Features.Elevation.Domain.Services
{
    /// <summary>
    /// 相邻边检测器 (性能优化版)
    /// 检测多边形之间的共享边
    /// 优化：预计算容差平方，避免重复计算
    /// </summary>
    public class AdjacencyDetector
    {
        private readonly double _tolerance;
        private readonly double _toleranceSquared;

        /// <summary>
        /// 构造函数
        /// 性能优化：预计算容差平方
        /// </summary>
        /// <param name="tolerance">容差（默认 1.0mm）</param>
        public AdjacencyDetector(double tolerance = 1.0)
        {
            _tolerance = tolerance;
            _toleranceSquared = tolerance * tolerance;
        }

        /// <summary>
        /// 检测所有模型的相邻边
        /// </summary>
        /// <param name="models">模型列表</param>
        /// <returns>字典，key=模型索引，value=该模型的相邻边集合</returns>
        public Dictionary<int, List<Line2D>> DetectAdjacentPolygons(
            List<SurfaceBasedModel> models)
        {
            var result = new Dictionary<int, List<Line2D>>();

            for (int i = 0; i < models.Count; i++)
            {
                var adjacentEdges = FindAdjacentEdges(models[i], models, i);
                if (adjacentEdges.Count > 0)
                {
                    result[i] = adjacentEdges;
                }
            }

            return result;
        }

        /// <summary>
        /// 查找与指定边相邻的模型
        /// </summary>
        /// <param name="edge">要查找的边</param>
        /// <param name="models">所有模型</param>
        /// <param name="currentModelIndex">当前模型索引（排除自己）</param>
        /// <returns>相邻模型，如果没有则返回 null</returns>
        public SurfaceBasedModel FindAdjacentModel(
            Line2D edge,
            List<SurfaceBasedModel> models,
            int currentModelIndex)
        {
            for (int i = 0; i < models.Count; i++)
            {
                if (i == currentModelIndex)
                    continue;

                var polygon = models[i].TopSurface.Polygon;
                if (HasEdge(polygon, edge))
                {
                    return models[i];
                }
            }

            return null;
        }

        /// <summary>
        /// 查找模型的所有相邻边
        /// </summary>
        private List<Line2D> FindAdjacentEdges(
            SurfaceBasedModel model,
            List<SurfaceBasedModel> allModels,
            int modelIndex)
        {
            var adjacentEdges = new List<Line2D>();
            var polygon1 = model.TopSurface.Polygon;

            foreach (var edge in GetEdges(polygon1))
            {
                if (IsAdjacentEdge(edge, allModels, modelIndex))
                {
                    adjacentEdges.Add(edge);
                }
            }

            return adjacentEdges;
        }

        /// <summary>
        /// 判断边是否为相邻边
        /// </summary>
        private bool IsAdjacentEdge(
            Line2D edge,
            List<SurfaceBasedModel> models,
            int excludeIndex)
        {
            for (int i = 0; i < models.Count; i++)
            {
                if (i == excludeIndex)
                    continue;

                var polygon = models[i].TopSurface.Polygon;
                if (HasEdge(polygon, edge))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 判断多边形是否包含指定的边
        /// </summary>
        private bool HasEdge(Polygon2D polygon, Line2D edge)
        {
            foreach (var polyEdge in GetEdges(polygon))
            {
                if (GeometryUtils.AreEdgesEqual(edge, polyEdge, _tolerance))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取多边形的所有边
        /// </summary>
        private IEnumerable<Line2D> GetEdges(Polygon2D polygon)
        {
            for (int i = 0; i < polygon.VertexCount; i++)
            {
                var v1 = polygon.Vertices[i];
                var v2 = polygon.Vertices[(i + 1) % polygon.VertexCount];
                yield return new Line2D(v1, v2);
            }
        }
    }
}
