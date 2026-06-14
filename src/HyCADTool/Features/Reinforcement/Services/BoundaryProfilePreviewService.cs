using Autodesk.AutoCAD.ApplicationServices;

using Autodesk.AutoCAD.Colors;

using Autodesk.AutoCAD.DatabaseServices;

using Autodesk.AutoCAD.Geometry;

using HyCAD.Geometry;

using HyCADTool.Features.Reinforcement.Domain;

using HyCADTool.Features.Reinforcement.Domain.Components;

using HyCADTool.Shared.AutoCAD.Converters;

using System.Collections.Generic;



namespace HyCADTool.Features.Reinforcement.Services

{

    /// <summary>

    /// 阶段 B 轮廓调试：外轮廓土/气分色 + 孔洞灰描边 + 割线参考线。

    /// </summary>

    public static class BoundaryProfilePreviewService

    {

        public const string DebugLayerName = "HY-轮廓调试";



        private const short SoilColor = 30;

        private const short AirColor = 3;

        private const short HoleOutlineColor = 8;

        private const short CutLineColor = 2;



        private const double ContactEdgeWidthMm = 50.0;

        private const double RefLineExtendMm = 500.0;

        private const double CutLineWidthMm = 25.0;



        public static int Draw(

            IReadOnlyList<IReadOnlyList<ReinRegion>> groups,

            IReadOnlyList<GroupBoundaryProfile> groupProfiles)

        {

            var doc = Application.DocumentManager.MdiActiveDocument;

            if (doc == null || groups == null || groupProfiles == null)

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



                for (int g = 0; g < groups.Count && g < groupProfiles.Count; g++)

                {

                    var groupRegions = groups[g];

                    var profile = groupProfiles[g];

                    if (groupRegions == null || profile == null)

                        continue;



                    for (int r = 0; r < groupRegions.Count; r++)

                    {

                        var region = groupRegions[r];

                        if (region?.Outer == null || region.Outer.VertexCount < 3)

                            continue;



                        count += DrawHoleOutlines(tr, btr, region);

                    }



                    if (profile.Regions != null)

                    {

                        foreach (var regionProfile in profile.Regions)

                        {

                            if (regionProfile?.Edges == null)

                                continue;



                            foreach (var edge in regionProfile.Edges)

                            {

                                if (edge?.Edge == null)

                                    continue;



                                count += DrawContactEdge(tr, btr, edge.Edge, edge.Role);

                            }

                        }

                    }



                    if (!double.IsNaN(profile.CutY))

                    {

                        double refMinX = !double.IsNaN(profile.CutIntersectionMinX)

                            ? profile.CutIntersectionMinX - RefLineExtendMm

                            : profile.GroupMinX - RefLineExtendMm;

                        double refMaxX = !double.IsNaN(profile.CutIntersectionMaxX)

                            ? profile.CutIntersectionMaxX + RefLineExtendMm

                            : profile.GroupMaxX + RefLineExtendMm;



                        count += DrawHorizontalReference(

                            tr, btr, refMinX, refMaxX, profile.CutY,

                            $"G{g} 割线 Y={profile.CutY:F2}",

                            CutLineColor, dashedLtId, dashed: true);

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



        private static int DrawContactEdge(Transaction tr, BlockTableRecord btr, Line2D edge, BoundaryEdgeRole role)

        {

            short color = role == BoundaryEdgeRole.Soil ? SoilColor : AirColor;

            var pl = new Polyline(2);

            pl.AddVertexAt(0, new Point2d(edge.StartPoint.X, edge.StartPoint.Y), 0, 0, 0);

            pl.AddVertexAt(1, new Point2d(edge.EndPoint.X, edge.EndPoint.Y), 0, 0, 0);

            pl.Layer = DebugLayerName;

            pl.Color = Color.FromColorIndex(ColorMethod.ByAci, color);

            pl.ConstantWidth = ContactEdgeWidthMm;

            btr.AppendEntity(pl);

            tr.AddNewlyCreatedDBObject(pl, true);

            return 1;

        }



        private static int DrawHorizontalReference(

            Transaction tr,

            BlockTableRecord btr,

            double minX,

            double maxX,

            double y,

            string label,

            short colorIndex,

            ObjectId dashedLtId,

            bool dashed)

        {

            var pl = new Polyline(2);

            pl.AddVertexAt(0, new Point2d(minX, y), 0, 0, 0);

            pl.AddVertexAt(1, new Point2d(maxX, y), 0, 0, 0);

            pl.Layer = DebugLayerName;

            pl.Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex);

            pl.ConstantWidth = CutLineWidthMm;



            if (dashed && !dashedLtId.IsNull)

                pl.LinetypeId = dashedLtId;



            btr.AppendEntity(pl);

            tr.AddNewlyCreatedDBObject(pl, true);



            var dbText = new DBText

            {

                Position = new Point3d(maxX + 80, y, 0),

                Height = 80,

                TextString = label,

                Layer = DebugLayerName,

                Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)

            };

            btr.AppendEntity(dbText);

            tr.AddNewlyCreatedDBObject(dbText, true);



            return 2;

        }



        private static int DrawHoleOutlines(Transaction tr, BlockTableRecord btr, ReinRegion region)

        {

            int count = 0;

            if (region.Holes == null)

                return count;



            foreach (var hole in region.Holes)

            {

                if (hole == null || hole.VertexCount < 3)

                    continue;



                var holePl = hole.ToAcadPolyline();

                holePl.Layer = DebugLayerName;

                holePl.Color = Color.FromColorIndex(ColorMethod.ByAci, HoleOutlineColor);

                holePl.Closed = true;

                btr.AppendEntity(holePl);

                tr.AddNewlyCreatedDBObject(holePl, true);

                count++;

            }



            return count;

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

                    Color = Color.FromColorIndex(ColorMethod.ByAci, 8)

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


