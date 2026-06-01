using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Domain.Models.Road;
using HyCADTool.Domain.Services.Road;
using HyCAD.Geometry;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Shared.AutoCAD.Xdata;

namespace HyCADTool.Shared.AutoCAD.Services.Road
{
    /// <summary>
    /// 导流箭头 AutoCAD 绘制服务（Infrastructure 层）。GB 5768-2009 §5.3。
    ///
    /// <para><b>几何</b></para>
    /// 把 <see cref="ArrowMarkingDesigner.BuildFootprint"/> 返回的每个闭合 <see cref="Polyline2D"/>
    /// 画为一条 AutoCAD <see cref="Polyline"/>（<c>Closed = true</c>，零常宽，纯轮廓；
    /// 实际打印时由 hatch 命令单独填充）。
    ///
    /// <para><b>Xdata</b></para>
    /// 同一 <see cref="ArrowMarking"/> 展开的 1~2 条 Polyline 共享 ID，KIND=<c>ArrowMarking</c>。
    /// </summary>
    public sealed class RoadArrowMarkingService
    {
        public const string ArrowMarkingKind_ = "ArrowMarking";

        public IReadOnlyList<ObjectId> DrawArrow(
            Transaction tr, Database db, ArrowMarking arrow, string layerName)
        {
            if (tr == null) throw new ArgumentNullException(nameof(tr));
            if (db == null) throw new ArgumentNullException(nameof(db));

            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
            bool useLayer = !string.IsNullOrWhiteSpace(layerName) && LayerExists(tr, db, layerName);

            var polygons = ArrowMarkingDesigner.BuildFootprint(arrow);
            var ids = new List<ObjectId>(polygons.Count);

            foreach (var poly in polygons)
            {
                var pl = new Polyline(poly.VertexCount);
                for (int i = 0; i < poly.VertexCount; i++)
                {
                    var v = poly.GetPointAt(i);
                    pl.AddVertexAt(i, new Point2d(v.X, v.Y), 0, 0, 0);
                }
                pl.Closed = poly.IsClosed;
                if (useLayer) pl.Layer = layerName;

                ms.AppendEntity(pl);
                tr.AddNewlyCreatedDBObject(pl, true);

                HyRoadXdata.Write(tr, db, pl, arrow.Id, ArrowMarkingKind_, SchemaVersion.Current);
                ids.Add(pl.ObjectId);
            }
            return ids;
        }

        public int ClearArrowById(Transaction tr, Database db, Guid arrowId)
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
                if (!string.Equals(HyRoadXdata.ReadKind(tr, ent), ArrowMarkingKind_, StringComparison.Ordinal))
                    continue;
                var id = HyRoadXdata.ReadId(tr, ent);
                if (id == Guid.Empty || id != arrowId) continue;
                ent.UpgradeOpen();
                ent.Erase();
                removed++;
            }
            return removed;
        }

        public int ClearAllArrows(Transaction tr, Database db)
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
                if (!string.Equals(HyRoadXdata.ReadKind(tr, ent), ArrowMarkingKind_, StringComparison.Ordinal))
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
