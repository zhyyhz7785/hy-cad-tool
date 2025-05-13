using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool.Commands
{
    public static partial class HyCommand
    {     
        [CommandMethod("hydimA_AddVertexAtIntersections")]
        public static void AddVertexAtIntersections()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            try
            {
                // 创建选择过滤器，只允许选择多段线和直线
                TypedValue[] filterList = new TypedValue[]
                {
            new TypedValue((int)DxfCode.Operator, "<or"),
            new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
            new TypedValue((int)DxfCode.Start, "LINE"),
            new TypedValue((int)DxfCode.Operator, "or>")
                };
                SelectionFilter filter = new SelectionFilter(filterList);
                // 提示用户选择对象
                PromptSelectionResult selResult = ed.GetSelection(filter);
                if (selResult.Status != PromptStatus.OK)
                    return;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    // 获取选择集中的所有对象
                    ObjectId[] selectedIds = selResult.Value.GetObjectIds();
                    // 存储所有直线和多段线的列表
                    List<Entity> entities = new List<Entity>();
                    foreach (ObjectId id in selectedIds)
                    {
                        Entity ent = tr.GetObject(id, OpenMode.ForRead) as Entity;
                        if (ent != null)
                        {
                            entities.Add(ent);
                        }
                    }
                    // 查找所有交点并处理
                    for (int i = 0; i < entities.Count; i++)
                    {
                        for (int j = i + 1; j < entities.Count; j++)
                        {
                            Point3dCollection intersectionPoints = new Point3dCollection();
                            entities[i].IntersectWith(entities[j],
                                Intersect.OnBothOperands,
                                intersectionPoints,
                                IntPtr.Zero,
                                IntPtr.Zero);
                            // 处理每个交点
                            foreach (Point3d pt in intersectionPoints)
                            {
                                // 如果是多段线，则添加顶点
                                if (entities[i] is Polyline pline1)
                                {
                                    AddVertexToPolyline(pline1, pt, tr);
                                }
                                if (entities[j] is Polyline pline2)
                                {
                                    AddVertexToPolyline(pline2, pt, tr);
                                }
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
        // 在多段线上的指定点添加顶点
        private static void AddVertexToPolyline(Polyline pline, Point3d pt, Transaction tr)
        {
            pline.UpgradeOpen();
            // 找到最近的顶点索引
            int closestIndex = 0;
            double minDist = double.MaxValue;
            for (int i = 0; i < pline.NumberOfVertices; i++)
            {
                Point3d vertex = pline.GetPoint3dAt(i);
                double dist = vertex.DistanceTo(pt);
                if (dist < minDist)
                {
                    minDist = dist;
                    closestIndex = i;
                }
            }
            // 找到插入点所在的线段
            int segmentIndex = -1;
            for (int i = 0; i < pline.NumberOfVertices - 1; i++)
            {
                Point3d p1 = pline.GetPoint3dAt(i);
                Point3d p2 = pline.GetPoint3dAt(i + 1);
                if (IsPointOnSegment(pt, p1, p2))
                {
                    segmentIndex = i;
                    break;
                }
            }
            if (segmentIndex >= 0)
            {
                // 在指定位置添加新顶点
                pline.AddVertexAt(segmentIndex + 1,
                    new Point2d(pt.X, pt.Y),
                    0, // 凸度
                    0, // 起始宽度
                    0); // 结束宽度
            }
        }
        // 判断点是否在线段上
        private static bool IsPointOnSegment(Point3d pt, Point3d p1, Point3d p2)
        {
            double tolerance = Tolerance.Global.EqualPoint;
            // 计算点到直线的距离
            double dist = new LineSegment3d(p1, p2).GetDistanceTo(pt);
            // 检查点是否在线段范围内
            double length = p1.DistanceTo(p2);
            double d1 = p1.DistanceTo(pt);
            double d2 = p2.DistanceTo(pt);
            return dist <= tolerance && d1 <= length + tolerance && d2 <= length + tolerance;
        }
    }
}