using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Features.Reinforcement.Domain.Components;
using HyCADTool.Shared.AutoCAD.Converters;
using HyCADTool.Shared.AutoCAD.Xdata;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Features.Reinforcement.Services
{
    /// <summary>
    /// 构件识别预览：5 色 Hatch + 类型文字，挂 HY_COMPONENT XData。
    /// </summary>
    public static class ComponentPreviewService
    {
        public const string PreviewLayerName = "HY-构件预览";

        public static int DrawSession(Guid sessionId, IReadOnlyList<ComponentRegion> regions)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || regions == null || regions.Count == 0)
                return 0;

            int count = 0;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var db = doc.Database;
                EnsureLayer(tr, db);
                EraseSession(tr, db, sessionId);

                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                foreach (var region in regions.OrderBy(r => r.Priority))
                {
                    if (region?.Polygon == null || region.Polygon.VertexCount < 3)
                        continue;

                    var boundary = region.Polygon.ToAcadPolyline();
                    boundary.Layer = PreviewLayerName;
                    boundary.Closed = true;
                    btr.AppendEntity(boundary);
                    tr.AddNewlyCreatedDBObject(boundary, true);

                    var hatch = CreateSolidHatch(tr, btr, boundary, region.Type);
                    if (hatch != null)
                    {
                        HyComponentXdata.WritePreview(tr, db, hatch, sessionId, region.Id, region.Type);
                        count++;
                    }

                    var label = CreateLabel(tr, btr, region);
                    if (label != null)
                    {
                        HyComponentXdata.WritePreview(tr, db, label, sessionId, region.Id, region.Type);
                        count++;
                    }
                }

                tr.Commit();
            }

            return count;
        }

        public static void EraseSession(Guid sessionId)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || sessionId == Guid.Empty)
                return;

            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                EraseSession(tr, doc.Database, sessionId);
                tr.Commit();
            }
        }

        public static void EraseSession(Transaction tr, Database db, Guid sessionId)
        {
            var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            var toErase = new List<ObjectId>();

            foreach (ObjectId id in btr)
            {
                var obj = tr.GetObject(id, OpenMode.ForRead);
                if (HyComponentXdata.TryReadPreview(obj, out Guid sid, out _, out _)
                    && sid == sessionId)
                {
                    toErase.Add(id);
                }
            }

            foreach (var id in toErase)
            {
                var ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                ent?.Erase();
            }
        }

        public static bool TryUpdateEntityType(Entity ent, ComponentType newType)
        {
            if (ent == null || !HyComponentXdata.TryReadPreview(ent, out _, out Guid regionId, out _))
                return false;

            HyComponentXdata.UpdateType(ent, newType);
            ent.Color = Color.FromColorIndex(ColorMethod.ByAci, newType.ColorIndex());
            ComponentSession.UpdateRegionType(regionId, newType);

            if (ent is DBText text)
                text.TextString = newType.DisplayName();

            return true;
        }

        private static Hatch CreateSolidHatch(Transaction tr, BlockTableRecord btr, Polyline boundary, ComponentType type)
        {
            var hatch = new Hatch
            {
                Layer = PreviewLayerName,
                Color = Color.FromColorIndex(ColorMethod.ByAci, type.ColorIndex())
            };

            try
            {
                hatch.SetHatchPattern(HatchPatternType.PreDefined, "SOLID");
            }
            catch
            {
                hatch.Dispose();
                return null;
            }

            // Hatch 必须先入库，再设 Associative / AppendLoop / EvaluateHatch，否则 eNotInDatabase。
            btr.AppendEntity(hatch);
            tr.AddNewlyCreatedDBObject(hatch, true);

            try
            {
                hatch.Associative = true;
                hatch.AppendLoop(HatchLoopTypes.Outermost, new ObjectIdCollection { boundary.ObjectId });
                hatch.EvaluateHatch(true);
                return hatch;
            }
            catch
            {
                try { hatch.Erase(); } catch { /* 吞 */ }
                return null;
            }
        }

        private static DBText CreateLabel(Transaction tr, BlockTableRecord btr, ComponentRegion region)
        {
            var c = region.Centroid;
            var text = new DBText
            {
                Position = new Point3d(c.X, c.Y, 0),
                Height = 50,
                TextString = region.Type.DisplayName(),
                Layer = PreviewLayerName,
                Color = Color.FromColorIndex(ColorMethod.ByAci, region.Type.ColorIndex()),
                HorizontalMode = TextHorizontalMode.TextCenter,
                VerticalMode = TextVerticalMode.TextVerticalMid,
                AlignmentPoint = new Point3d(c.X, c.Y, 0)
            };

            btr.AppendEntity(text);
            tr.AddNewlyCreatedDBObject(text, true);
            return text;
        }

        private static void EnsureLayer(Transaction tr, Database db)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            if (lt.Has(PreviewLayerName)) return;

            lt.UpgradeOpen();
            var layer = new LayerTableRecord
            {
                Name = PreviewLayerName,
                Color = Color.FromColorIndex(ColorMethod.ByAci, 8)
            };
            lt.Add(layer);
            tr.AddNewlyCreatedDBObject(layer, true);
        }
    }
}
