using System;
using System.Collections.Generic;
using HyCAD.Geometry;

namespace HyCADTool.Features.BaseRein.Domain.Services
{
    /// <summary>
    /// 一次多边形并集：若干实心区域，每个区域有一条外环和零条或多条孔洞环。
    /// </summary>
    public sealed class PolygonUnionResult
    {
        public PolygonUnionResult(IReadOnlyList<PolygonUnionRegion> regions)
        {
            if (regions == null)
                throw new ArgumentNullException(nameof(regions));
            if (regions.Count == 0)
                throw new ArgumentException("并集至少要有一个区域", nameof(regions));

            Regions = regions;
        }

        /// <summary>
        /// 并集里的实心区域。不相交的多边形各占一个区域。
        /// </summary>
        public IReadOnlyList<PolygonUnionRegion> Regions { get; }

        public static PolygonUnionResult FromOuter(Polygon2D outer)
        {
            return new PolygonUnionResult(new[]
            {
                new PolygonUnionRegion(outer, Array.Empty<Polygon2D>())
            });
        }
    }

    /// <summary>
    /// 一个实心区域：外环减去孔洞。
    /// </summary>
    public sealed class PolygonUnionRegion
    {
        public PolygonUnionRegion(Polygon2D outer, IReadOnlyList<Polygon2D> holes)
        {
            if (outer == null)
                throw new ArgumentNullException(nameof(outer));

            Outer = outer;
            Holes = holes ?? Array.Empty<Polygon2D>();
        }

        /// <summary>
        /// 外环，逆时针。
        /// </summary>
        public Polygon2D Outer { get; }

        /// <summary>
        /// 孔洞环，顺时针。孔洞里的岛不在这里，岛是另一个 <see cref="PolygonUnionRegion"/>。
        /// </summary>
        public IReadOnlyList<Polygon2D> Holes { get; }

        /// <summary>
        /// 外环面积减去孔洞面积。
        /// </summary>
        public double NetArea
        {
            get
            {
                double area = Outer.GetArea();
                for (int i = 0; i < Holes.Count; i++)
                    area -= Holes[i].GetArea();
                return area;
            }
        }
    }
}
