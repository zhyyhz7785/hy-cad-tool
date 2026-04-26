using System;
using HyCADTool.Domain.ValueObjects;

namespace HyCADTool.Shared.Geometry
{
    /// <summary>
    /// 三维表面（值对象）
    /// 描述一个具有标高的平面多边形
    /// </summary>
    public class Surface3D
    {
        public Polygon2D Polygon { get; }
        public Elevation Elevation { get; }
        public string Name { get; }
        
        private Surface3D(Polygon2D polygon, Elevation elevation, string name)
        {
            Polygon = polygon ?? throw new ArgumentNullException(nameof(polygon));
            Elevation = elevation ?? throw new ArgumentNullException(nameof(elevation));
            Name = name ?? "未命名表面";
        }
        
        public static Surface3D Create(Polygon2D polygon, Elevation elevation, string name)
        {
            return new Surface3D(polygon, elevation, name);
        }
        
        public override string ToString()
        {
            return $"{Name} @ {Elevation.ToFormattedString()}";
        }
        
        // 值对象应该支持相等性比较
        public bool Equals(Surface3D other)
        {
            if (other == null) return false;
            return Polygon.Equals(other.Polygon) && 
                   Elevation.Equals(other.Elevation) && 
                   Name == other.Name;
        }
        
        public override bool Equals(object obj)
        {
            return obj is Surface3D other && Equals(other);
        }
        
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (Polygon?.GetHashCode() ?? 0);
                hash = hash * 23 + (Elevation?.GetHashCode() ?? 0);
                hash = hash * 23 + (Name?.GetHashCode() ?? 0);
                return hash;
            }
        }
    }
}

