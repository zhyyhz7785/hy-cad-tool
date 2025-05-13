using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using Clipper2Lib;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using Exception = Autodesk.AutoCAD.Runtime.Exception;
namespace HyCADTool.Commands
{
    public static partial class HyCommand
    {
        [CommandMethod("abrcs")]
        public static void ReplacePolygonByIntersectionBatch()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            // Step 1: 用户选择图层1的参考多段线
            PromptEntityOptions peo = new PromptEntityOptions("\n请选择一个闭合多段线确定图层1：");
            peo.SetRejectMessage("只能选择闭合的多段线！");
            peo.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult res1 = ed.GetEntity(peo);
            if (res1.Status != PromptStatus.OK) return;
            // Step 2: 用户选择图层2的参考多段线
            peo.Message = "\n请选择一个闭合多段线确定图层2：";
            PromptEntityResult res2 = ed.GetEntity(peo);
            if (res2.Status != PromptStatus.OK) return;
            // Step 3: 用户选择多个待处理多段线
            PromptSelectionOptions selOpts = new PromptSelectionOptions();
            selOpts.MessageForAdding = "\n请选择要处理的多个闭合多段线：";
            selOpts.MessageForRemoval = "\n移除选择：";
            PromptSelectionResult selRes = ed.GetSelection(selOpts);
            if (selRes.Status != PromptStatus.OK) return;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                Polyline sample1 = tr.GetObject(res1.ObjectId, OpenMode.ForRead) as Polyline;
                Polyline sample2 = tr.GetObject(res2.ObjectId, OpenMode.ForRead) as Polyline;
                if (sample1 == null || !sample1.Closed || sample2 == null || !sample2.Closed)
                {
                    ed.WriteMessage("\n两个示例多段线必须是闭合的！");
                    return;
                }
                string layer1 = sample1.Layer;
                string layer2 = sample2.Layer;
                List<(ObjectId id, Point3d centroid)> polylines1 = new List<(ObjectId, Point3d)>();
                List<(Polyline poly, Point3d centroid)> polylines2 = new List<(Polyline, Point3d)>();
                SelectionSet ss = selRes.Value;
                foreach (SelectedObject so in ss)
                {
                    if (so == null) continue;
                    Polyline pl = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Polyline;
                    if (pl == null || !pl.Closed) continue;
                    Point3d centroid = CalculateCentroid(pl);
                    if (pl.Layer == layer1)
                        polylines1.Add((pl.ObjectId, centroid));
                    else if (pl.Layer == layer2)
                        polylines2.Add((pl, centroid));
                }
                int replaceCount = 0;
                foreach (var item1 in polylines1)
                {
                    ObjectId id1 = item1.id;
                    Point3d center1 = item1.centroid;
                    Polyline bestMatch = null;
                    double bestDist = double.MaxValue;
                    foreach (var item2 in polylines2)
                    {
                        Polyline poly2 = item2.poly;
                        Point3d center2 = item2.centroid;
                        if (!IsPointInsideByRayCasting(poly2, center1)) continue;
                        double dist = center1.DistanceTo(center2);
                        if (dist < bestDist)
                        {
                            bestDist = dist;
                            bestMatch = poly2;
                        }
                    }
                    if (bestMatch != null)
                    {
                        // ⚠️ 使用写模式打开 poly1
                        Polyline poly1Write = tr.GetObject(id1, OpenMode.ForWrite) as Polyline;
                        if (poly1Write != null)
                        {
                            ReplaceWithClipperLogic(tr, poly1Write, bestMatch, layer1, db, 0.7, ed);
                            replaceCount++;
                        }
                    }
                }
                ed.WriteMessage($"\n完成替换 {replaceCount} 个多边形。\n");
                tr.Commit();
            }
        }
        private static void ProcessIntersection(Transaction tr, Polyline pline1, Polyline pline2,
    string targetLayer, Editor ed, Database db)
        {
            Path64 subj = GeometryUtils.ConvertToPath64(pline1);
            Path64 clip = GeometryUtils.ConvertToPath64(pline2);
            Paths64 result = Clipper.Intersect(new Paths64 { subj }, new Paths64 { clip }, FillRule.NonZero);
            if (result == null || result.Count == 0) return;
            double area2 = Math.Abs(Clipper.Area(clip)) / 1_000_000.0;
            double totalIntersectArea = result.Sum(p => Math.Abs(Clipper.Area(p))) / 1_000_000.0;
            if (totalIntersectArea < Tolerance.Global.EqualPoint) return;
            pline1.UpgradeOpen();
            pline1.Erase();
            BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            double threshold = 0.7;
            if (totalIntersectArea >= threshold * area2)
            {
                var newPl = pline2.Clone() as Polyline;
                if (newPl != null)
                {
                    newPl.Layer = targetLayer;
                    btr.AppendEntity(newPl);
                    tr.AddNewlyCreatedDBObject(newPl, true);
                }
            }
            else
            {
                foreach (var path in result)
                {
                    var newPl = GeometryUtils.CreatePolylineFromPath64(path);
                    if (newPl != null)
                    {
                        newPl.Layer = targetLayer;
                        btr.AppendEntity(newPl);
                        tr.AddNewlyCreatedDBObject(newPl, true);
                    }
                }
            }
        }
        public static Point3d CalculateCentroid(Polyline poly)
        {
            double area = 0, cx = 0, cy = 0;
            int count = poly.NumberOfVertices;
            for (int i = 0; i < count; i++)
            {
                Point2d p0 = poly.GetPoint2dAt(i);
                Point2d p1 = poly.GetPoint2dAt((i + 1) % count);
                double cross = p0.X * p1.Y - p1.X * p0.Y;
                area += cross;
                cx += (p0.X + p1.X) * cross;
                cy += (p0.Y + p1.Y) * cross;
            }
            area *= 0.5;
            if (Math.Abs(area) < 1e-10) return poly.GetPoint3dAt(0); // fallback
            cx /= (6 * area);
            cy /= (6 * area);
            return new Point3d(cx, cy, 0);
        }
        public static bool IsPointInsideByRayCasting(Polyline poly, Point3d pt)
        {
            int count = poly.NumberOfVertices;
            bool inside = false;
            for (int i = 0, j = count - 1; i < count; j = i++)
            {
                Point2d pi = poly.GetPoint2dAt(i);
                Point2d pj = poly.GetPoint2dAt(j);
                if (((pi.Y > pt.Y) != (pj.Y > pt.Y)) &&
                    (pt.X < (pj.X - pi.X) * (pt.Y - pi.Y) / (pj.Y - pi.Y + 1e-10) + pi.X))
                {
                    inside = !inside;
                }
            }
            return inside;
        }
    }
}