using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Clipper2Lib;
using System;
using System.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Pad
{
    /// <summary>
    /// 多边形替换命令（abrc）
    /// 选择两个闭合多段线，用交集替换第一个多段线
    /// 若交集面积 >= 70% 参考面积，直接用参考多段线替换
    /// </summary>
    public class ReplacePolygonCommand
    {
        private const double Scale = 1000.0;
        private const double AreaThreshold = 0.7;

        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            // 选择第一个（被替换）
            var peo1 = new PromptEntityOptions("\n请选择第一个闭合多段线（被替换）：");
            peo1.SetRejectMessage("只能选择闭合的多段线！");
            peo1.AddAllowedClass(typeof(Polyline), true);
            var res1 = ed.GetEntity(peo1);
            if (res1.Status != PromptStatus.OK) return;

            // 选择第二个（参考）
            var peo2 = new PromptEntityOptions("\n请选择第二个闭合多段线（参考）：");
            peo2.SetRejectMessage("只能选择闭合的多段线！");
            peo2.AddAllowedClass(typeof(Polyline), true);
            var res2 = ed.GetEntity(peo2);
            if (res2.Status != PromptStatus.OK) return;

            try
            {
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var pline1 = tr.GetObject(res1.ObjectId, OpenMode.ForWrite) as Polyline;
                    var pline2 = tr.GetObject(res2.ObjectId, OpenMode.ForRead) as Polyline;

                    if (pline1 == null || pline2 == null || !pline1.Closed || !pline2.Closed)
                    {
                        ed.WriteMessage("\n两个多段线必须是闭合的！");
                        return;
                    }

                    ReplaceWithIntersection(tr, pline1, pline2, pline1.Layer, db, ed);
                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n替换失败: {ex.Message}");
            }
        }

        internal static void ReplaceWithIntersection(
            Transaction tr, Polyline pline1, Polyline pline2,
            string targetLayer, Database db, Editor ed)
        {
            var subj = ToPath64(pline1);
            var clip = ToPath64(pline2);
            var result = Clipper.Intersect(new Paths64 { subj }, new Paths64 { clip }, FillRule.NonZero);

            if (result == null || result.Count == 0)
            {
                ed.WriteMessage("\n交集为空");
                return;
            }

            double area2 = Math.Abs(Clipper.Area(clip)) / (Scale * Scale);
            double intersectArea = result.Sum(p => Math.Abs(Clipper.Area(p))) / (Scale * Scale);

            const double MinValidArea = 1e-6;
            if (intersectArea < MinValidArea)
            {
                ed.WriteMessage("\n交集面积太小");
                return;
            }

            pline1.Erase();
            var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

            if (intersectArea >= AreaThreshold * area2)
            {
                var clone = pline2.Clone() as Polyline;
                if (clone != null)
                {
                    clone.Layer = targetLayer;
                    btr.AppendEntity(clone);
                    tr.AddNewlyCreatedDBObject(clone, true);
                }
            }
            else
            {
                foreach (var path in result)
                {
                    var poly = FromPath64(path);
                    if (poly != null)
                    {
                        poly.Layer = targetLayer;
                        btr.AppendEntity(poly);
                        tr.AddNewlyCreatedDBObject(poly, true);
                    }
                }
            }

            ed.WriteMessage($"\n交集面积: {intersectArea:F2}, 参考面积: {area2:F2}, 比例 {intersectArea / area2:P2}");
        }

        internal static Path64 ToPath64(Polyline pline)
        {
            var path = new Path64();
            for (int i = 0; i < pline.NumberOfVertices; i++)
            {
                var pt = pline.GetPoint2dAt(i);
                path.Add(new Point64((long)(pt.X * Scale), (long)(pt.Y * Scale)));
            }
            return path;
        }

        internal static Polyline FromPath64(Path64 path)
        {
            var pline = new Polyline();
            for (int i = 0; i < path.Count; i++)
            {
                pline.AddVertexAt(i, new Point2d(path[i].X / Scale, path[i].Y / Scale), 0, 0, 0);
            }
            pline.Closed = true;
            return pline;
        }
    }
}
