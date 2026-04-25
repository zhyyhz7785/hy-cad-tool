using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Shared.Geometry;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Shared.AutoCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Xdata;

namespace HyCADTool.Shared.AutoCAD.Services.Road
{
    /// <summary>
    /// 把 <see cref="Alignment"/> 按 <see cref="AlignmentStationBreakdown"/> 得到的直/缓/圆段
    /// 分段硬编码 ACI 颜色绘制到指定图层，每段一条 Polyline，并挂 HY_ROAD Xdata
    /// （KIND=<paramref>xdataKind</paramref>，ID=<see cref="Alignment.Id"/>）。
    ///
    /// <para>给 <see cref="RoadAlignmentDesignPreviewService"/>（画到 <c>05_hy_道路_原线</c>）
    /// 与 <see cref="RoadAlignmentLivePreviewService"/>（画到 <c>05_hy_道路_预览</c>）共用，
    /// 统一段分解与着色规则，避免两份实现漂移。</para>
    ///
    /// <para>颜色硬编码（直接写 <c>Entity.Color</c>，独立于图层 ByLayer）：</para>
    /// <list type="bullet">
    ///   <item>Line → ACI 2（黄）</item>
    ///   <item>Spiral 入侧（<see cref="SpiralSegmentRole.Entry"/>）→ ACI 4（青）</item>
    ///   <item>Spiral 出侧（<see cref="SpiralSegmentRole.Exit"/>）→ ACI 30（橙）</item>
    ///   <item>Arc → ACI 3（绿）</item>
    /// </list>
    ///
    /// <para>擦除时严格按 <c>(KIND, ID)</c> 过滤，保证同图层上不同 KIND 互不干扰
    /// （典型场景：<c>05_hy_道路_原线</c> 层上 <see cref="HyRoadXdata.KindAlignmentRawPick"/>
    /// 与 <see cref="HyRoadXdata.KindAlignmentDesignPreview"/> 并存）。</para>
    /// </summary>
    public static class AlignmentSegmentRenderer
    {
        /// <summary>
        /// 为 <paramref name="aln"/> 在 <paramref name="targetLayer"/> 上绘制分段彩色 Polyline。
        /// </summary>
        /// <param name="eraseExistingSameId">
        ///   true  = 幂等模式：先按 (KIND, <c>aln.Id</c>) 擦旧再画新。
        ///           用于 <see cref="HyRoadXdata.KindAlignmentDesignPreview"/> — 每次 PI 调整稳定都刷新。
        ///   false = 累加模式：不清同 KIND+ID 旧实体，直接追加一批新的。
        ///           用于 <see cref="HyRoadXdata.KindAlignmentLivePreview"/> — 允许用户保留多个历史快照并排比对。
        /// </param>
        /// <returns>新建的 Polyline 条数；无法分解 / 文档关闭时返回 0。</returns>
        public static int Draw(Document doc, Alignment aln, string targetLayer, string xdataKind, bool eraseExistingSameId = true)
        {
            if (doc == null) return 0;
            if (aln == null) return 0;
            if (aln.Centerline == null || aln.Centerline.VertexCount < 2) return 0;
            if (string.IsNullOrWhiteSpace(targetLayer)) throw new ArgumentException("targetLayer", nameof(targetLayer));
            if (string.IsNullOrWhiteSpace(xdataKind)) throw new ArgumentException("xdataKind", nameof(xdataKind));

            try
            {
                var db = doc.Database;
                int count = 0;

                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    if (eraseExistingSameId)
                    {
                        EraseByKindAndIdInternal(tr, db, xdataKind, aln.Id);
                    }

                    var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                    bool segmented = false;
                    if (aln.Source?.PiElements != null && aln.Source.PiElements.Count >= 2)
                    {
                        try
                        {
                            var piList = aln.Source.PiElements
                                .Select(e => new PiElement(e.P, e.Radius, e.SpiralIn, e.SpiralOut, e.Tag))
                                .ToList();
                            var breakdown = AlignmentStationBreakdown.Build(
                                piList, aln.StartStation, new PiDesignOptions());

                            foreach (var seg in breakdown.Segments)
                            {
                                var domain = BuildSegmentPolyline(aln, seg);
                                if (domain == null || domain.VertexCount < 2) continue;

                                var pl = RoadGeometryBridge.ToAutoCadPolyline(domain);
                                if (LayerExists(tr, db, targetLayer)) pl.Layer = targetLayer;
                                short aci = ColorIndexFor(seg);
                                pl.Color = Color.FromColorIndex(ColorMethod.ByAci, aci);
                                btr.AppendEntity(pl);
                                tr.AddNewlyCreatedDBObject(pl, true);
                                HyRoadXdata.Write(tr, db, pl, aln.Id, xdataKind, SchemaVersion.Current);
                                count++;
                            }

                            segmented = count > 0;
                        }
                        catch
                        {
                            segmented = false;
                        }
                    }

                    // 分解失败降级：整条中心线作一条灰白 Polyline，至少保证可见
                    if (!segmented)
                    {
                        var pl = RoadGeometryBridge.ToAutoCadPolyline(aln.Centerline);
                        if (LayerExists(tr, db, targetLayer)) pl.Layer = targetLayer;
                        pl.Color = Color.FromColorIndex(ColorMethod.ByAci, 7);
                        btr.AppendEntity(pl);
                        tr.AddNewlyCreatedDBObject(pl, true);
                        HyRoadXdata.Write(tr, db, pl, aln.Id, xdataKind, SchemaVersion.Current);
                        count = 1;
                    }

                    tr.Commit();
                }

                return count;
            }
            catch
            {
                // 文档被关闭 / LockDocument 失败等 — VM 线程不扩散异常
                return 0;
            }
        }

        /// <summary>
        /// 按 KIND 擦除当前文档 ModelSpace 中所有同类实体（不区分 alignment）。
        /// 供「切 Alignment / 关面板 / 应用定稿」等需要整类清零的场景。
        /// </summary>
        public static int EraseByKind(Document doc, string xdataKind)
        {
            if (doc == null || string.IsNullOrWhiteSpace(xdataKind)) return 0;
            try
            {
                var db = doc.Database;
                int n;
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    n = EraseByKindInternal(tr, db, xdataKind);
                    tr.Commit();
                }
                return n;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>按 (KIND, ID) 精确擦除本 alignment 的同类预览，避免误伤其他线位。</summary>
        public static int EraseByKindAndId(Document doc, string xdataKind, Guid alignmentId)
        {
            if (doc == null || string.IsNullOrWhiteSpace(xdataKind) || alignmentId == Guid.Empty) return 0;
            try
            {
                var db = doc.Database;
                int n;
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    n = EraseByKindAndIdInternal(tr, db, xdataKind, alignmentId);
                    tr.Commit();
                }
                return n;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>分段硬编码 ACI：直=黄 2 / 缓入=青 4 / 缓出=橙 30 / 圆=绿 3。</summary>
        public static short ColorIndexFor(SegmentRecord seg)
        {
            switch (seg.Kind)
            {
                case SegmentKind.Line: return 2;
                case SegmentKind.Arc: return 3;
                case SegmentKind.Spiral:
                    switch (seg.SpiralRole)
                    {
                        case SpiralSegmentRole.Exit: return 30;
                        case SpiralSegmentRole.Entry:
                        case SpiralSegmentRole.None:
                        default: return 4;
                    }
                default: return 7;
            }
        }

        // =========================================================================
        // 以下段→Polyline 构建逻辑与 RoadAlignmentUserPickPreviewService 保持同构：
        // 直/圆段 = 两点 + bulge（与 Sub-Entity 表窗口段高亮共用算法）；
        // 缓和段沿中心线采样，避免回旋线方程反解带来的数值风险。
        // =========================================================================

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

        private static int EraseByKindInternal(Transaction tr, Database db, string xdataKind)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            var toErase = new List<ObjectId>();
            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead);
                if (ent == null) continue;
                var k = HyRoadXdata.ReadKind(tr, ent);
                if (!string.Equals(k, xdataKind, StringComparison.Ordinal)) continue;
                toErase.Add(id);
            }
            foreach (var id in toErase)
            {
                var ent = tr.GetObject(id, OpenMode.ForWrite);
                if (ent != null && !ent.IsErased) ent.Erase();
            }
            return toErase.Count;
        }

        private static int EraseByKindAndIdInternal(Transaction tr, Database db, string xdataKind, Guid alignmentId)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            var toErase = new List<ObjectId>();
            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead);
                if (ent == null) continue;
                var k = HyRoadXdata.ReadKind(tr, ent);
                if (!string.Equals(k, xdataKind, StringComparison.Ordinal)) continue;
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

        private static bool LayerExists(Transaction tr, Database db, string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName)) return false;
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            return lt.Has(layerName);
        }
    }
}
