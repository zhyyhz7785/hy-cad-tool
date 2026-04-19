using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road
{
    /// <summary>
    /// 交叉口无障碍设施（<see cref="CurbRamp"/> + <see cref="TactilePaving"/>）AutoCAD 绘制服务 —— Infrastructure 层。
    ///
    /// <para><b>职责</b></para>
    /// <list type="bullet">
    /// <item><see cref="DrawCurbRamps"/>：把 <see cref="Intersection.CurbRamps"/> 逐个画为闭合 <see cref="Polyline"/>（矩形，4 顶点），
    /// 挂 HY_ROAD Xdata（KIND="<see cref="CurbRampKind"/>"，ID=Intersection.Id），放到 <see cref="HyRoadLayers.CurbRampLayer"/>；</item>
    /// <item><see cref="DrawTactilePavings"/>：把 <see cref="Intersection.TactilePavings"/> 逐条画为带 <see cref="Polyline.ConstantWidth"/> 的 LWPolyline，
    /// 挂 HY_ROAD Xdata（KIND="<see cref="TactilePavingKind"/>"），放到 <see cref="HyRoadLayers.TactilePavingLayer"/>；</item>
    /// <item><see cref="ClearAccessibilityEntities"/>：按 HY_ROAD Xdata (ID=Intersection.Id, KIND ∈ {CurbRamp, TactilePaving})
    /// 扫除旧图元（用于幂等重建）；</item>
    /// <item><see cref="RebuildAccessibility"/>：先 Clear 再 Draw，保证重入一致。</item>
    /// </list>
    ///
    /// <para><b>线程 / 事务约束</b></para>
    /// 所有方法均在调用方提供的 <see cref="Transaction"/> 中执行；本类不 StartTransaction，也不 Commit。
    ///
    /// <para><b>Xdata 共用 Intersection.Id</b></para>
    /// CurbRamp / TactilePaving 本身没有独立 Id（值对象），但隶属的 <see cref="Intersection"/> 有稳定 Id，
    /// 因此本服务用 <paramref name="intersection"/>.Id 作为 Xdata 的 ID；KIND 字段区分实体类型。
    /// "删除整个交叉口" 时扫描 3 种 KIND（<see cref="RoadIntersectionService.IntersectionKind"/> +
    /// <see cref="CurbRampKind"/> + <see cref="TactilePavingKind"/>）即可清掉包括转角弧在内的所有图元。
    /// </summary>
    public sealed class RoadAccessibilityService
    {
        /// <summary>HY_ROAD Xdata KIND：缘石坡道。</summary>
        public const string CurbRampKind = "CurbRamp";

        /// <summary>HY_ROAD Xdata KIND：盲道。</summary>
        public const string TactilePavingKind = "TactilePaving";

        // =========================================================================
        //  CurbRamp
        // =========================================================================

        /// <summary>
        /// 把 <paramref name="intersection"/> 的所有 <see cref="CurbRamp"/> 画成闭合 LWPolyline。
        ///
        /// <para>v1.1 分类几何（<see cref="CurbRampDesigner.BuildFootprint"/>）：
        /// <list type="bullet">
        /// <item><see cref="CurbRampKind.SingleFace"/>：1 条矩形 Polyline；</item>
        /// <item><see cref="CurbRampKind.ThreeFace"/>：3 条 Polyline（主坡矩形 + 左/右侧三角坡），
        /// 每条都挂 <see cref="CurbRampKind"/> Xdata + Intersection.Id → <see cref="ClearAccessibilityEntities"/> 一次清理；</item>
        /// <item><see cref="CurbRampKind.Fan"/>：1 条扇环 Polyline（弧用 <see cref="CurbRampDesigner.DefaultFanTesselationSegments"/> 段直线镶嵌）；
        /// 需要 <see cref="Intersection.CornerArcs"/> 中匹配 <see cref="CurbRamp.CornerArcIndex"/> 的弧。</item>
        /// </list></para>
        /// </summary>
        public IReadOnlyList<ObjectId> DrawCurbRamps(
            Transaction tr, Database db, Intersection intersection, string layerName)
        {
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (intersection == null) throw new ArgumentNullException(nameof(intersection));

            var results = new List<ObjectId>(intersection.CurbRamps.Count);
            if (intersection.CurbRamps.Count == 0) return results;

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            bool useLayer = !string.IsNullOrWhiteSpace(layerName) && LayerExists(tr, db, layerName);

            foreach (var ramp in intersection.CurbRamps)
            {
                CornerArc? arc = null;
                if (ramp.CornerArcIndex >= 0 && ramp.CornerArcIndex < intersection.CornerArcs.Count)
                {
                    arc = intersection.CornerArcs[ramp.CornerArcIndex];
                }

                var footprints = CurbRampDesigner.BuildFootprint(ramp, arc);
                foreach (var pl2d in footprints)
                {
                    var pl = ToAutoCadPolyline(pl2d);
                    if (useLayer) pl.Layer = layerName;

                    ms.AppendEntity(pl);
                    tr.AddNewlyCreatedDBObject(pl, true);

                    HyRoadXdata.Write(tr, db, pl, intersection.Id, CurbRampKind, SchemaVersion.Current);
                    results.Add(pl.ObjectId);
                }
            }

            return results;
        }

        // =========================================================================
        //  TactilePaving
        // =========================================================================

        /// <summary>
        /// 把 <paramref name="intersection"/> 的所有 <see cref="TactilePaving"/> 画成带宽 LWPolyline。
        /// </summary>
        public IReadOnlyList<ObjectId> DrawTactilePavings(
            Transaction tr, Database db, Intersection intersection, string layerName)
        {
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (intersection == null) throw new ArgumentNullException(nameof(intersection));

            var results = new List<ObjectId>(intersection.TactilePavings.Count);
            if (intersection.TactilePavings.Count == 0) return results;

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

            bool useLayer = !string.IsNullOrWhiteSpace(layerName) && LayerExists(tr, db, layerName);

            foreach (var paving in intersection.TactilePavings)
            {
                if (paving == null || !paving.IsValid) continue;

                var pl = ToAutoCadPolyline(paving);
                if (useLayer) pl.Layer = layerName;

                ms.AppendEntity(pl);
                tr.AddNewlyCreatedDBObject(pl, true);

                HyRoadXdata.Write(tr, db, pl, intersection.Id, TactilePavingKind, SchemaVersion.Current);
                results.Add(pl.ObjectId);
            }

            return results;
        }

        // =========================================================================
        //  Clear / Rebuild
        // =========================================================================

        /// <summary>
        /// 按 HY_ROAD Xdata 扫模型空间，擦除所有 KIND ∈ {<see cref="CurbRampKind"/>, <see cref="TactilePavingKind"/>}
        /// 且 ID=<paramref name="intersectionId"/> 的图元。
        /// </summary>
        /// <returns>被删除的图元数量。</returns>
        public int ClearAccessibilityEntities(Transaction tr, Database db, Guid intersectionId)
        {
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (db == null) throw new ArgumentNullException(nameof(db));

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            int removed = 0;
            foreach (var entId in ms)
            {
                var ent = tr.GetObject(entId, OpenMode.ForRead) as Entity;
                if (ent == null) continue;

                var kind = HyRoadXdata.ReadKind(tr, ent);
                if (!string.Equals(kind, CurbRampKind, StringComparison.Ordinal)
                    && !string.Equals(kind, TactilePavingKind, StringComparison.Ordinal))
                    continue;

                var id = HyRoadXdata.ReadId(tr, ent);
                if (id == Guid.Empty || id != intersectionId) continue;

                ent.UpgradeOpen();
                ent.Erase();
                removed++;
            }
            return removed;
        }

        /// <summary>
        /// "擦旧 + 画新" 幂等重建：先清空所有 CurbRamp / TactilePaving，再重新画。
        /// </summary>
        public (IReadOnlyList<ObjectId> RampIds, IReadOnlyList<ObjectId> PavingIds) RebuildAccessibility(
            Transaction tr,
            Database db,
            Intersection intersection,
            string curbRampLayer,
            string tactilePavingLayer)
        {
            if (intersection == null) throw new ArgumentNullException(nameof(intersection));
            ClearAccessibilityEntities(tr, db, intersection.Id);
            var rampIds = DrawCurbRamps(tr, db, intersection, curbRampLayer);
            var pavingIds = DrawTactilePavings(tr, db, intersection, tactilePavingLayer);
            return (rampIds, pavingIds);
        }

        // =========================================================================
        //  Domain → AutoCAD 几何转换
        // =========================================================================

        /// <summary>
        /// 把 Domain 层 <see cref="Polyline2D"/> 转成 AutoCAD <see cref="Polyline"/>（等线宽 0，闭合性透传）。
        /// 用于 <see cref="CurbRamp"/> 分类几何（Single / ThreeFace / Fan）的各闭合轮廓。
        /// </summary>
        internal static Polyline ToAutoCadPolyline(Polyline2D pl2d)
        {
            if (pl2d == null) throw new ArgumentNullException(nameof(pl2d));
            var pl = new Polyline(pl2d.VertexCount)
            {
                Closed = pl2d.IsClosed,
            };
            for (int i = 0; i < pl2d.VertexCount; i++)
            {
                var v = pl2d.GetPointAt(i);
                pl.AddVertexAt(i, new Point2d(v.X, v.Y), 0, 0, 0);
            }
            return pl;
        }

        /// <summary>
        /// 把 <see cref="TactilePaving"/> 转成带宽 <see cref="Polyline"/>（<see cref="Polyline.ConstantWidth"/>=<see cref="TactilePaving.Width"/>）。
        /// </summary>
        internal static Polyline ToAutoCadPolyline(TactilePaving paving)
        {
            var pl = new Polyline(paving.Centerline.Count);
            for (int i = 0; i < paving.Centerline.Count; i++)
            {
                var v = paving.Centerline[i];
                pl.AddVertexAt(i, new Point2d(v.X, v.Y), 0, paving.Width, paving.Width);
            }
            pl.ConstantWidth = paving.Width;
            return pl;
        }

        private static bool LayerExists(Transaction tr, Database db, string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName)) return false;
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            return lt.Has(layerName);
        }
    }
}
