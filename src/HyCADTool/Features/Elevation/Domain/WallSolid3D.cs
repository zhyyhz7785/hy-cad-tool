using System;
using HyCADTool.Domain.ValueObjects;

namespace HyCADTool.Shared.Geometry
{
    /// <summary>
    /// 墙体实体（值对象）
    /// 描述一个外侧扩张的墙体三维实体
    /// </summary>
    public class WallSolid3D
    {
        public Polygon2D OuterPolygon { get; }
        public Polygon2D InnerPolygon { get; }
        public Elevation BottomElevation { get; }
        public Elevation TopElevation { get; }
        public bool ContactsWithSoil { get; }
        
        private WallSolid3D(
            Polygon2D outerPolygon,
            Polygon2D innerPolygon,
            Elevation bottomElevation,
            Elevation topElevation,
            bool contactsWithSoil)
        {
            OuterPolygon = outerPolygon ?? throw new ArgumentNullException(nameof(outerPolygon));
            InnerPolygon = innerPolygon ?? throw new ArgumentNullException(nameof(innerPolygon));
            BottomElevation = bottomElevation ?? throw new ArgumentNullException(nameof(bottomElevation));
            TopElevation = topElevation ?? throw new ArgumentNullException(nameof(topElevation));
            ContactsWithSoil = contactsWithSoil;
            
            if (bottomElevation.Value >= topElevation.Value)
                throw new ArgumentException("墙体底标高必须小于顶标高");
        }
        
        public static WallSolid3D Create(
            Polygon2D outerPolygon,
            Polygon2D innerPolygon,
            Elevation bottomElevation,
            Elevation topElevation,
            bool contactsWithSoil)
        {
            return new WallSolid3D(
                outerPolygon,
                innerPolygon,
                bottomElevation,
                topElevation,
                contactsWithSoil);
        }
        
        public double Height => TopElevation.Value - BottomElevation.Value;
        
        public override string ToString()
        {
            return $"墙体 [{BottomElevation.ToFormattedString()} → {TopElevation.ToFormattedString()}], " +
                   $"高度: {Height:F0}mm, 土壤接触: {ContactsWithSoil}";
        }
        
        // 值对象相等性比较
        public bool Equals(WallSolid3D other)
        {
            if (other == null) return false;
            return OuterPolygon.Equals(other.OuterPolygon) &&
                   InnerPolygon.Equals(other.InnerPolygon) &&
                   BottomElevation.Equals(other.BottomElevation) &&
                   TopElevation.Equals(other.TopElevation) &&
                   ContactsWithSoil == other.ContactsWithSoil;
        }
        
        public override bool Equals(object obj)
        {
            return obj is WallSolid3D other && Equals(other);
        }
        
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + (OuterPolygon?.GetHashCode() ?? 0);
                hash = hash * 23 + (InnerPolygon?.GetHashCode() ?? 0);
                hash = hash * 23 + (BottomElevation?.GetHashCode() ?? 0);
                hash = hash * 23 + (TopElevation?.GetHashCode() ?? 0);
                hash = hash * 23 + ContactsWithSoil.GetHashCode();
                return hash;
            }
        }
    }
}

