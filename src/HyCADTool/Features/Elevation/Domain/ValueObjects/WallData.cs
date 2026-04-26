using HyCADTool.Shared.Geometry;

namespace HyCADTool.Features.Elevation.Domain.ValueObjects
{
    /// <summary>
    /// 墙体数据值对象
    /// 描述单条墙体边的所有信息
    /// </summary>
    public class WallData
    {
        /// <summary>
        /// 墙体边线
        /// </summary>
        public Line2D Edge { get; }
        
        /// <summary>
        /// 内侧标高（基础内部）
        /// </summary>
        public ElevationValue InnerElevation { get; }
        
        /// <summary>
        /// 外侧标高（土壤或相邻基础）
        /// </summary>
        public ElevationValue OuterElevation { get; }
        
        /// <summary>
        /// 墙体厚度
        /// </summary>
        public WallThickness Thickness { get; }
        
        /// <summary>
        /// 边界条件
        /// </summary>
        public BoundaryCondition Boundary { get; }
        
        /// <summary>
        /// 是否为墙体
        /// </summary>
        public bool IsWall => Boundary.IsWall;
        
        /// <summary>
        /// 是否为挡土墙
        /// </summary>
        public bool IsRetainingWall => Boundary.IsSoilBoundary && IsWall;
        
        /// <summary>
        /// 端点类型（用于开放曲线的端点处理）
        /// </summary>
        public EndType EndType { get; }
        
        /// <summary>
        /// 墙体高度（mm）
        /// </summary>
        public double Height => OuterElevation.Value - InnerElevation.Value;
        
        private WallData(
            Line2D edge,
            ElevationValue innerElevation,
            ElevationValue outerElevation,
            WallThickness thickness,
            BoundaryCondition boundary,
            EndType endType)
        {
            Edge = edge;
            InnerElevation = innerElevation;
            OuterElevation = outerElevation;
            Thickness = thickness;
            Boundary = boundary;
            EndType = endType;
        }
        
        /// <summary>
        /// 创建墙体数据
        /// </summary>
        public static WallData Create(
            Line2D edge,
            ElevationValue innerElevation,
            ElevationValue outerElevation,
            WallThickness thickness,
            BoundaryCondition boundary,
            EndType endType = EndType.Polygon)
        {
            return new WallData(
                edge,
                innerElevation,
                outerElevation,
                thickness,
                boundary,
                endType);
        }
        
        /// <summary>
        /// 创建挡土墙数据（外侧标高固定为地面标高）
        /// </summary>
        public static WallData CreateRetainingWall(
            Line2D edge,
            ElevationValue innerElevation,
            WallThickness thickness,
            BoundaryCondition boundary,
            EndType endType = EndType.Polygon)
        {
            return new WallData(
                edge,
                innerElevation,
                ElevationValue.Ground, // 挡土墙外侧标高固定为0.0m
                thickness,
                boundary,
                endType);
        }
        
        public override string ToString()
        {
            string wallType = IsRetainingWall ? "挡土墙" : "墙体";
            return $"{wallType} (长度: {Edge.Length:F0}mm, 高度: {Height:F0}mm, 厚度: {Thickness})";
        }
    }
    
    /// <summary>
    /// 端点类型（用于开放曲线的端点处理）
    /// </summary>
    public enum EndType
    {
        /// <summary>
        /// 多边形端点（默认）
        /// </summary>
        Polygon,
        
        /// <summary>
        /// 连接端点（与其他墙体连接）
        /// </summary>
        Joined,
        
        /// <summary>
        /// 平头端点（不延伸）
        /// </summary>
        Butt,
        
        /// <summary>
        /// 方形端点（延伸 thickness/2）
        /// </summary>
        Square,
        
        /// <summary>
        /// 圆形端点（圆弧端点）
        /// </summary>
        Round
    }
}














