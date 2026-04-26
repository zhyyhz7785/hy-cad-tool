using System;
using HyCADTool.Features.Elevation.Domain.ValueObjects;
using HyCADTool.Shared.Geometry;

namespace HyCADTool.Features.Elevation.Domain.Entities
{
    /// <summary>
    /// 3D墙体实体
    /// 表示一个拉伸后的墙体构件
    /// </summary>
    public class Wall3D : BuildingElement
    {
        /// <summary>
        /// 墙体缓冲区轮廓（2D多边形）
        /// </summary>
        public Polygon2D BufferRegion { get; private set; }
        
        /// <summary>
        /// 墙体厚度
        /// </summary>
        public WallThickness Thickness { get; private set; }
        
        /// <summary>
        /// 内侧标高（基础内部）
        /// </summary>
        public ElevationValue InnerElevation { get; private set; }
        
        /// <summary>
        /// 外侧标高（土壤或相邻基础）
        /// </summary>
        public ElevationValue OuterElevation { get; private set; }
        
        /// <summary>
        /// 是否为挡土墙
        /// </summary>
        public bool IsRetainingWall { get; private set; }
        
        /// <summary>
        /// 是否为地下墙体
        /// </summary>
        public bool IsUnderground => InnerElevation.IsUnderground;
        
        private Wall3D(
            string name,
            string layerName,
            Polygon2D bufferRegion,
            WallThickness thickness,
            ElevationValue innerElevation,
            ElevationValue outerElevation,
            bool isRetainingWall,
            SlabThickness slabThickness = null)
            : base(name, layerName, 
                   CalculateWallBottomElevation(innerElevation, outerElevation, slabThickness),  // bottomElevation
                   CalculateWallTopElevation(innerElevation, outerElevation))                    // topElevation
        {
            BufferRegion = bufferRegion ?? throw new ArgumentNullException(nameof(bufferRegion));
            Thickness = thickness ?? throw new ArgumentNullException(nameof(thickness));
            InnerElevation = innerElevation;
            OuterElevation = outerElevation;
            IsRetainingWall = isRetainingWall;
        }
        
        /// <summary>
        /// 计算墙体底标高
        /// 逻辑：从墙体两侧标高中选择较低的那个，再减去筏板厚度
        /// </summary>
        private static ElevationValue CalculateWallBottomElevation(
            ElevationValue innerElevation, 
            ElevationValue outerElevation, 
            SlabThickness slabThickness)
        {
            // 选择较低的标高
            double lowerElevation = Math.Min(innerElevation.Value, outerElevation.Value);
            
            // 减去筏板厚度（默认400mm）
            double raftThickness = slabThickness?.Value ?? 400.0;
            
            return ElevationValue.FromMillimeters(lowerElevation - raftThickness);
        }
        
        /// <summary>
        /// 计算墙体顶标高
        /// 逻辑：从墙体两侧标高中选择较高的那个
        /// </summary>
        private static ElevationValue CalculateWallTopElevation(
            ElevationValue innerElevation, 
            ElevationValue outerElevation)
        {
            // 选择较高的标高
            return innerElevation.Value >= outerElevation.Value ? innerElevation : outerElevation;
        }
        
        /// <summary>
        /// 创建普通墙体
        /// </summary>
        public static Wall3D CreateNormalWall(
            Polygon2D bufferRegion,
            WallThickness thickness,
            ElevationValue innerElevation,
            ElevationValue outerElevation,
            string layerName = "Bufferid_Wall",
            SlabThickness slabThickness = null)
        {
            return new Wall3D(
                "墙体",
                layerName,
                bufferRegion,
                thickness,
                innerElevation,
                outerElevation,
                false,
                slabThickness);
        }
        
        /// <summary>
        /// 创建挡土墙
        /// </summary>
        public static Wall3D CreateRetainingWall(
            Polygon2D bufferRegion,
            WallThickness thickness,
            ElevationValue innerElevation,
            string layerName = "Bufferid_Wall",
            SlabThickness slabThickness = null)
        {
            // 挡土墙外侧标高固定为地面标高（0.0m）
            return new Wall3D(
                "挡土墙",
                layerName,
                bufferRegion,
                thickness,
                innerElevation,
                ElevationValue.Ground,
                true,
                slabThickness);
        }
        
        /// <summary>
        /// 验证墙体的有效性
        /// </summary>
        public override bool Validate(out string errorMessage)
        {
            if (!base.Validate(out errorMessage))
                return false;
            
            if (BufferRegion == null || !BufferRegion.IsClosed)
            {
                errorMessage = "墙体缓冲区必须是闭合多边形";
                return false;
            }
            
            double area = BufferRegion.GetArea();
            if (area < 1.0) // 面积至少1mm²
            {
                errorMessage = $"墙体缓冲区面积过小：{area:F2}mm²";
                return false;
            }
            
            errorMessage = null;
            return true;
        }
        
        public override string ToString()
        {
            string wallType = IsRetainingWall ? "挡土墙" : "墙体";
            double area = BufferRegion.GetArea();
            return $"{wallType} (厚度: {Thickness}, 高度: {Height:F0}mm, 面积: {area:F2}mm²)";
        }
    }
}

