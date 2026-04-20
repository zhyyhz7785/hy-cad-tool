using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Geometry;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road
{
    /// <summary>
    /// 路线工作台「原线」开关：在图层 <see cref="HyRoadLayers.RawPolylineLayer"/> 上绘制
    /// <see cref="Alignment.RawPickedPolyline"/>（创建时刻中心线快照），挂 HY_ROAD KIND=<see cref="HyRoadXdata.KindAlignmentRawPick"/>。
    /// </summary>
    public static class RoadAlignmentRawPolylineService
    {
        /// <summary>
        /// 为当前图纸中所有线位绘制原线；先擦除旧的原线实体（幂等）。
        /// 若某线位 <see cref="Alignment.RawPickedPolyline"/> 为空但 <see cref="Alignment.Centerline"/> 有效，
        /// 则用当前中心线克隆补齐并计入 <paramref name="rawFieldsUpgraded"/>，供调用方决定是否落盘 JSON。
        /// </summary>
        public static (int Drawn, int RawFieldsUpgraded) DrawAll(Document doc, RoadDesign design)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            if (design == null) throw new ArgumentNullException(nameof(design));

            int drawn = 0;
            int upgraded = 0;
            var db = doc.Database;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                EraseAllInternal(tr, db);

                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                foreach (var a in design.Alignments)
                {
                    if (a == null) continue;
                    var poly = a.RawPickedPolyline;
                    if (poly == null || poly.VertexCount < 2)
                    {
                        if (a.Centerline != null && a.Centerline.VertexCount >= 2)
                        {
                            a.RawPickedPolyline = a.Centerline.Clone();
                            poly = a.RawPickedPolyline;
                            upgraded++;
                        }
                        else continue;
                    }

                    var pl = RoadGeometryBridge.ToAutoCadPolyline(poly);
                    pl.Layer = HyRoadLayers.RawPolylineLayer;
                    pl.Color = Color.FromColorIndex(ColorMethod.ByLayer, 0);
                    btr.AppendEntity(pl);
                    tr.AddNewlyCreatedDBObject(pl, true);
                    HyRoadXdata.Write(tr, db, pl, a.Id, HyRoadXdata.KindAlignmentRawPick, SchemaVersion.Current);
                    drawn++;
                }

                tr.Commit();
            }

            return (drawn, upgraded);
        }

        /// <summary>擦除当前图纸 ModelSpace 中所有 KIND=<see cref="HyRoadXdata.KindAlignmentRawPick"/> 的实体。</summary>
        public static int EraseAll(Document doc)
        {
            if (doc == null) throw new ArgumentNullException(nameof(doc));
            var db = doc.Database;
            int n;
            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                n = EraseAllInternal(tr, db);
                tr.Commit();
            }
            return n;
        }

        private static int EraseAllInternal(Transaction tr, Database db)
        {
            var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
            var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);
            var toErase = new List<ObjectId>();
            foreach (ObjectId id in ms)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead);
                if (ent == null) continue;
                var k = HyRoadXdata.ReadKind(tr, ent);
                if (!string.Equals(k, HyRoadXdata.KindAlignmentRawPick, StringComparison.Ordinal)) continue;
                toErase.Add(id);
            }
            foreach (var id in toErase)
            {
                var ent = tr.GetObject(id, OpenMode.ForWrite);
                if (ent != null && !ent.IsErased) ent.Erase();
            }
            return toErase.Count;
        }
    }
}
