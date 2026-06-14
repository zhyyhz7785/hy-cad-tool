using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCAD.Geometry;
using HyCADTool.Features.Reinforcement.Domain.Components;
using System.Collections.Generic;

namespace HyCADTool.Features.Reinforcement.Services
{
    /// <summary>
    /// 阶段 B 续：基础/底板分区调试可视化（图层 HY-基础调试）。
    /// </summary>
    public static class BasePartitionPreviewService
    {
        public const string DebugLayerName = "HY-基础调试";

        private const short BaseSlabColor = 5;
        private const short CutLineColor = 2;
        private const double CellOutlineWidthMm = 40.0;
        private const double CutLineWidthMm = 20.0;
        private const double RefLineExtendMm = 400.0;

        public static int Draw(
            IReadOnlyList<GroupBoundaryProfile> groupProfiles,
            IReadOnlyList<GroupBasePartitionResult> partitionResults)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || groupProfiles == null || partitionResults == null)
                return 0;

            int count = 0;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var db = doc.Database;
                EnsureLayer(tr, db);
                EraseLayer(tr, db);

                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                var dashedLtId = TryGetLinetypeId(tr, db, "DASHED");

                for (int g = 0; g < partitionResults.Count; g++)
                {
                    var result = partitionResults[g];
                    if (result == null)
                        continue;

                    if (result.BottomCells != null)
                    {
                        foreach (var cell in result.BottomCells)
                        {
                            if (cell == null)
                                continue;

                            count += DrawCell(tr, btr, cell);
                        }
                    }

                    GroupBoundaryProfile profile = g < groupProfiles.Count ? groupProfiles[g] : null;
                    if (profile != null && !double.IsNaN(profile.CutY))
                    {
                        double refMinX = profile.GroupMinX - RefLineExtendMm;
                        double refMaxX = profile.GroupMaxX + RefLineExtendMm;
                        if (!double.IsNaN(profile.CutIntersectionMinX))
                            refMinX = profile.CutIntersectionMinX - RefLineExtendMm;
                        if (!double.IsNaN(profile.CutIntersectionMaxX))
                            refMaxX = profile.CutIntersectionMaxX + RefLineExtendMm;

                        count += DrawHorizontalReference(
                            tr, btr, refMinX, refMaxX, profile.CutY,
                            $"G{g} 割线 Y={profile.CutY:F0}",
                            CutLineColor, dashedLtId);
                    }
                }

                tr.Commit();
            }

            return count;
        }

        public static void Clear()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return;

            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                EraseLayer(tr, doc.Database);
                tr.Commit();
            }
        }

        private static int DrawCell(Transaction tr, BlockTableRecord btr, BaseSlabCell cell)
        {
            var pl = new Polyline(4);
            pl.AddVertexAt(0, new Point2d(cell.X0, cell.TopL), 0, 0, 0);
            pl.AddVertexAt(1, new Point2d(cell.X1, cell.TopR), 0, 0, 0);
            pl.AddVertexAt(2, new Point2d(cell.X1, cell.BotR), 0, 0, 0);
            pl.AddVertexAt(3, new Point2d(cell.X0, cell.BotL), 0, 0, 0);
            pl.Closed = true;
            pl.Layer = DebugLayerName;
            pl.Color = Color.FromColorIndex(ColorMethod.ByAci, BaseSlabColor);
            pl.ConstantWidth = CellOutlineWidthMm;

            btr.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);

            double cx = (cell.X0 + cell.X1) / 2.0;
            double cy = (cell.TopL + cell.TopR + cell.BotL + cell.BotR) / 4.0;
            var label = new DBText
            {
                Position = new Point3d(cx, cy, 0),
                Height = 60,
                TextString = $"t={cell.ThicknessMm:F0}",
                Layer = DebugLayerName,
                Color = Color.FromColorIndex(ColorMethod.ByAci, BaseSlabColor)
            };
            btr.AppendEntity(label);
            tr.AddNewlyCreatedDBObject(label, true);

            return 2;
        }

        private static int DrawHorizontalReference(
            Transaction tr,
            BlockTableRecord btr,
            double minX,
            double maxX,
            double y,
            string label,
            short colorIndex,
            ObjectId dashedLtId)
        {
            var pl = new Polyline(2);
            pl.AddVertexAt(0, new Point2d(minX, y), 0, 0, 0);
            pl.AddVertexAt(1, new Point2d(maxX, y), 0, 0, 0);
            pl.Layer = DebugLayerName;
            pl.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
            pl.ConstantWidth = CutLineWidthMm;
            if (!dashedLtId.IsNull)
                pl.LinetypeId = dashedLtId;

            btr.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);

            var dbText = new DBText
            {
                Position = new Point3d(maxX + 60, y, 0),
                Height = 60,
                TextString = label,
                Layer = DebugLayerName,
                Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
            };
            btr.AppendEntity(dbText);
            tr.AddNewlyCreatedDBObject(dbText, true);

            return 2;
        }

        private static ObjectId TryGetLinetypeId(Transaction tr, Database db, string name)
        {
            var lt = (LinetypeTable)tr.GetObject(db.LinetypeTableId, OpenMode.ForRead);
            return lt.Has(name) ? lt[name] : ObjectId.Null;
        }

        private static void EraseLayer(Transaction tr, Database db)
        {
            var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            var toErase = new List<ObjectId>();

            foreach (ObjectId id in btr)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent != null && ent.Layer == DebugLayerName)
                    toErase.Add(id);
            }

            foreach (var id in toErase)
            {
                var ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                ent?.Erase();
            }
        }

        private static void EnsureLayer(Transaction tr, Database db)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (!lt.Has(DebugLayerName))
            {
                lt.UpgradeOpen();
                var layer = new LayerTableRecord
                {
                    Name = DebugLayerName,
                    Color = Color.FromColorIndex(ColorMethod.ByAci, BaseSlabColor)
                };
                lt.Add(layer);
                tr.AddNewlyCreatedDBObject(layer, true);
            }

            var existing = (LayerTableRecord)tr.GetObject(lt[DebugLayerName], OpenMode.ForWrite);
            existing.IsOff = false;
            existing.IsFrozen = false;
        }
    }
}
