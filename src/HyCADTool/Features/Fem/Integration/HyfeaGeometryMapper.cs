using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCAD.Geometry;
using HYFEA.Core.Model;
using HYFEA.Hosting;

namespace HyCADTool.Features.Fem.Integration
{
    /// <summary>
    /// AutoCAD 选线 → 梁轴线。薄 Integration：唯此处读 Curve。
    /// 采样后委托 <see cref="HyfeaAxisGeometry"/>，实现 <see cref="IFemHostGeometry"/>。
    /// </summary>
    public sealed class HyfeaGeometryMapper : IFemHostGeometry
    {
        private readonly HyfeaAxisGeometry _axis;

        public HyfeaGeometryMapper(IReadOnlyList<Point2D> nodes)
        {
            _axis = new HyfeaAxisGeometry(nodes);
        }

        public HyfeaGeometryMapper(Curve curve, int segmentCount, UnitSystem units)
            : this(MapBeam(curve, segmentCount, units))
        {
        }

        public IReadOnlyList<Point2D> GetAxisNodes() => _axis.GetAxisNodes();

        /// <param name="segmentCount">沿整条轴线划分的段数（≥1），生成 segmentCount+1 个节点。</param>
        public static IReadOnlyList<Point2D> MapBeam(Curve curve, int segmentCount, UnitSystem units)
        {
            if (curve == null)
                throw new ArgumentNullException(nameof(curve));

            IReadOnlyList<Point2D> verticesMm;
            bool closed = false;
            switch (curve)
            {
                case Line ln:
                {
                    var a = ln.StartPoint;
                    var b = ln.EndPoint;
                    if (a.DistanceTo(b) < 1e-9)
                        throw new InvalidOperationException("线长为 0。");
                    verticesMm = new[]
                    {
                        new Point2D(a.X, a.Y),
                        new Point2D(b.X, b.Y),
                    };
                    break;
                }
                case Polyline pl:
                {
                    int nc = pl.NumberOfVertices;
                    if (nc < 2)
                        throw new InvalidOperationException("多段线顶点不足。");
                    var verts = new Point2D[nc];
                    for (int i = 0; i < nc; i++)
                    {
                        var p = pl.GetPoint2dAt(i);
                        verts[i] = new Point2D(p.X, p.Y);
                    }

                    verticesMm = verts;
                    closed = pl.Closed;
                    break;
                }
                default:
                    throw new InvalidOperationException("仅支持 Line / Polyline。");
            }

            return HyfeaAxisGeometry.SampleByLength(verticesMm, segmentCount, units, closed);
        }
    }
}
