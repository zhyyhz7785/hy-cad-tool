using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;

namespace HyCADTool.Features.Dimension
{
    /// <summary>
    /// 在多段线交点处添加顶点命令（对应旧命令 hydimA_AddVertexAtIntersections）
    /// 流程：选择多段线/直线 → 计算两两交点 → 在多段线上添加顶点
    /// </summary>
    public class AddVertexAtIntersectionsCommand
    {
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            try
            {
                // 过滤：只选多段线和直线
                var filterList = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Operator, "<or"),
                    new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                    new TypedValue((int)DxfCode.Start, "LINE"),
                    new TypedValue((int)DxfCode.Operator, "or>")
                };
                var filter = new SelectionFilter(filterList);

                PromptSelectionResult selResult = ed.GetSelection(filter);
                if (selResult.Status != PromptStatus.OK) return;

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var selectedIds = selResult.Value.GetObjectIds();
                    var entities = new List<Entity>();

                    foreach (ObjectId id in selectedIds)
                    {
                        var ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent != null) entities.Add(ent);
                    }

                    // 两两求交点
                    for (int i = 0; i < entities.Count; i++)
                    {
                        for (int j = i + 1; j < entities.Count; j++)
                        {
                            var intersectionPoints = new Point3dCollection();
                            entities[i].IntersectWith(entities[j],
                                Intersect.OnBothOperands,
                                intersectionPoints,
                                IntPtr.Zero, IntPtr.Zero);

                            foreach (Point3d pt in intersectionPoints)
                            {
                                if (entities[i] is Polyline pline1)
                                    AddVertexToPolyline(pline1, pt);
                                if (entities[j] is Polyline pline2)
                                    AddVertexToPolyline(pline2, pt);
                            }
                        }
                    }

                    tr.Commit();
                }
                ed.Regen();
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}");
            }
        }

        /// <summary>
        /// 在多段线指定点处添加顶点
        /// </summary>
        private static void AddVertexToPolyline(Polyline pline, Point3d pt)
        {
            pline.UpgradeOpen();

            // 找到插入点所在的线段
            for (int i = 0; i < pline.NumberOfVertices - 1; i++)
            {
                Point3d p1 = pline.GetPoint3dAt(i);
                Point3d p2 = pline.GetPoint3dAt(i + 1);

                if (IsPointOnSegment(pt, p1, p2))
                {
                    pline.AddVertexAt(i + 1, new Point2d(pt.X, pt.Y), 0, 0, 0);
                    return;
                }
            }
        }

        /// <summary>
        /// 判断点是否在线段上
        /// </summary>
        private static bool IsPointOnSegment(Point3d pt, Point3d p1, Point3d p2)
        {
            double tolerance = Tolerance.Global.EqualPoint;
            double dist = new LineSegment3d(p1, p2).GetDistanceTo(pt);
            double length = p1.DistanceTo(p2);
            double d1 = p1.DistanceTo(pt);
            double d2 = p2.DistanceTo(pt);
            return dist <= tolerance && d1 <= length + tolerance && d2 <= length + tolerance;
        }
    }
}
