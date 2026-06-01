using System;
using HyCADTool.Features.Elevation.Domain.ValueObjects;
using HyCAD.Geometry;

namespace HyCADTool.Features.Elevation.Domain
{
    /// <summary>
    /// 底板实体（值对象）
    /// 描述一个底板三维实体
    /// </summary>
    public class SlabSolid3D
    {
        public Polygon2D Polygon { get; }
        public ElevationValue TopElevation { get; }
        public SlabThickness Thickness { get; }
        public ElevationValue BottomElevation { get; }
        
        private SlabSolid3D(
            Polygon2D polygon,
            ElevationValue topElevation,
            SlabThickness thickness)
        {
            Polygon = polygon ?? throw new ArgumentNullException(nameof(polygon));
            TopElevation = topElevation ?? throw new ArgumentNullException(nameof(topElevation));
            Thickness = thickness ?? throw new ArgumentNullException(nameof(thickness));
            
            BottomElevation = ElevationValue.FromMillimeters(topElevation.Value - thickness.Value);
        }
        
        public static SlabSolid3D Create(
            Polygon2D polygon,
            ElevationValue topElevation,
            SlabThickness thickness)
        {
            return new SlabSolid3D(polygon, topElevation, thickness);
        }
        
        public override string ToString()
        {
            return $"底板 [{BottomElevation.ToFormattedString()} → {TopElevation.ToFormattedString()}], " +
                   $"厚度: {Thickness.Value:F0}mm";
        }
        
        // 值对象相等性比较
        public bool Equals(SlabSolid3D other)
        {
            if (other == null) return false;
            return Polygon.Equals(other.Polygon) &&
                   TopElevation.Equals(other.TopElevation) &&
                   Thickness.Equals(other.Thickness);
        }
        
        public override bool Equals(object obj)
        {
            return obj is SlabSolid3D other && Equals(other);
        }
        
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (Polygon?.GetHashCode() ?? 0);
                hash = hash * 23 + (TopElevation?.GetHashCode() ?? 0);
                hash = hash * 23 + (Thickness?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }
}

