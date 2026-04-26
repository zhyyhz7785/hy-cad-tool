using System;
using HyCADTool.Features.Elevation.Domain.ValueObjects;
using HyCADTool.Shared.Geometry;

namespace HyCADTool.Features.Elevation.Domain.Entities
{
    /// <summary>
    /// 3D筏板实体
    /// 表示基础筏板（Raft Slab）
    /// 注意：筏板和底板是同一事物
    /// </summary>
    public class Slab3D : BuildingElement
    {
        /// <summary>
        /// 筏板平面轮廓（2D多边形）
        /// </summary>
        public Polygon2D Region { get; private set; }
        
        /// <summary>
        /// 筏板厚度
        /// </summary>
        public SlabThickness Thickness { get; private set; }
        
        /// <summary>
        /// 基础标高（筏板顶部）
        /// </summary>
        public ElevationValue BaseElevation { get; private set; }
        
        /// <summary>
        /// 筏板面积（mm²）
        /// </summary>
        public double Area => Region.GetArea();
        
        private Slab3D(
            string name,
            string layerName,
            Polygon2D region,
            SlabThickness thickness,
            ElevationValue baseElevation)
            : base(
                name,
                layerName,
                ElevationValue.FromMillimeters(baseElevation.Value - thickness.Value), // 底部标高
                baseElevation) // 顶部标高
        {
            Region = region ?? throw new ArgumentNullException(nameof(region));
            Thickness = thickness ?? throw new ArgumentNullException(nameof(thickness));
            BaseElevation = baseElevation;
        }
        
        /// <summary>
        /// 创建筏板
        /// </summary>
        /// <param name="region">筏板平面轮廓</param>
        /// <param name="thickness">筏板厚度</param>
        /// <param name="baseElevation">基础标高（筏板顶部）</param>
        /// <param name="layerName">图层名称</param>
        public static Slab3D Create(
            Polygon2D region,
            SlabThickness thickness,
            ElevationValue baseElevation,
            string layerName = "Bufferid_Slab")
        {
            return new Slab3D(
                "筏板",
                layerName,
                region,
                thickness,
                baseElevation);
        }
        
        /// <summary>
        /// 验证筏板的有效性
        /// </summary>
        public override bool Validate(out string errorMessage)
        {
            if (!base.Validate(out errorMessage))
                return false;
            
            if (Region == null || !Region.IsClosed)
            {
                errorMessage = "筏板轮廓必须是闭合多边形";
                return false;
            }
            
            if (Region.GetArea() < 1.0) // 面积至少1mm²
            {
                errorMessage = $"筏板面积过小：{Region.GetArea():F2}mm²";
                return false;
            }
            
            if (Thickness.Value > Height)
            {
                errorMessage = $"筏板厚度({Thickness.Value}mm)不能大于总高度({Height:F0}mm)";
                return false;
            }
            
            errorMessage = null;
            return true;
        }
        
        /// <summary>
        /// 判断点是否在筏板平面范围内
        /// </summary>
        public bool ContainsPoint(Point2D point)
        {
            return Region.ContainsPoint(point);
        }
        
        public override string ToString()
        {
            return $"筏板 (厚度: {Thickness}, 面积: {Area:F2}mm², 标高: {BaseElevation.ToFormattedString()})";
        }
    }
}














