#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Shared.Geometry;

namespace HyCADTool.Domain.ValueObjects
{
    /// <summary>
    /// 几何数据值对象 - 数据中心枢纽
    /// 
    /// 核心职责：
    /// 1. 整合外部输入（多边形、标高、墙体边界）为结构化数据
    /// 2. 提供高速索引访问（字典查找 O(1)）
    /// 3. 预处理墙体段分类（封闭/开放曲线）
    /// 4. 支持大规模工业项目（1000+ 复杂多边形）
    /// </summary>
    public class GeometryData
    {
        /// <summary>
        /// 原始多边形
        /// </summary>
        public Polygon2D Polygon { get; }
        
        /// <summary>
        /// 多边形标高
        /// </summary>
        public Elevation Elevation { get; }
        
        /// <summary>
        /// 所有墙体边（包括墙体和非墙体）
        /// </summary>
        public IReadOnlyList<WallData> AllWalls { get; }
        
        /// <summary>
        /// 筏板厚度
        /// </summary>
        public SlabThickness SlabThickness { get; }
        
        /// <summary>
        /// 墙体段类型
        /// </summary>
        public WallSegmentType SegmentType { get; private set; }
        
        /// <summary>
        /// 封闭曲线墙体段
        /// </summary>
        public IReadOnlyList<WallData> ClosedCurveSegment { get; private set; }
        
        /// <summary>
        /// 单条开放曲线墙体段
        /// </summary>
        public IReadOnlyList<WallData> SingleOpenCurveSegment { get; private set; }
        
        /// <summary>
        /// 多条开放曲线墙体段
        /// </summary>
        public IReadOnlyList<IReadOnlyList<WallData>> MultipleOpenCurveSegments { get; private set; }
        
        // ============================================
        // 高性能索引（字典）- 支持大规模工业项目
        // ============================================
        
        /// <summary>
        /// 墙体边索引：Line2D -> WallData
        /// 用于快速查询墙体数据 O(1)
        /// </summary>
        private readonly Dictionary<Line2D, WallData> _wallIndex;
        
        /// <summary>
        /// 只包含墙体边的缓存列表（预处理）
        /// 避免重复 LINQ 查询，提升性能
        /// </summary>
        private readonly List<WallData> _wallsOnlyCache;
        
        /// <summary>
        /// 只包含墙体边的列表（高性能访问）
        /// </summary>
        public IEnumerable<WallData> WallsOnly => _wallsOnlyCache;
        
        private GeometryData(
            Polygon2D polygon,
            Elevation elevation,
            IReadOnlyList<WallData> allWalls,
            SlabThickness slabThickness)
        {
            Polygon = polygon ?? throw new ArgumentNullException(nameof(polygon));
            Elevation = elevation ?? throw new ArgumentNullException(nameof(elevation));
            AllWalls = allWalls ?? throw new ArgumentNullException(nameof(allWalls));
            SlabThickness = slabThickness ?? throw new ArgumentNullException(nameof(slabThickness));
            
            // 初始化时未分类
            SegmentType = WallSegmentType.Unclassified;
            ClosedCurveSegment = new List<WallData>().AsReadOnly();
            SingleOpenCurveSegment = new List<WallData>().AsReadOnly();
            MultipleOpenCurveSegments = new List<IReadOnlyList<WallData>>().AsReadOnly();
            
            // ============================================
            // 预处理：构建高性能索引（数据中心核心功能）
            // ============================================
            
            // 1. 构建墙体边索引字典 O(n)
            _wallIndex = new Dictionary<Line2D, WallData>(new Line2DEqualityComparer());
            _wallsOnlyCache = new List<WallData>();
            
            foreach (var wall in allWalls)
            {
                if (wall.IsWall && wall.Thickness.Value > 0)
                {
                    _wallIndex[wall.Edge] = wall;
                    _wallsOnlyCache.Add(wall);
                }
            }
        }
        
        /// <summary>
        /// 创建几何数据
        /// </summary>
        public static GeometryData Create(
            Polygon2D polygon,
            Elevation elevation,
            IReadOnlyList<WallData> allWalls,
            SlabThickness slabThickness)
        {
            return new GeometryData(polygon, elevation, allWalls, slabThickness);
        }
        
        /// <summary>
        /// 设置墙体段分类结果（封闭曲线）
        /// </summary>
        public void SetClosedCurveSegment(IReadOnlyList<WallData> segment)
        {
            SegmentType = WallSegmentType.ClosedCurve;
            ClosedCurveSegment = segment;
            SingleOpenCurveSegment = new List<WallData>().AsReadOnly();
            MultipleOpenCurveSegments = new List<IReadOnlyList<WallData>>().AsReadOnly();
        }
        
        /// <summary>
        /// 设置墙体段分类结果（单条开放曲线）
        /// </summary>
        public void SetSingleOpenCurveSegment(IReadOnlyList<WallData> segment)
        {
            SegmentType = WallSegmentType.SingleOpenCurve;
            ClosedCurveSegment = new List<WallData>().AsReadOnly();
            SingleOpenCurveSegment = segment;
            MultipleOpenCurveSegments = new List<IReadOnlyList<WallData>>().AsReadOnly();
        }
        
        /// <summary>
        /// 设置墙体段分类结果（多条开放曲线）
        /// </summary>
        public void SetMultipleOpenCurveSegments(IReadOnlyList<IReadOnlyList<WallData>> segments)
        {
            SegmentType = WallSegmentType.MultipleOpenCurves;
            ClosedCurveSegment = new List<WallData>().AsReadOnly();
            SingleOpenCurveSegment = new List<WallData>().AsReadOnly();
            MultipleOpenCurveSegments = segments;
        }
        
        /// <summary>
        /// 设置无墙体段
        /// </summary>
        public void SetNoWallSegment()
        {
            SegmentType = WallSegmentType.None;
            ClosedCurveSegment = new List<WallData>().AsReadOnly();
            SingleOpenCurveSegment = new List<WallData>().AsReadOnly();
            MultipleOpenCurveSegments = new List<IReadOnlyList<WallData>>().AsReadOnly();
        }
        
        // ============================================
        // 高性能查询方法（数据中心核心功能）
        // ============================================
        
        /// <summary>
        /// 快速查询墙体边的厚度 O(1)
        /// </summary>
        /// <param name="edge">墙体边</param>
        /// <returns>墙体厚度，如果不是墙体返回 null</returns>
        public WallThickness? GetWallThickness(Line2D edge)
        {
            if (_wallIndex.TryGetValue(edge, out var wallData))
            {
                return wallData.Thickness;
            }
            return null;
        }
        
        /// <summary>
        /// 快速查询墙体数据 O(1)
        /// </summary>
        /// <param name="edge">墙体边</param>
        /// <returns>墙体数据，如果不是墙体返回 null</returns>
        public WallData GetWallData(Line2D edge)
        {
            _wallIndex.TryGetValue(edge, out var wallData);
            return wallData;
        }
        
        /// <summary>
        /// 快速获取所有墙体边和厚度 O(n)
        /// </summary>
        /// <returns>墙体边和厚度的枚举</returns>
        public IEnumerable<(Line2D Edge, WallThickness Thickness)> GetWallEdgesAndThickness()
        {
            foreach (var wall in _wallsOnlyCache)
            {
                yield return (wall.Edge, wall.Thickness);
            }
        }
        
        /// <summary>
        /// 获取墙体数量（预处理结果）
        /// </summary>
        public int WallCount => _wallsOnlyCache.Count;
        
        /// <summary>
        /// 获取总边数
        /// </summary>
        public int TotalEdgeCount => AllWalls.Count;
        
        public override string ToString()
        {
            return $"几何数据 (标高: {Elevation.ToFormattedString()}, 墙体边: {WallCount}/{TotalEdgeCount}, 类型: {SegmentType})";
        }
    }
    
    /// <summary>
    /// 墙体段类型
    /// </summary>
    public enum WallSegmentType
    {
        /// <summary>
        /// 未分类
        /// </summary>
        Unclassified,
        
        /// <summary>
        /// 无墙体段
        /// </summary>
        None,
        
        /// <summary>
        /// 封闭曲线（所有墙体边连续且首尾相连）
        /// </summary>
        ClosedCurve,
        
        /// <summary>
        /// 单条开放曲线
        /// </summary>
        SingleOpenCurve,
        
        /// <summary>
        /// 多条开放曲线
        /// </summary>
        MultipleOpenCurves
    }
    
    /// <summary>
    /// Line2D 相等性比较器（用于字典索引）
    /// 支持高性能墙体边查找 O(1)
    /// </summary>
    internal class Line2DEqualityComparer : IEqualityComparer<Line2D>
    {
        private const double Tolerance = 1e-6;
        
        public bool Equals(Line2D x, Line2D y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x == null || y == null) return false;
            
            // 比较起点和终点（考虑公差）
            return PointEquals(x.StartPoint, y.StartPoint) && 
                   PointEquals(x.EndPoint, y.EndPoint);
        }
        
        public int GetHashCode(Line2D obj)
        {
            if (obj == null) return 0;
            
            // 使用起点和终点的哈希码
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + GetPointHashCode(obj.StartPoint);
                hash = hash * 31 + GetPointHashCode(obj.EndPoint);
                return hash;
            }
        }
        
        private bool PointEquals(Point2D p1, Point2D p2)
        {
            return Math.Abs(p1.X - p2.X) < Tolerance && 
                   Math.Abs(p1.Y - p2.Y) < Tolerance;
        }
        
        private int GetPointHashCode(Point2D point)
        {
            // 对坐标进行取整，确保相近的点有相同的哈希码
            int xHash = ((int)(point.X / Tolerance)).GetHashCode();
            int yHash = ((int)(point.Y / Tolerance)).GetHashCode();
            return xHash ^ yHash;
        }
    }
}



