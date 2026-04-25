using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Shared.AutoCAD.Xdata;

namespace HyCADTool.Shared.AutoCAD.Services.Road
{
    /// <summary>
    /// 独立停止线 AutoCAD 绘制服务（Infrastructure 层）。
    ///
    /// <para><b>职责</b></para>
    /// <list type="bullet">
    /// <item><see cref="DrawStopLine"/>：把一个 <see cref="StopLine"/> 画为单段带等线宽的 <see cref="Polyline"/>（ConstantWidth = StripeWidth），
    /// 挂 HY_ROAD Xdata（KIND=<see cref="StandaloneStopLineKind"/>，ID=StopLine.Id），放到 <see cref="Xdata.HyRoadLayers.StopLineLayer"/>；</item>
    /// <item><see cref="ClearStopLineById"/>：按 HY_ROAD Xdata KIND + ID 扫除指定停止线；</item>
    /// <item><see cref="ClearAllStandaloneStopLines"/>：按 KIND 一次性清除所有独立停止线（用于"批量重建"场景）。</item>
    /// </list>
    ///
    /// <para><b>与 Crosswalk 的 StopLine 区分</b></para>
    /// Crosswalk 的停止线由 <see cref="RoadCrosswalkService"/> 绘制，KIND = <see cref="RoadCrosswalkService.StopLineKind"/>
    /// （字符串 <c>"StopLine"</c>），依附 <c>Intersection.Id</c>；本服务用
    /// KIND = <see cref="StandaloneStopLineKind"/>（字符串 <c>"StandaloneStopLine"</c>），独立 <see cref="StopLine.Id"/>，
    /// 互不冲突，清理时各走各的。
    ///
    /// <para><b>线程 / 事务约束</b></para>
    /// 所有方法在调用方提供的 <see cref="Transaction"/> 中执行；本类不 StartTransaction，也不 Commit。
    /// </summary>
    public sealed class RoadStopLineService
    {
        /// <summary>HY_ROAD Xdata KIND：独立停止线（区别于 <see cref="RoadCrosswalkService.StopLineKind"/>）。</summary>
        public const string StandaloneStopLineKind = "StandaloneStopLine";

        /// <summary>
        /// 绘制单个 <see cref="StopLine"/>，挂 HY_ROAD Xdata。
        /// </summary>
        public ObjectId DrawStopLine(
            Transaction tr, Database db, StopLine stopLine, string layerName)
        {
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (db == null) throw new ArgumentNullException(nameof(db));

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            bool useLayer = !string.IsNullOrWhiteSpace(layerName) && LayerExists(tr, db, layerName);

            var pl = new Polyline(2);
            pl.AddVertexAt(0, new Point2d(stopLine.Left.X, stopLine.Left.Y), 0, stopLine.StripeWidth, stopLine.StripeWidth);
            pl.AddVertexAt(1, new Point2d(stopLine.Right.X, stopLine.Right.Y), 0, stopLine.StripeWidth, stopLine.StripeWidth);
            pl.ConstantWidth = stopLine.StripeWidth;
            if (useLayer) pl.Layer = layerName;

            ms.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);

            HyRoadXdata.Write(tr, db, pl, stopLine.Id, StandaloneStopLineKind, SchemaVersion.Current);
            return pl.ObjectId;
        }

        /// <summary>按 Xdata(ID=<paramref name="stopLineId"/>, KIND=<see cref="StandaloneStopLineKind"/>) 擦除指定停止线。</summary>
        public int ClearStopLineById(Transaction tr, Database db, Guid stopLineId)
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
                if (!string.Equals(HyRoadXdata.ReadKind(tr, ent), StandaloneStopLineKind, StringComparison.Ordinal))
                    continue;
                var id = HyRoadXdata.ReadId(tr, ent);
                if (id == Guid.Empty || id != stopLineId) continue;
                ent.UpgradeOpen();
                ent.Erase();
                removed++;
            }
            return removed;
        }

        /// <summary>按 Xdata KIND 一次清除所有独立停止线（调试 / 批量清理用）。</summary>
        public int ClearAllStandaloneStopLines(Transaction tr, Database db)
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
                if (!string.Equals(HyRoadXdata.ReadKind(tr, ent), StandaloneStopLineKind, StringComparison.Ordinal))
                    continue;
                ent.UpgradeOpen();
                ent.Erase();
                removed++;
            }
            return removed;
        }

        private static bool LayerExists(Transaction tr, Database db, string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName)) return false;
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            return lt.Has(layerName);
        }
    }
}
