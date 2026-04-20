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
    /// 把 Domain <see cref="Alignment"/> 按 PI 分解的直/缓/圆段绘制到固定图层 <see cref="HyRoadLayers.UserPickPreviewLayer"/>，
    /// 每段单独 <see cref="Polyline"/>，并为每条实体挂 HY_ROAD Preview Xdata（KIND=<see cref="KindAlignmentPreview"/>、ID=AlignmentId）。
    ///
    /// <para>颜色策略：</para>
    /// <list type="bullet">
    ///   <item>分段色（<see cref="ColorMode.BySegmentKind"/>）：直=黄、缓=青、圆=绿。用于看清几何组成。</item>
    ///   <item>按 Alignment 分色（<see cref="ColorMode.ByAlignmentId"/>）：同一条 Alignment 所有段同色，
    ///     多条 UserPicked 线位同屏时按 Id 哈希轮流取色，容易分辨"哪段属于哪条"。</item>
    /// </list>
    /// 不写 HY_ROAD Alignment Xdata，不影响正式中心线实体；Commit 阶段通过 <see cref="ErasePreviewsForAlignment"/>
    /// 按 (KIND=AlignmentPreview, ID=AlignmentId) 擦除之前的预览，实现"提交 → 清草稿"。
    /// </summary>
    public static class RoadAlignmentUserPickPreviewService
    {
        /// <summary>HY_ROAD Xdata 里预览实体的 KIND 值。专用 Commit / Clear 时识别。</summary>
        public const string KindAlignmentPreview = "AlignmentPreview";

        public enum ColorMode
        {
            /// <summary>按段类型着色：直=黄、缓=青、圆=绿。单条线位查看最直观。</summary>
            BySegmentKind = 0,

            /// <summary>按 <see cref="Alignment.Id"/> 哈希着色：多条 UserPicked 同屏时区分。</summary>
            ByAlignmentId = 1,
        }

        // 深色背景下 12 种差异化 ACI，避开 7（白/黑，与 UI 主文字撞色）和 2/3/4（分段色，避免混淆）
        // 选色原则：色相分散、明度中等、视觉互斥。
        private static readonly short[] AlignmentPalette =
        {
            1,   // 红
            5,   // 蓝
            6,   // 品红
            30,  // 橙
            40,  // 黄绿
            50,  // 柠檬黄
            140, // 蓝绿
            170, // 紫蓝
            210, // 紫红
            92,  // 草绿
            122, // 青蓝
            184, // 粉紫
        };

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

        /// <summary>按 GUID 哈希稳定取色。同一 Alignment 每次着同色。</summary>
        public static short ColorIndexFor(Guid alignmentId)
        {
            var bytes = alignmentId.ToByteArray();
            int h = 0;
            for (int i = 0; i < bytes.Length; i++) h = unchecked(h * 31 + bytes[i]);
            int idx = (h & 0x7FFFFFFF) % AlignmentPalette.Length;
            return AlignmentPalette[idx];
        }

        /// <returns>新建的 Polyline 条数。</returns>
        public static int DrawPreviewPolylines(Document doc, Alignment alignment, ColorMode colorMode = ColorMode.BySegmentKind)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (alignment == null) throw new ArgumentNullException(nameof(alignment));
            if (alignment.Centerline == null || alignment.Centerline.VertexCount < 2) return 0;

            var db = doc.Database;
            int count = 0;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                // 幂等：重新预览前先擦同 AlignmentId 的旧预览实体，避免堆叠。
                ErasePreviewsForAlignmentInternal(tr, db, alignment.Id);

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
                            short aci = colorMode == ColorMode.ByAlignmentId
                                ? ColorIndexFor(alignment.Id)
                                : ColorIndexFor(seg.Kind);
                            pl.Color = Color.FromColorIndex(ColorMethod.ByAci, aci);
                            btr.AppendEntity(pl);
                            tr.AddNewlyCreatedDBObject(pl, true);
                            HyRoadXdata.Write(tr, db, pl, alignment.Id, KindAlignmentPreview, SchemaVersion.Current);
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
                    short aci = colorMode == ColorMode.ByAlignmentId
                        ? ColorIndexFor(alignment.Id)
                        : (short)7;
                    pl.Color = Color.FromColorIndex(ColorMethod.ByAci, aci);
                    btr.AppendEntity(pl);
                    tr.AddNewlyCreatedDBObject(pl, true);
                    HyRoadXdata.Write(tr, db, pl, alignment.Id, KindAlignmentPreview, SchemaVersion.Current);
                    count = 1;
                }

                tr.Commit();
            }

            return count;
        }

        /// <summary>
        /// 擦除 ModelSpace 中 KIND=AlignmentPreview、ID=<paramref name="alignmentId"/> 的所有预览实体。
        /// 调用方需自行开 <paramref name="tr"/> 并在结束后 Commit。
        /// </summary>
        /// <returns>被擦除的实体数量。</returns>
        public static int ErasePreviewsForAlignment(Transaction tr, Database db, Guid alignmentId)
        {
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (alignmentId == Guid.Empty) return 0;
            return ErasePreviewsForAlignmentInternal(tr, db, alignmentId);
        }

        private static int ErasePreviewsForAlignmentInternal(Transaction tr, Database db, Guid alignmentId)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            var toErase = new List<ObjectId>();
            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead);
                if (ent == null) continue;
                var k = HyRoadXdata.ReadKind(tr, ent);
                if (!string.Equals(k, KindAlignmentPreview, StringComparison.Ordinal)) continue;
                var gid = HyRoadXdata.ReadId(tr, ent);
                if (gid != alignmentId) continue;
                toErase.Add(id);
            }
            foreach (var id in toErase)
            {
                var ent = tr.GetObject(id, OpenMode.ForWrite);
                if (ent != null && !ent.IsErased) ent.Erase();
            }
            return toErase.Count;
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
