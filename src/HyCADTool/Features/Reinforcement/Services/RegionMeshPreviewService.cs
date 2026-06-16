using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using HyCAD.Geometry;
using HyCADTool.Features.Reinforcement.Domain.Components;
using HyCADTool.Shared.AutoCAD.Converters;
using System.Collections.Generic;

namespace HyCADTool.Features.Reinforcement.Services
{
    /// <summary>区域网格化预览：竖向青、横向绿、近方形洋红、三角形黄。</summary>
    public static class RegionMeshPreviewService
    {
        public const string PreviewLayerName = "HY-网格预览";

        private const short VerticalColor = 4;
        private const short HorizontalColor = 3;
        private const short SquareColor = 6;
        private const short TriangleColor = 2;

        public static int Draw(IReadOnlyList<MeshCell> cells)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null || cells == null || cells.Count == 0)
                return 0;

            int count = 0;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var db = doc.Database;
                EnsureLayer(tr, db);
                EraseLayer(tr, db);

                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                foreach (var cell in cells)
                {
                    if (cell?.Polygon == null || cell.Polygon.VertexCount < 3)
                        continue;

                    short color = PickColor(cell);
                    var pl2d = new Polyline2D(cell.Polygon.Vertices, isClosed: true);
                    var boundary = pl2d.ToAcadPolyline();
                    boundary.Layer = PreviewLayerName;
                    boundary.Closed = true;
                    boundary.Color = Color.FromColorIndex(ColorMethod.ByAci, color);
                    btr.AppendEntity(boundary);
                    tr.AddNewlyCreatedDBObject(boundary, true);
                    count++;

                    var hatch = CreateSolidHatch(tr, btr, boundary, color);
                    if (hatch != null)
                        count++;
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

        private static short PickColor(MeshCell cell)
        {
            if (cell == null)
                return TriangleColor;

            if (cell.Kind == MeshCellKind.Triangle)
                return TriangleColor;

            switch (cell.Orientation)
            {
                case MeshCellOrientation.Vertical:
                    return VerticalColor;
                case MeshCellOrientation.Horizontal:
                    return HorizontalColor;
                case MeshCellOrientation.Square:
                    return SquareColor;
                default:
                    return SquareColor;
            }
        }

        private static Hatch CreateSolidHatch(Transaction tr, BlockTableRecord btr, Polyline boundary, short colorIndex)
        {
            var hatch = new Hatch
            {
                Layer = PreviewLayerName,
                Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
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
                try { hatch.Erase(); } catch { }
                return null;
            }
        }

        private static void EraseLayer(Transaction tr, Database db)
        {
            var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            var toErase = new List<ObjectId>();

            foreach (ObjectId id in btr)
            {
                var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                if (ent != null && ent.Layer == PreviewLayerName)
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
            if (lt.Has(PreviewLayerName))
                return;

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
