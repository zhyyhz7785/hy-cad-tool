using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCAD.Geometry;
using HYFEA.Core.Model;

namespace HyCADTool.Features.Fem.Integration
{
    /// <summary>将选中的 Line / Polyline 采样为梁轴线节点（模型空间坐标按 mm 读取）。</summary>
    public static class HyfeaGeometryMapper
    {
        /// <param name="segmentCount">沿整条轴线划分的段数（≥1），生成 segmentCount+1 个节点。</param>
        public static IReadOnlyList<Point2D> MapBeam(Curve curve, int segmentCount, UnitSystem units)
        {
            if (segmentCount < 1) segmentCount = 1;

            double s = units == UnitSystem.SI ? 1e-3 : 1.0;

            var vertices2 = new List<Point2d>();
            switch (curve)
            {
                case Line ln:
                {
                    var a = ln.StartPoint;
                    var b = ln.EndPoint;
                    if (a.DistanceTo(b) < 1e-9)
                        throw new InvalidOperationException("线长为 0。");
                    for (int i = 0; i <= segmentCount; i++)
                    {
                        double t = (double)i / segmentCount;
                        var p = a + (b - a) * t;
                        vertices2.Add(new Point2d(p.X, p.Y));
                    }

                    break;
                }
                case Polyline pl:
                {
                    int nc = pl.NumberOfVertices;
                    if (nc < 2)
                        throw new InvalidOperationException("多段线顶点不足。");
                    var lens = new double[Math.Max(0, pl.Closed ? nc : nc - 1)];
                    double acc = 0;
                    int segCount = pl.Closed ? nc : nc - 1;
                    for (int i = 0; i < segCount; i++)
                    {
                        var p0 = pl.GetPoint2dAt(i);
                        var p1 = pl.GetPoint2dAt((i + 1) % nc);
                        double dl = p0.GetDistanceTo(p1);
                        lens[i] = dl;
                        acc += dl;
                    }

                    double totalLen = acc;
                    if (totalLen < 1e-9)
                        throw new InvalidOperationException("多段线长度为 0。");

                    vertices2.Capacity = segmentCount + 1;
                    for (int k = 0; k <= segmentCount; k++)
                    {
                        double target = totalLen * k / segmentCount;
                        double walk = 0;
                        for (int i = 0; i < segCount; i++)
                        {
                            if (target <= walk + lens[i] + 1e-12 || i == segCount - 1)
                            {
                                var p0 = pl.GetPoint2dAt(i);
                                var p1 = pl.GetPoint2dAt((i + 1) % nc);
                                double t = lens[i] < 1e-12 ? 0 : (target - walk) / lens[i];
                                if (t < 0) t = 0;
                                if (t > 1) t = 1;
                                double x = p0.X + t * (p1.X - p0.X);
                                double y = p0.Y + t * (p1.Y - p0.Y);
                                vertices2.Add(new Point2d(x, y));
                                break;
                            }

                            walk += lens[i];
                        }
                    }

                    break;
                }
                default:
                    throw new InvalidOperationException("仅支持 Line / Polyline。");
            }

            var list = new List<Point2D>(vertices2.Count);
            foreach (var p in vertices2)
                list.Add(new Point2D(p.X * s, p.Y * s));
            return list;
        }
    }
}
