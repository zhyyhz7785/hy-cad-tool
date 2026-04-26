using System;
using System.Collections.Generic;

namespace HyCADTool.Shared.Geometry.Algorithms
{
    /// <summary>
    /// 空间索引服务 - 提供高效的空间查询功能
    /// 使用网格索引（Grid Index）加速邻近对象查询
    /// 平台无关，可用于任何需要空间索引的场景
    /// </summary>
    /// <typeparam name="T">索引对象类型</typeparam>
    public class SpatialIndexService<T>
    {
        /// <summary>
        /// 构建空间网格索引
        /// </summary>
        /// <param name="items">待索引的对象列表</param>
        /// <param name="getBounds">获取对象边界的函数</param>
        /// <param name="gridSize">网格大小</param>
        /// <returns>空间索引字典（网格坐标 -> 对象索引列表）</returns>
        public Dictionary<(long, long), List<int>> BuildIndex(
            List<T> items,
            Func<T, (double minX, double minY, double maxX, double maxY)> getBounds,
            double gridSize)
        {
            var index = new Dictionary<(long, long), List<int>>();

            for (int i = 0; i < items.Count; i++)
            {
                var bounds = getBounds(items[i]);

                // 计算对象占据的网格范围
                long gridMinX = (long)System.Math.Floor(bounds.minX / gridSize);
                long gridMinY = (long)System.Math.Floor(bounds.minY / gridSize);
                long gridMaxX = (long)System.Math.Floor(bounds.maxX / gridSize);
                long gridMaxY = (long)System.Math.Floor(bounds.maxY / gridSize);

                // 将对象添加到所有相关网格
                for (long gx = gridMinX; gx <= gridMaxX; gx++)
                {
                    for (long gy = gridMinY; gy <= gridMaxY; gy++)
                    {
                        var gridKey = (gx, gy);
                        if (!index.ContainsKey(gridKey))
                        {
                            index[gridKey] = new List<int>();
                        }
                        index[gridKey].Add(i);
                    }
                }
            }

            return index;
        }

        /// <summary>
        /// 获取附近的对象索引（基于边界框查询）
        /// </summary>
        /// <param name="bounds">查询边界</param>
        /// <param name="index">空间索引</param>
        /// <param name="gridSize">网格大小</param>
        /// <param name="searchRadius">搜索半径（扩展范围）</param>
        /// <returns>附近对象的索引集合</returns>
        public HashSet<int> GetNearbyIndices(
            (double minX, double minY, double maxX, double maxY) bounds,
            Dictionary<(long, long), List<int>> index,
            double gridSize,
            double searchRadius)
        {
            var result = new HashSet<int>();

            // 扩展搜索范围
            double minX = bounds.minX - searchRadius;
            double maxX = bounds.maxX + searchRadius;
            double minY = bounds.minY - searchRadius;
            double maxY = bounds.maxY + searchRadius;

            // 计算搜索范围的网格坐标
            long gridMinX = (long)System.Math.Floor(minX / gridSize);
            long gridMaxX = (long)System.Math.Floor(maxX / gridSize);
            long gridMinY = (long)System.Math.Floor(minY / gridSize);
            long gridMaxY = (long)System.Math.Floor(maxY / gridSize);

            // 收集所有相关网格中的对象
            for (long gx = gridMinX; gx <= gridMaxX; gx++)
            {
                for (long gy = gridMinY; gy <= gridMaxY; gy++)
                {
                    var gridKey = (gx, gy);
                    if (index.ContainsKey(gridKey))
                    {
                        foreach (int idx in index[gridKey])
                        {
                            result.Add(idx);
                        }
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// 获取附近的对象索引（基于单个对象查询）
        /// </summary>
        /// <param name="item">查询对象</param>
        /// <param name="items">所有对象列表</param>
        /// <param name="getBounds">获取对象边界的函数</param>
        /// <param name="index">空间索引</param>
        /// <param name="gridSize">网格大小</param>
        /// <param name="searchRadius">搜索半径</param>
        /// <returns>附近对象的索引集合</returns>
        public HashSet<int> GetNearbyIndicesForItem(
            T item,
            List<T> items,
            Func<T, (double minX, double minY, double maxX, double maxY)> getBounds,
            Dictionary<(long, long), List<int>> index,
            double gridSize,
            double searchRadius)
        {
            var bounds = getBounds(item);
            return GetNearbyIndices(bounds, index, gridSize, searchRadius);
        }
    }
}

