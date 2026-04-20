using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Geometry;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road
{
    /// <summary>
    /// 将当前 Domain <see cref="Alignment"/> 按 PI 分解的直/缓/圆段绘制到固定图层 <see cref="HyRoadLayers.UserPickPreviewLayer"/>，
    /// 每段单独 <see cref="Polyline"/>、ACI 颜色区分（与路线工作台预览一致）。
    /// 不写 HY_ROAD Xdata，不影响正式中心线实体。
    /// </summary>
    public static class RoadAlignmentUserPickPreviewService
    {
        /// <summary>直线：黄；缓和：青；圆曲线：绿。</summary>
        public static short ColorIndexFor(SegmentKind kind)
        {
            switch (kind)
            {
                case SegmentKind.Line: return 2;
                case SegmentKind.Spiral: return 4;
                case SegmentKind.Arc: return 3;
                default: return 7;
            }
        }

        /// <returns>新建的 Polyline 条数。</returns>
        public static int DrawPreviewPolylines(Document doc, Alignment alignment)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (alignment == null) throw new ArgumentNullException(nameof(alignment));
            if (alignment.Centerline == null || alignment.Centerline.VertexCount < 2) return 0;

            var db = doc.Database;
            int count = 0;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                bool segmented = false;
                if (alignment.Source?.PiElements != null && alignment.Source.PiElements.Count >= 2)
                {
                    try
                    {
                        var piList = alignment.Source.PiElements
                            .Select(e => new PiElement(e.P, e.Radius, e.SpiralIn, e.SpiralOut, e.Tag))
                            .ToList();
                        var breakdown = AlignmentStationBreakdown.Build(
                            piList,
                            alignment.StartStation,
                            new PiDesignOptions());

                        foreach (var seg in breakdown.Segments)
                        {
                            var domain = BuildSegmentPolyline(alignment, seg);
                            if (domain == null || domain.VertexCount < 2) continue;

                            var pl = RoadGeometryBridge.ToAutoCadPolyline(domain);
                            pl.Layer = HyRoadLayers.UserPickPreviewLayer;
                            pl.Color = Color.FromColorIndex(ColorMethod.ByAci, ColorIndexFor(seg.Kind));
                            btr.AppendEntity(pl);
                            tr.AddNewlyCreatedDBObject(pl, true);
                            count++;
                        }

                        segmented = count > 0;
                    }
                    catch (Exception)
                    {
                        segmented = false;
                    }
                }

                if (!segmented)
                {
                    var pl = RoadGeometryBridge.ToAutoCadPolyline(alignment.Centerline);
                    pl.Layer = HyRoadLayers.UserPickPreviewLayer;
                    pl.Color = Color.FromColorIndex(ColorMethod.ByAci, 7);
                    btr.AppendEntity(pl);
                    tr.AddNewlyCreatedDBObject(pl, true);
                    count = 1;
                }

                tr.Commit();
            }

            return count;
        }

        private static Polyline3D BuildSegmentPolyline(Alignment alignment, SegmentRecord seg)
        {
            switch (seg.Kind)
            {
                case SegmentKind.Spiral:
                    return BuildSpiralAlongCenterline(alignment, seg);
                default:
                    return BuildLineOrArcChord(seg);
            }
        }

        /// <summary>与 Sub-Entity 表窗口段高亮一致：直线两点；圆曲线两点 + bulge。</summary>
        private static Polyline3D BuildLineOrArcChord(SegmentRecord seg)
        {
            var verts = new[]
            {
                new Point3D(seg.StartPoint.X, seg.StartPoint.Y, 0),
                new Point3D(seg.EndPoint.X, seg.EndPoint.Y, 0),
            };

            double bulge = 0;
            if (seg.Kind == SegmentKind.Arc)
            {
                double dPsi = NormalizeAnglePi(seg.EndBearingRad - seg.StartBearingRad);
                bulge = Math.Tan(dPsi / 4.0);
            }

            return new Polyline3D(verts, isClosed: false, bulges: new[] { bulge, 0.0 });
        }

        private static Polyline3D BuildSpiralAlongCenterline(Alignment alignment, SegmentRecord seg)
        {
            var pl = alignment.Centerline;
            if (pl == null || pl.VertexCount < 2) return null;

            double baseChain = alignment.StartStation;
            double d0 = seg.StationStartM - baseChain;
            double d1 = seg.StationEndM - baseChain;
            double total = pl.GetPlanarLength();
            if (d0 < 0) d0 = 0;
            if (d1 > total) d1 = total;
            if (d1 <= d0 + 1e-9) return null;

            double len = d1 - d0;
            int steps = Math.Max(2, (int)Math.Ceiling(len / 0.5));
            var pts = new List<Point3D>(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                double u = i / (double)steps;
                double s = d0 + len * u;
                pts.Add(pl.PointAtPlanarStation(s));
            }

            return new Polyline3D(pts, isClosed: false);
        }

        private static double NormalizeAnglePi(double rad)
        {
            while (rad > Math.PI) rad -= 2 * Math.PI;
            while (rad <= -Math.PI) rad += 2 * Math.PI;
            return rad;
        }
    }
}
