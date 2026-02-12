using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 批量多边形替换命令（abrcs）
    /// 选择两个参考多段线确定图层，再批量选择多段线，
    /// 按图层分组后自动匹配（形心在对方内部）并用交集替换
    /// </summary>
    public class ReplacePolygonBatchCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            // 选择图层1参考
            var peo = new PromptEntityOptions("\n请选择一个闭合多段线确定图层1：");
            peo.SetRejectMessage("只能选择闭合的多段线！");
            peo.AddAllowedClass(typeof(Polyline), true);
            var res1 = ed.GetEntity(peo);
            if (res1.Status != PromptStatus.OK) return;

            // 选择图层2参考
            peo.Message = "\n请选择一个闭合多段线确定图层2：";
            var res2 = ed.GetEntity(peo);
            if (res2.Status != PromptStatus.OK) return;

            // 批量选择
            var selOpts = new PromptSelectionOptions { MessageForAdding = "\n请选择要处理的多个闭合多段线：" };
            var selRes = ed.GetSelection(selOpts);
            if (selRes.Status != PromptStatus.OK) return;

            try
            {
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var sample1 = tr.GetObject(res1.ObjectId, OpenMode.ForRead) as Polyline;
                    var sample2 = tr.GetObject(res2.ObjectId, OpenMode.ForRead) as Polyline;
                    if (sample1 == null || !sample1.Closed || sample2 == null || !sample2.Closed)
                    {
                        ed.WriteMessage("\n示例多段线必须闭合！");
                        return;
                    }

                    string layer1 = sample1.Layer;
                    string layer2 = sample2.Layer;

                    var group1 = new List<(ObjectId id, Point3d centroid)>();
                    var group2 = new List<(Polyline poly, Point3d centroid)>();

                    foreach (SelectedObject so in selRes.Value)
                    {
                        if (so == null) continue;
                        var pl = tr.GetObject(so.ObjectId, OpenMode.ForRead) as Polyline;
                        if (pl == null || !pl.Closed) continue;

                        var c = GetCentroid(pl);
                        if (pl.Layer == layer1)
                            group1.Add((pl.ObjectId, c));
                        else if (pl.Layer == layer2)
                            group2.Add((pl, c));
                    }

                    int count = 0;
                    foreach (var item1 in group1)
                    {
                        Polyline best = null;
                        double bestDist = double.MaxValue;

                        foreach (var item2 in group2)
                        {
                            if (!IsPointInside(item2.poly, item1.centroid)) continue;
                            double dist = item1.centroid.DistanceTo(item2.centroid);
                            if (dist < bestDist)
                            {
                                bestDist = dist;
                                best = item2.poly;
                            }
                        }

                        if (best != null)
                        {
                            var poly1 = tr.GetObject(item1.id, OpenMode.ForWrite) as Polyline;
                            if (poly1 != null)
                            {
                                ReplacePolygonCommand.ReplaceWithIntersection(tr, poly1, best, layer1, db, ed);
                                count++;
                            }
                        }
                    }

                    tr.Commit();
                    ed.WriteMessage($"\n替换完成: {count} 个多边形");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n批量替换失败: {ex.Message}");
            }
        }

        private static Point3d GetCentroid(Polyline poly)
        {
            double area = 0, cx = 0, cy = 0;
            int n = poly.NumberOfVertices;
            for (int i = 0; i < n; i++)
            {
                var p0 = poly.GetPoint2dAt(i);
                var p1 = poly.GetPoint2dAt((i + 1) % n);
                double cross = p0.X * p1.Y - p1.X * p0.Y;
                area += cross;
                cx += (p0.X + p1.X) * cross;
                cy += (p0.Y + p1.Y) * cross;
            }
            area *= 0.5;
            if (Math.Abs(area) < 1e-10) return poly.GetPoint3dAt(0);
            return new Point3d(cx / (6 * area), cy / (6 * area), 0);
        }

        private static bool IsPointInside(Polyline poly, Point3d pt)
        {
            int n = poly.NumberOfVertices;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                var pi = poly.GetPoint2dAt(i);
                var pj = poly.GetPoint2dAt(j);
                if (((pi.Y > pt.Y) != (pj.Y > pt.Y)) &&
                    (pt.X < (pj.X - pi.X) * (pt.Y - pi.Y) / (pj.Y - pi.Y + 1e-10) + pi.X))
                    inside = !inside;
            }
            return inside;
        }
    }
}
