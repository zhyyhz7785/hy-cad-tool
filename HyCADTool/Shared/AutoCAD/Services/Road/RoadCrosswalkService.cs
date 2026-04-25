using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Shared.AutoCAD.Xdata;

namespace HyCADTool.Shared.AutoCAD.Services.Road
{
    /// <summary>
    /// v1.1 人行横道 AutoCAD 绘制服务 —— Infrastructure 层；把 <see cref="Intersection.Crosswalks"/>
    /// 展开成条纹 Line + 停止线 Line。替代旧 <c>HyCADTool.Shared.AutoCAD.Services.CrosswalkService</c>
    /// 的"绘制"部分（<c>DrawCrosswalkForArm</c> + <c>DrawAuxiliaryLines</c>）。
    ///
    /// <para><b>与旧 CrosswalkService 的对照</b></para>
    /// <list type="bullet">
    /// <item>旧：用户选若干 Line + Arc → 反推 RoadArm → 条纹绘制（含 <c>RayHitArc</c> 双端弧线裁切）；</item>
    /// <item>新：<see cref="Intersection"/> 已持有 Legs + CornerArcs → <see cref="CrosswalkDesigner"/> 纯函数算几何，
    /// 绘制通过 <see cref="CrosswalkDesigner.ComputeStripesClipped"/> 自动按 <see cref="CornerArc"/> 做两端弧裁切
    /// （v1.2 迁移完成，算法等价旧 <c>RayHitArc</c>）。</item>
    /// </list>
    ///
    /// <para><b>Xdata 规约</b></para>
    /// <list type="bullet">
    /// <item>条纹 Line 挂 KIND="<see cref="CrosswalkKind"/>"，ID=<see cref="Intersection.Id"/>；</item>
    /// <item>停止线 Line 挂 KIND="<see cref="StopLineKind"/>"，同样 ID=Intersection.Id；</item>
    /// <item>两者 ID 一致 → <see cref="ClearCrosswalkEntities"/> 一次扫掉。</item>
    /// </list>
    /// </summary>
    public sealed class RoadCrosswalkService
    {
        /// <summary>HY_ROAD Xdata KIND：人行横道条纹。</summary>
        public const string CrosswalkKind = "Crosswalk";

        /// <summary>HY_ROAD Xdata KIND：停止线。</summary>
        public const string StopLineKind = "StopLine";

        /// <summary>
        /// 把 <paramref name="intersection"/> 所有 <see cref="Crosswalk"/> 展开为条纹 Line 并绘制。
        /// </summary>
        public IReadOnlyList<ObjectId> DrawCrosswalkStripes(
            Transaction tr, Database db, Intersection intersection, string layerName)
        {
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (intersection == null) throw new ArgumentNullException(nameof(intersection));

            var results = new List<ObjectId>();
            if (intersection.Crosswalks.Count == 0) return results;

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            bool useLayer = !string.IsNullOrWhiteSpace(layerName) && LayerExists(tr, db, layerName);

            foreach (var cw in intersection.Crosswalks)
            {
                var stripes = CrosswalkDesigner.ComputeStripesClipped(cw, intersection.CornerArcs);
                foreach (var s in stripes)
                {
                    var line = new Line(
                        new Point3d(s.From.X, s.From.Y, 0),
                        new Point3d(s.To.X, s.To.Y, 0));
                    if (useLayer) line.Layer = layerName;

                    ms.AppendEntity(line);
                    tr.AddNewlyCreatedDBObject(line, true);

                    HyRoadXdata.Write(tr, db, line, intersection.Id, CrosswalkKind, SchemaVersion.Current);
                    results.Add(line.ObjectId);
                }
            }
            return results;
        }

        /// <summary>
        /// 把 <paramref name="intersection"/> 所有 <see cref="Crosswalk"/> 的停止线（L4）画为 Line。
        /// </summary>
        public IReadOnlyList<ObjectId> DrawStopLines(
            Transaction tr, Database db, Intersection intersection, string layerName)
        {
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (db == null) throw new ArgumentNullException(nameof(db));
            if (intersection == null) throw new ArgumentNullException(nameof(intersection));

            var results = new List<ObjectId>();
            if (intersection.Crosswalks.Count == 0) return results;

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            bool useLayer = !string.IsNullOrWhiteSpace(layerName) && LayerExists(tr, db, layerName);

            foreach (var cw in intersection.Crosswalks)
            {
                var (l, r) = CrosswalkDesigner.ComputeStopLine(cw);
                var line = new Line(new Point3d(l.X, l.Y, 0), new Point3d(r.X, r.Y, 0));
                if (useLayer) line.Layer = layerName;

                ms.AppendEntity(line);
                tr.AddNewlyCreatedDBObject(line, true);

                HyRoadXdata.Write(tr, db, line, intersection.Id, StopLineKind, SchemaVersion.Current);
                results.Add(line.ObjectId);
            }
            return results;
        }

        /// <summary>
        /// 按 HY_ROAD Xdata 扫模型空间，擦除所有 KIND ∈ {<see cref="CrosswalkKind"/>, <see cref="StopLineKind"/>}
        /// 且 ID=<paramref name="intersectionId"/> 的图元。
        /// </summary>
        public int ClearCrosswalkEntities(Transaction tr, Database db, Guid intersectionId)
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
                if (!string.Equals(kind, CrosswalkKind, StringComparison.Ordinal)
                    && !string.Equals(kind, StopLineKind, StringComparison.Ordinal))
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
        /// "擦旧 + 画新" 幂等重建：先清空所有 Crosswalk / StopLine，再重新画。
        /// </summary>
        public (IReadOnlyList<ObjectId> StripeIds, IReadOnlyList<ObjectId> StopLineIds) RebuildCrosswalks(
            Transaction tr,
            Database db,
            Intersection intersection,
            string crosswalkLayer,
            string stopLineLayer)
        {
            if (intersection == null) throw new ArgumentNullException(nameof(intersection));
            ClearCrosswalkEntities(tr, db, intersection.Id);
            var stripeIds = DrawCrosswalkStripes(tr, db, intersection, crosswalkLayer);
            var stopIds = DrawStopLines(tr, db, intersection, stopLineLayer);
            return (stripeIds, stopIds);
        }

        private static bool LayerExists(Transaction tr, Database db, string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName)) return false;
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            return lt.Has(layerName);
        }
    }
}
