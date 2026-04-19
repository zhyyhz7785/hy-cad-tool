using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.Colors;
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
    /// 车道分界标线 AutoCAD 绘制服务（Infrastructure 层）。
    ///
    /// <para><b>Xdata 规约</b></para>
    /// <list type="bullet">
    /// <item>KIND = <see cref="LaneMarkingKind_"/>（固定字符串 <c>"LaneMarking"</c>，与值对象枚举 <see cref="LaneMarkingKind"/> 名冲突，故字段末尾加下划线）；</item>
    /// <item>ID = <see cref="LaneMarking.Id"/>（虚线的每段画段、双黄的两条平行线都共享同一 ID）；</item>
    /// <item>子类型（实/虚/单/双）<b>不写入 Xdata</b>——几何已表达出差异，MVP 不支持按子类型回溯重绘。</item>
    /// </list>
    ///
    /// <para><b>颜色策略</b></para>
    /// <list type="bullet">
    /// <item>白色类（<see cref="LaneMarkingKind.SolidWhite"/> / <see cref="LaneMarkingKind.DashedWhite"/>）：ColorIndex = 7（白）；</item>
    /// <item>黄色类（<see cref="LaneMarkingKind.SolidYellow"/> / <see cref="LaneMarkingKind.DoubleYellow"/>）：ColorIndex = 2（黄）。</item>
    /// </list>
    /// 图层统一走 <see cref="Xdata.HyRoadLayers.MarkingLayer"/>；通过 ColorIndex 覆盖区分。
    ///
    /// <para><b>几何实现</b></para>
    /// 所有子段均为 <see cref="Polyline"/>（2 顶点 + <c>ConstantWidth</c> = <see cref="LaneMarking.StripeWidth"/>），
    /// 与停止线一致，受 CAD 打印 / 视觉缩放正确表达"地面漆膜"。
    /// </summary>
    public sealed class RoadLaneMarkingService
    {
        /// <summary>HY_ROAD Xdata KIND（区别于 Road 模块其他 KIND）。</summary>
        public const string LaneMarkingKind_ = "LaneMarking";

        private const short ColorWhite = 7;
        private const short ColorYellow = 2;

        /// <summary>
        /// 绘制一条 <see cref="LaneMarking"/>，自动按 <see cref="LaneMarking.Kind"/> 展开成
        /// 1~N 条 <see cref="Polyline"/>（虚线多段、双黄两段、实线一段），全部共享 Id 的 Xdata。
        /// </summary>
        public IReadOnlyList<ObjectId> DrawLaneMarking(
            Transaction tr, Database db, LaneMarking marking, string layerName)
        {
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (db == null) throw new ArgumentNullException(nameof(db));

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            bool useLayer = !string.IsNullOrWhiteSpace(layerName) && LayerExists(tr, db, layerName);

            var segments = LaneMarkingDesigner.ExpandSegments(marking);
            var ids = new List<ObjectId>(segments.Count);
            short colorIdx = ColorForKind(marking.Kind);

            foreach (var seg in segments)
            {
                var pl = new Polyline(2);
                pl.AddVertexAt(0, new Point2d(seg.Start.X, seg.Start.Y), 0, marking.StripeWidth, marking.StripeWidth);
                pl.AddVertexAt(1, new Point2d(seg.End.X, seg.End.Y), 0, marking.StripeWidth, marking.StripeWidth);
                pl.ConstantWidth = marking.StripeWidth;
                if (useLayer) pl.Layer = layerName;
                pl.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIdx);

                ms.AppendEntity(pl);
                tr.AddNewlyCreatedDBObject(pl, true);

                HyRoadXdata.Write(tr, db, pl, marking.Id, LaneMarkingKind_, SchemaVersion.Current);
                ids.Add(pl.ObjectId);
            }
            return ids;
        }

        /// <summary>按 Xdata(ID, KIND=LaneMarking) 擦除指定一条 LaneMarking 的所有子段（含双黄两条 / 虚线多段）。</summary>
        public int ClearLaneMarkingById(Transaction tr, Database db, Guid markingId)
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
                if (!string.Equals(HyRoadXdata.ReadKind(tr, ent), LaneMarkingKind_, StringComparison.Ordinal))
                    continue;
                var id = HyRoadXdata.ReadId(tr, ent);
                if (id == Guid.Empty || id != markingId) continue;
                ent.UpgradeOpen();
                ent.Erase();
                removed++;
            }
            return removed;
        }

        /// <summary>按 KIND=LaneMarking 批量清除所有车道分界标线（调试 / 批量清理）。</summary>
        public int ClearAllLaneMarkings(Transaction tr, Database db)
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
                if (!string.Equals(HyRoadXdata.ReadKind(tr, ent), LaneMarkingKind_, StringComparison.Ordinal))
                    continue;
                ent.UpgradeOpen();
                ent.Erase();
                removed++;
            }
            return removed;
        }

        private static short ColorForKind(LaneMarkingKind kind)
        {
            switch (kind)
            {
                case LaneMarkingKind.SolidYellow:
                case LaneMarkingKind.DoubleYellow:
                    return ColorYellow;
                case LaneMarkingKind.SolidWhite:
                case LaneMarkingKind.DashedWhite:
                default:
                    return ColorWhite;
            }
        }

        private static bool LayerExists(Transaction tr, Database db, string layerName)
        {
            if (string.IsNullOrWhiteSpace(layerName)) return false;
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            return lt.Has(layerName);
        }
    }
}
