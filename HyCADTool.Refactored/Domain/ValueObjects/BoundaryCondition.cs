using HyCADTool.Refactored.Domain.ValueObjects.Geometry;

namespace HyCADTool.Refactored.Domain.ValueObjects
{
    /// <summary>
    /// 边界条件值对象
    /// 描述多边形边的类型和相邻关系
    /// </summary>
    public class BoundaryCondition
    {
        /// <summary>
        /// 边线
        /// </summary>
        public Line2D Edge { get; }
        
        /// <summary>
        /// 是否为土壤边界（与外包轮廓重合）
        /// </summary>
        public bool IsSoilBoundary { get; }
        
        /// <summary>
        /// 相邻多边形（如果有）
        /// </summary>
        public Polygon2D AdjacentPolygon { get; }
        
        /// <summary>
        /// 相邻多边形的标高（如果有）
        /// </summary>
        public Elevation AdjacentElevation { get; }
        
        /// <summary>
        /// 是否为墙体边
        /// </summary>
        public bool IsWall { get; }
        
        /// <summary>
        /// 重合的边（相邻多边形的对应边）
        /// </summary>
        public Line2D CoincidentEdge { get; }
        
        private BoundaryCondition(
            Line2D edge,
            bool isSoilBoundary,
            Polygon2D adjacentPolygon,
            Elevation adjacentElevation,
            bool isWall,
            Line2D? coincidentEdge)
        {
            Edge = edge;
            IsSoilBoundary = isSoilBoundary;
            AdjacentPolygon = adjacentPolygon;
            AdjacentElevation = adjacentElevation;
            IsWall = isWall;
            CoincidentEdge = coincidentEdge ?? default(Line2D);
        }
        
        /// <summary>
        /// 创建土壤边界条件
        /// </summary>
        public static BoundaryCondition CreateSoilBoundary(Line2D edge, bool isWall)
        {
            return new BoundaryCondition(
                edge,
                isSoilBoundary: true,
                adjacentPolygon: null,
                adjacentElevation: null,
                isWall: isWall,
                coincidentEdge: default(Line2D));
        }
        
        /// <summary>
        /// 创建相邻多边形边界条件
        /// </summary>
        public static BoundaryCondition CreateAdjacentBoundary(
            Line2D edge,
            Polygon2D adjacentPolygon,
            Elevation adjacentElevation,
            bool isWall,
            Line2D? coincidentEdge)
        {
            return new BoundaryCondition(
                edge,
                isSoilBoundary: false,
                adjacentPolygon: adjacentPolygon,
                adjacentElevation: adjacentElevation,
                isWall: isWall,
                coincidentEdge: coincidentEdge);
        }
        
        /// <summary>
        /// 创建普通边界条件（既不是土壤边界，也没有相邻多边形）
        /// </summary>
        public static BoundaryCondition CreateNormalBoundary(Line2D edge, bool isWall)
        {
            return new BoundaryCondition(
                edge,
                isSoilBoundary: false,
                adjacentPolygon: null,
                adjacentElevation: null,
                isWall: isWall,
                coincidentEdge: default(Line2D));
        }
        
        public override string ToString()
        {
            if (IsSoilBoundary)
                return $"土壤边界 (IsWall: {IsWall})";
            else if (AdjacentPolygon != null)
                return $"相邻边界 (标高: {AdjacentElevation?.ToFormattedString()}, IsWall: {IsWall})";
            else
                return $"普通边界 (IsWall: {IsWall})";
        }
    }
}

