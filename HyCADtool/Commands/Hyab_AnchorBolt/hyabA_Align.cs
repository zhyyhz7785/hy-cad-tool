using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool.Command
{
    public static partial class HyCommand
    {
        [CommandMethod("hyabA_Align")]
        public static void MoveBoltsToVerticalIntersection()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            try
            {
                // 选择螺栓（CIRCLE、INSERT）
                var boltRes = ed.GetSelection(
                    new PromptSelectionOptions { MessageForAdding = "\n请选择螺栓（圆或块）：" },
                    new SelectionFilter(new[]
                    {
                new TypedValue((int)DxfCode.Start, "CIRCLE,INSERT")
                    }));
                if (boltRes.Status != PromptStatus.OK) return;
                // 选择多段线
                var polyRes = ed.GetSelection(
                    new PromptSelectionOptions { MessageForAdding = "\n请选择多段线：" },
                    new SelectionFilter(new[]
                    {
                new TypedValue((int)DxfCode.Start, "LWPOLYLINE,POLYLINE")
                    }));
                if (polyRes.Status != PromptStatus.OK) return;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    var bolts = new List<(Entity Entity, Point3d OriginalCenter, Point3d? TargetPoint)>();
                    foreach (ObjectId boltId in boltRes.Value.GetObjectIds())
                    {
                        Entity boltEnt = tr.GetObject(boltId, OpenMode.ForWrite) as Entity;
                        if (boltEnt == null) continue;
                        Point3d center;
                        if (boltEnt is Circle c)
                        {
                            center = c.Center;
                        }
                        else if (boltEnt is BlockReference br)
                        {
                            center = br.Position;
                        }
                        else
                        {
                            continue; // 或 center = Point3d.Origin;
                        }
                        // 向上与向下两条射线
                        Line rayUp = new Line(center, center - Vector3d.YAxis * 1e6);
                        Line rayDown = new Line(center, center + Vector3d.YAxis * 1e6);
                        Point3d? upwardMaxY = null;
                        double maxY = double.MinValue;
                        Point3d? downwardMinY = null;
                        double minY = double.MaxValue;
                        foreach (ObjectId polyId in polyRes.Value.GetObjectIds())
                        {
                            Polyline poly = tr.GetObject(polyId, OpenMode.ForRead) as Polyline;
                            if (poly == null) continue;
                            // 向上交点
                            Point3dCollection upIntersections = new Point3dCollection();
                            rayUp.IntersectWith(poly, Intersect.ExtendBoth, upIntersections, IntPtr.Zero, IntPtr.Zero);
                            foreach (Point3d pt in upIntersections)
                            {
                                if (pt.Y > maxY)
                                {
                                    maxY = pt.Y;
                                    upwardMaxY = pt;
                                }
                            }
                            // 向下交点
                            Point3dCollection downIntersections = new Point3dCollection();
                            rayDown.IntersectWith(poly, Intersect.ExtendBoth, downIntersections, IntPtr.Zero, IntPtr.Zero);
                            foreach (Point3d pt in downIntersections)
                            {
                                if (pt.Y < minY)
                                {
                                    minY = pt.Y;
                                    downwardMinY = pt;
                                }
                            }
                        }
                        // 优先使用向上的交点
                        Point3d? finalTarget = upwardMaxY ?? downwardMinY;
                        bolts.Add((boltEnt, center, finalTarget));
                    }
                    // 重叠目标点处理（容差合并）
                    var grouped = bolts
                        .Where(b => b.TargetPoint.HasValue)
                        .GroupBy(b => b.TargetPoint.Value, new Point3dEqualityComparer())
                        .Where(g => g.Count() > 1);
                    foreach (var group in grouped)
                    {
                        var minYBolt = group.OrderBy(b => b.OriginalCenter.Y).First();
                        foreach (var bolt in group)
                        {
                            if (bolt.Entity != minYBolt.Entity)
                            {
                                bolt.Entity.TransformBy(Matrix3d.Displacement(minYBolt.OriginalCenter - bolt.OriginalCenter));
                            }
                        }
                    }
                    // 执行移动
                    foreach (var bolt in bolts)
                    {
                        if (bolt.TargetPoint.HasValue)
                        {
                            bolt.Entity.TransformBy(Matrix3d.Displacement(bolt.TargetPoint.Value - bolt.OriginalCenter));
                            ed.WriteMessage($"\n螺栓移动至交点 Y = {bolt.TargetPoint.Value.Y:F2}");
                        }
                        else
                        {
                            ed.WriteMessage("\n未找到交点，位置未变。");
                        }
                    }
                    tr.Commit();
                    ed.WriteMessage($"\n完成处理 {bolts.Count} 个螺栓。");
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n发生异常: {ex.Message}");
            }
        }
    }
    // 用于比较Point3d的相等性
    public class Point3dEqualityComparer : IEqualityComparer<Point3d>
    {
        private const double Tolerance = 0.001; // 位置重叠的容差
        public bool Equals(Point3d p1, Point3d p2)
        {
            return p1.GetVectorTo(p2).Length < Tolerance;
        }
        public int GetHashCode(Point3d p)
        {
            return p.X.GetHashCode() ^ p.Y.GetHashCode() ^ p.Z.GetHashCode();
        }
    }
}