using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCAD.Geometry;
using HyCADTool.Features.Reinforcement.Domain;
using HyCADTool.Shared.AutoCAD.Converters;
using System.Collections.Generic;

namespace HyCADTool.Features.Reinforcement.Services
{
    /// <summary>
    /// 嵌套/分组调试预览：ReinRegion 混凝土 SOLID 填充 + 外/孔边界 + bbox + 首顶点。
    /// </summary>
    public static class BoundaryClassifyPreviewService
    {
        public const string DebugLayerName = "HY-嵌套调试";

        private const short HoleEdgeColor = 1;
        private const short FirstVertexColor = 2;

        /// <summary>计算组配色：G0=红 G1=蓝 G2=绿 G3=洋红 G4=黄 G5=青 …</summary>
        private static readonly short[] GroupColors = { 1, 5, 3, 6, 2, 4, 30, 140 };

        private static short GetGroupColor(int groupIndex) =>
            GroupColors[groupIndex % GroupColors.Length];

        public static string DescribeGroupColor(int groupIndex)
        {
            switch (GetGroupColor(groupIndex))
            {
                case 1: return "红";
                case 5: return "蓝";
                case 3: return "绿";
                case 6: return "洋红";
                case 2: return "黄";
                case 4: return "青";
                default: return $"ACI{GetGroupColor(groupIndex)}";
            }
        }

        /// <summary>§2.3：按计算组分色（外包框 + 填充 + 边线同色）。</summary>
        public static int DrawGrouped(
            IReadOnlyList<ReinRegionBuilder.ClassifiedBoundary> classified,
            IReadOnlyList<IReadOnlyList<ReinRegion>> groups,
            IReadOnlyList<IReadOnlyList<int>> outerClusters)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                return 0;

            int count = 0;
            using (doc.LockDocument())
            using (var tr = doc.Database.TransactionManager.StartTransaction())
            {
                var db = doc.Database;
                EnsureLayer(tr, db);
                EraseLayer(tr, db);

                var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                if (outerClusters != null)
                {
                    for (int g = 0; g < outerClusters.Count; g++)
                    {
                        short groupColor = GetGroupColor(g);
                        var memberIndices = outerClusters[g];
                        if (memberIndices == null || memberIndices.Count == 0)
                            continue;

                        var memberBounds = new List<Polyline2D>();
                        foreach (int outerIdx in memberIndices)
                        {
                            if (classified == null || outerIdx < 0 || outerIdx >= classified.Count)
                                continue;

                            var b = classified[outerIdx].Boundary;
                            if (b == null || b.VertexCount < 3)
                                continue;

                            memberBounds.Add(b);
                            count += DrawBbox(
                                tr, btr, b, groupColor,
                                constantWidth: 0,
                                label: $"G{g} 外#{outerIdx}");
                        }

                        if (memberBounds.Count > 0)
                        {
                            count += DrawGroupEnvelopeBbox(
                                tr, btr, memberBounds, groupColor,
                                $"G{g} 计算组×{memberBounds.Count}");
                        }
                    }
                }

                if (groups != null)
                {
                    for (int g = 0; g < groups.Count; g++)
                    {
                        short groupColor = GetGroupColor(g);
                        var groupRegions = groups[g];
                        if (groupRegions == null)
                            continue;

                        for (int r = 0; r < groupRegions.Count; r++)
                        {
                            var region = groupRegions[r];
                            if (region?.Outer == null || region.Outer.VertexCount < 3)
                                continue;

                            int holeCount = region.Holes?.Count ?? 0;
                            count += DrawConcreteRegion(
                                tr, btr, region, groupColor,
                                $"G{g}-R{r} 混凝土 孔{holeCount}");
                        }
                    }
                }

                if (classified != null)
                {
                    for (int i = 0; i < classified.Count; i++)
                    {
                        var item = classified[i];
                        if (item?.Boundary == null || item.Boundary.VertexCount < 3)
                            continue;

                        string role = item.IsHole ? "孔" : "外";
                        count += DrawFirstVertexMarker(
                            tr, btr, item.Boundary.GetPointAt(0), $"P0#{i} {role} d={item.NestingDepth}");
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

        private static int DrawConcreteRegion(
            Transaction tr,
            BlockTableRecord btr,
            ReinRegion region,
            short groupColor,
            string label)
        {
            int count = 0;

            var outerPl = region.Outer.ToAcadPolyline();
            outerPl.Layer = DebugLayerName;
            outerPl.Color = Color.FromColorIndex(ColorMethod.ByAci, groupColor);
            outerPl.Closed = true;
            btr.AppendEntity(outerPl);
            tr.AddNewlyCreatedDBObject(outerPl, true);
            count++;

            var holePls = new List<Polyline>();
            if (region.Holes != null)
            {
                foreach (var hole in region.Holes)
                {
                    if (hole == null || hole.VertexCount < 3)
                        continue;

                    var holePl = hole.ToAcadPolyline();
                    holePl.Layer = DebugLayerName;
                    holePl.Color = Color.FromColorIndex(ColorMethod.ByAci, HoleEdgeColor);
                    holePl.Closed = true;
                    btr.AppendEntity(holePl);
                    tr.AddNewlyCreatedDBObject(holePl, true);
                    holePls.Add(holePl);
                    count++;
                }
            }

            if (CreateSolidHatch(tr, btr, outerPl, holePls, groupColor) != null)
                count++;

            var centroid = new Polygon2D(region.Outer.Vertices, isClosed: true).GetCentroid();
            count += DrawLabel(tr, btr, centroid, label, groupColor, 80);

            return count;
        }

        /// <summary>单条外轮廓 axis-aligned bbox（细线）。</summary>
        private static int DrawBbox(
            Transaction tr,
            BlockTableRecord btr,
            Polyline2D boundary,
            short colorIndex,
            double constantWidth,
            string label)
        {
            if (!TryGetBounds(boundary, out double minX, out double maxX, out double minY, out double maxY))
                return 0;

            var pl = CreateRectPolyline(minX, minY, maxX, maxY, colorIndex, constantWidth);
            btr.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);

            var center = new Point2D((minX + maxX) / 2.0, (minY + maxY) / 2.0);
            DrawLabel(tr, btr, center, label, colorIndex, 50);
            return 2;
        }

        /// <summary>计算组整体外包框（组内所有外轮廓 bbox 的并集，粗线）。</summary>
        private static int DrawGroupEnvelopeBbox(
            Transaction tr,
            BlockTableRecord btr,
            IReadOnlyList<Polyline2D> members,
            short colorIndex,
            string label)
        {
            double minX = double.MaxValue, maxX = double.MinValue;
            double minY = double.MaxValue, maxY = double.MinValue;

            foreach (var boundary in members)
            {
                if (!TryGetBounds(boundary, out double bx0, out double bx1, out double by0, out double by1))
                    continue;

                minX = System.Math.Min(minX, bx0);
                maxX = System.Math.Max(maxX, bx1);
                minY = System.Math.Min(minY, by0);
                maxY = System.Math.Max(maxY, by1);
            }

            if (minX == double.MaxValue)
                return 0;

            var pl = CreateRectPolyline(minX, minY, maxX, maxY, colorIndex, constantWidth: 40);
            btr.AppendEntity(pl);
            tr.AddNewlyCreatedDBObject(pl, true);

            var center = new Point2D((minX + maxX) / 2.0, maxY + 120);
            DrawLabel(tr, btr, center, label, colorIndex, 100);
            return 2;
        }

        private static bool TryGetBounds(
            Polyline2D boundary,
            out double minX,
            out double maxX,
            out double minY,
            out double maxY)
        {
            minX = double.MaxValue;
            maxX = double.MinValue;
            minY = double.MaxValue;
            maxY = double.MinValue;

            if (boundary == null || boundary.VertexCount < 3)
                return false;

            for (int i = 0; i < boundary.VertexCount; i++)
            {
                var p = boundary.GetPointAt(i);
                minX = System.Math.Min(minX, p.X);
                maxX = System.Math.Max(maxX, p.X);
                minY = System.Math.Min(minY, p.Y);
                maxY = System.Math.Max(maxY, p.Y);
            }

            return minX != double.MaxValue;
        }

        private static Polyline CreateRectPolyline(
            double minX,
            double minY,
            double maxX,
            double maxY,
            short colorIndex,
            double constantWidth)
        {
            var pl = new Polyline(4);
            pl.AddVertexAt(0, new Point2d(minX, minY), 0, 0, 0);
            pl.AddVertexAt(1, new Point2d(maxX, minY), 0, 0, 0);
            pl.AddVertexAt(2, new Point2d(maxX, maxY), 0, 0, 0);
            pl.AddVertexAt(3, new Point2d(minX, maxY), 0, 0, 0);
            pl.Closed = true;
            pl.Layer = DebugLayerName;
            pl.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);
            if (constantWidth > 0)
                pl.ConstantWidth = constantWidth;
            return pl;
        }

        private static Hatch CreateSolidHatch(
            Transaction tr,
            BlockTableRecord btr,
            Polyline outer,
            IReadOnlyList<Polyline> holes,
            short colorIndex)
        {
            var hatch = new Hatch
            {
                Layer = DebugLayerName,
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
                hatch.AppendLoop(HatchLoopTypes.Outermost, new ObjectIdCollection { outer.ObjectId });

                if (holes != null)
                {
                    foreach (var hole in holes)
                    {
                        if (hole == null || hole.ObjectId.IsNull)
                            continue;

                        hatch.AppendLoop(HatchLoopTypes.Default, new ObjectIdCollection { hole.ObjectId });
                    }
                }

                hatch.EvaluateHatch(true);
                return hatch;
            }
            catch
            {
                try { hatch.Erase(); } catch { /* 吞 */ }
                return null;
            }
        }

        private static int DrawFirstVertexMarker(
            Transaction tr,
            BlockTableRecord btr,
            Point2D p,
            string label)
        {
            var circle = new Circle(
                new Point3d(p.X, p.Y, 0),
                Vector3d.ZAxis,
                30);
            circle.Layer = DebugLayerName;
            circle.Color = Color.FromColorIndex(ColorMethod.ByAci, FirstVertexColor);
            btr.AppendEntity(circle);
            tr.AddNewlyCreatedDBObject(circle, true);

            DrawLabel(tr, btr, new Point2D(p.X + 40, p.Y + 40), label, FirstVertexColor, 40);
            return 2;
        }

        private static int DrawLabel(
            Transaction tr,
            BlockTableRecord btr,
            Point2D position,
            string text,
            short colorIndex,
            double height)
        {
            var dbText = new DBText
            {
                Position = new Point3d(position.X, position.Y, 0),
                Height = height,
                TextString = text,
                Layer = DebugLayerName,
                Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex),
                HorizontalMode = TextHorizontalMode.TextCenter,
                VerticalMode = TextVerticalMode.TextVerticalMid,
                AlignmentPoint = new Point3d(position.X, position.Y, 0)
            };
            btr.AppendEntity(dbText);
            tr.AddNewlyCreatedDBObject(dbText, true);
            return 1;
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
            if (lt.Has(DebugLayerName))
                return;

            lt.UpgradeOpen();
            var layer = new LayerTableRecord
            {
                Name = DebugLayerName,
                Color = Color.FromColorIndex(ColorMethod.ByAci, 8)
            };
            lt.Add(layer);
            tr.AddNewlyCreatedDBObject(layer, true);
        }
    }
}
