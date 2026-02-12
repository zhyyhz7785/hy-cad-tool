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
    /// 在交点处打断直线（HYBL）
    /// </summary>
    public class BreakLinesCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var selOpts = new PromptSelectionOptions { MessageForAdding = "\n请选择要处理的直线：" };
            var filter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LINE") });
            var selRes = ed.GetSelection(selOpts, filter);
            if (selRes.Status != PromptStatus.OK) return;

            var selectedIds = selRes.Value.GetObjectIds();

            try
            {
                using (doc.LockDocument())
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    // 收集线段
                    var lineMap = new Dictionary<ObjectId, Line>();
                    var intersections = new Dictionary<ObjectId, List<Point3d>>();

                    foreach (var id in selectedIds)
                    {
                        var line = tr.GetObject(id, OpenMode.ForRead) as Line;
                        if (line == null) continue;
                        lineMap[id] = line;
                        intersections[id] = new List<Point3d>();
                    }

                    // 计算交点
                    var ids = lineMap.Keys.ToList();
                    for (int i = 0; i < ids.Count; i++)
                    {
                        for (int j = i + 1; j < ids.Count; j++)
                        {
                            var pts = new Point3dCollection();
                            lineMap[ids[i]].IntersectWith(lineMap[ids[j]],
                                Intersect.OnBothOperands, pts, IntPtr.Zero, IntPtr.Zero);

                            foreach (Point3d pt in pts)
                            {
                                if (IsOnSegment(lineMap[ids[i]], pt))
                                    intersections[ids[i]].Add(pt);
                                if (IsOnSegment(lineMap[ids[j]], pt))
                                    intersections[ids[j]].Add(pt);
                            }
                        }
                    }

                    // 打断并创建新线段
                    var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                    var ms = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                    foreach (var kv in intersections)
                    {
                        var line = lineMap[kv.Key];
                        string layer = line.Layer;
                        var pts = kv.Value;

                        if (pts.Count > 0)
                        {
                            var sorted = pts.Distinct(new Point3dComparer())
                                .OrderBy(p => line.GetDistAtPoint(p)).ToList();

                            var all = new List<Point3d> { line.StartPoint };
                            all.AddRange(sorted);
                            all.Add(line.EndPoint);

                            for (int i = 0; i < all.Count - 1; i++)
                            {
                                if (all[i].DistanceTo(all[i + 1]) > Tolerance.Global.EqualPoint)
                                {
                                    var seg = new Line(all[i], all[i + 1]) { Layer = layer };
                                    ms.AppendEntity(seg);
                                    tr.AddNewlyCreatedDBObject(seg, true);
                                }
                            }
                        }
                        else
                        {
                            var copy = new Line(line.StartPoint, line.EndPoint) { Layer = layer };
                            ms.AppendEntity(copy);
                            tr.AddNewlyCreatedDBObject(copy, true);
                        }

                        // 删除原线
                        var orig = tr.GetObject(kv.Key, OpenMode.ForWrite) as Entity;
                        orig?.Erase();
                    }

                    tr.Commit();
                }

                ed.WriteMessage($"\n打断完成，处理 {selectedIds.Length} 条直线");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n打断失败: {ex.Message}");
            }
        }

        private static bool IsOnSegment(Line line, Point3d pt)
        {
            double total = line.Length;
            double d1 = line.StartPoint.DistanceTo(pt);
            double d2 = line.EndPoint.DistanceTo(pt);
            return Math.Abs(d1 + d2 - total) <= Tolerance.Global.EqualPoint;
        }

        private class Point3dComparer : IEqualityComparer<Point3d>
        {
            public bool Equals(Point3d a, Point3d b) => a.DistanceTo(b) <= Tolerance.Global.EqualPoint;
            public int GetHashCode(Point3d p) => 0; // 简化实现，小集合可接受
        }
    }
}
