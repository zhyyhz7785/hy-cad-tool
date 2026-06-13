using HyCAD.Geometry;
using System.Collections.Generic;

namespace HyCADTool.Features.Cluster.Domain.Services
{
    /// <summary>
    /// 聚类算法服务接口（平台无关）
    /// 提供基于几何距离的聚类算法
    /// </summary>
    public interface IClusteringService
    {
        /// <summary>
        /// 基于边界框距离的聚类算法（BFS）
        /// 将一组边界框根据最大距离阈值进行聚类
        /// </summary>
        /// <typeparam name="T">数据类型</typeparam>
        /// <param name="items">要聚类的项目列表</param>
        /// <param name="getBounds">获取项目边界框的函数</param>
        /// <param name="maxDistance">最大距离阈值</param>
        /// <returns>聚类结果，每个聚类包含一组项目</returns>
        List<List<T>> ClusterByBoundsDistance<T>(
            List<T> items,
            System.Func<T, BoundingBox> getBounds,
            double maxDistance);

        /// <summary>
        /// 基于边界框距离的聚类（空间网格 + 并查集，结果与 BFS 版等价，大图元量下更快）
        /// </summary>
        List<List<T>> ClusterByBoundsDistanceGrid<T>(
            List<T> items,
            System.Func<T, BoundingBox> getBounds,
            double maxDistance);

        /// <summary>
        /// 基于点距离的聚类算法（BFS）
        /// </summary>
        /// <param name="points">点集合</param>
        /// <param name="maxDistance">最大距离阈值</param>
        /// <returns>聚类结果</returns>
        List<List<Point2D>> ClusterByPointDistance(
            List<Point2D> points,
            double maxDistance);
    }
}

