using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Tools;
using System;
[assembly: CommandClass(typeof(HyCADTool.Commands.HyCommand))]
namespace HyCADTool.Commands
{
    public static partial class HyCommand
    {
        [CommandMethod("ge")]
        public static void ExtentRein()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            var doc = Application.DocumentManager.MdiActiveDocument;
            var a = HyTool.GetPolylineInfo("请选择一个需要延伸的多段线对象");
            var d = Reinforcement.AnchorageLength;
            if (a != null)
            {
                var pl = a.Value.Polyline;
                var p = a.Value.ClosestPoint;
                var param = a.Value.Parameter;
                var index = (int)Math.Floor(param);
                ExtendPolylineSegment(pl, p, d, index);
            }
        }
        /// <summary>
        /// 延伸多段线中包含点A的线段
        /// </summary>
        /// <param name="polyline">需要操作的多段线</param>
        /// <param name="pointA">位于某线段上的点A</param>
        /// <param name="D">延伸距离</param>
        /// <param name="index">包含点A的线段的起始顶点索引</param>
        public static void ExtendPolylineSegment(Polyline polyline, Point3d pointA, double D, int index)
        {
            if (polyline == null)
                throw new ArgumentNullException(nameof(polyline));
            if (D <= 0)
                throw new ArgumentException("延伸距离D必须大于0");
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                // 打开多段线以进行写操作
                Polyline pline = trans.GetObject(polyline.ObjectId, OpenMode.ForWrite) as Polyline;
                if (pline == null)
                {
                    ed.WriteMessage("\n无法打开多段线对象进行写操作。");
                    trans.Abort();
                    return;
                }
                // 验证索引是否在有效范围内
                if (index < 0 || index >= pline.NumberOfVertices - 1)
                {
                    ed.WriteMessage("\n索引超出范围。");
                    trans.Abort();
                    return;
                }
                // 获取线段的两个端点
                Point3d p1 = pline.GetPoint3dAt(index);
                Point3d p2 = pline.GetPoint3dAt(index + 1);
                LineSegment3d targetSegment = new LineSegment3d(p1, p2);
                // 确定较近的端点（点 C）
                double distToStart = pointA.DistanceTo(targetSegment.StartPoint);
                double distToEnd = pointA.DistanceTo(targetSegment.EndPoint);
                //选择点是否更接近终点
                bool extendForward = distToStart > distToEnd;
                // 计算延伸向量（单位向量 * D）
                Vector3d extensionVec = extendForward
                    ? (p2 - p1).GetNormal() * D
                    : (p1 - p2).GetNormal() * D;
                if (extendForward)
                {
                    // 延伸点 C 和之后的所有点
                    for (int i = index + 1; i < pline.NumberOfVertices; i++)
                    {
                        Point3d pt = pline.GetPoint3dAt(i);
                        Point3d newPt = pt + extensionVec;
                        pline.SetPointAt(i, new Point2d(newPt.X, newPt.Y));
                    }
                }
                else
                {
                    // 延伸点 C 和之前的所有点
                    for (int i = index; i >= 0; i--)
                    {
                        Point3d pt = pline.GetPoint3dAt(i);
                        Point3d newPt = pt + extensionVec;
                        pline.SetPointAt(i, new Point2d(newPt.X, newPt.Y));
                    }
                }
                // 提交事务
                trans.Commit();
            }
        }
        /// <summary>
        /// 将多段线的某一段延伸至指定的边界多段线并减去指定距离
        /// </summary>
        /// <param name="polyline">需要操作的多段线</param>
        /// <param name="pointA">位于某线段上的点A</param>
        /// <param name="boundaryPoly">用于确定延伸边界的多段线</param>
        /// <param name="d">从边界减去的距离</param>
        /// <param name="index">包含点A的线段的起始顶点索引</param>
        public static void ExtendPolylineSegmentToPoly(Polyline polyline, Point3d pointA, Polyline boundaryPoly, double d, int index)
        {
            if (polyline == null)
                throw new ArgumentNullException(nameof(polyline));
            if (boundaryPoly == null)
                throw new ArgumentNullException(nameof(boundaryPoly));
            if (d < 0)
                throw new ArgumentException("距离d不能为负值");
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                // 打开多段线和边界多段线
                Polyline pline = trans.GetObject(polyline.ObjectId, OpenMode.ForWrite) as Polyline;
                Polyline bline = trans.GetObject(boundaryPoly.ObjectId, OpenMode.ForRead) as Polyline;
                if (pline == null || bline == null)
                {
                    ed.WriteMessage("\n无法打开多段线对象。");
                    trans.Abort();
                    return;
                }
                // 验证索引是否在有效范围内
                if (index < 0 || index >= pline.NumberOfVertices - 1)
                {
                    ed.WriteMessage("\n索引超出范围。");
                    trans.Abort();
                    return;
                }
                // 获取线段的两个端点
                Point3d p1 = pline.GetPoint3dAt(index);
                Point3d p2 = pline.GetPoint3dAt(index + 1);
                LineSegment3d targetSegment = new LineSegment3d(p1, p2);
                // 确定较近的端点（点 C）
                double distToStart = pointA.DistanceTo(targetSegment.StartPoint);
                double distToEnd = pointA.DistanceTo(targetSegment.EndPoint);
                bool extendForward = distToStart > distToEnd;
                Point3d extensionPoint = extendForward ? p2 : p1;
                Vector3d extensionDirection = extendForward
                    ? (p2 - p1).GetNormal()
                    : (p1 - p2).GetNormal();
                // 找到边界点并计算延伸距离
                Point3d closestBoundaryPoint = FindClosestBoundaryPoint(extensionPoint, extensionDirection, bline);
                if (closestBoundaryPoint == Point3d.Origin)
                {
                    ed.WriteMessage("\n无法找到合适的边界点。");
                    trans.Abort();
                    return;
                }
                double extensionDistance = extensionPoint.DistanceTo(closestBoundaryPoint) - d;
                if (extensionDistance <= 0)
                {
                    ed.WriteMessage("\n计算的延伸距离小于等于零。");
                    trans.Abort();
                    return;
                }
                Vector3d extensionVec = extensionDirection * extensionDistance;
                // 根据延伸方向更新多段线
                if (extendForward)
                {
                    for (int i = index + 1; i < pline.NumberOfVertices; i++)
                    {
                        Point3d pt = pline.GetPoint3dAt(i);
                        Point3d newPt = pt + extensionVec;
                        pline.SetPointAt(i, new Point2d(newPt.X, newPt.Y));
                    }
                }
                else
                {
                    for (int i = index; i >= 0; i--)
                    {
                        Point3d pt = pline.GetPoint3dAt(i);
                        Point3d newPt = pt + extensionVec;
                        pline.SetPointAt(i, new Point2d(newPt.X, newPt.Y));
                    }
                }
                // 提交事务
                trans.Commit();
            }
        }
        /// <summary>
        /// 找到延伸方向上与边界多段线的最近交点
        /// </summary>
        /// <param name="startPoint">延伸起点</param>
        /// <param name="direction">延伸方向</param>
        /// <param name="boundaryPoly">边界多段线</param>
        /// <returns>最近的边界点</returns>
        private static Point3d FindClosestBoundaryPoint(Point3d startPoint, Vector3d direction, Polyline boundaryPoly)
        {
            Line extensionLine = new Line(startPoint, startPoint + direction);
            Point3d closestPoint = Point3d.Origin;
            double closestDistance = double.MaxValue;
            using (Transaction trans = boundaryPoly.Database.TransactionManager.StartTransaction())
            {
                // 遍历多段线的每个线段
                for (int i = 0; i < boundaryPoly.NumberOfVertices; i++)
                {
                    Point3d boundaryP1 = boundaryPoly.GetPoint3dAt(i);
                    Point3d boundaryP2 = boundaryPoly.GetPoint3dAt((i + 1) % boundaryPoly.NumberOfVertices);
                    // 创建边界线段
                    Line boundaryLine = new Line(boundaryP1, boundaryP2);
                    // 计算交点
                    Point3dCollection intersectionPoints = new Point3dCollection();
                    extensionLine.IntersectWith(boundaryLine, Intersect.ExtendBoth, intersectionPoints, IntPtr.Zero, IntPtr.Zero);
                    // 寻找最近的交点
                    foreach (Point3d intersection in intersectionPoints)
                    {
                        double distance = startPoint.DistanceTo(intersection);
                        if (distance < closestDistance)
                        {
                            closestPoint = intersection;
                            closestDistance = distance;
                        }
                    }
                    boundaryLine.Dispose(); // 释放边界线资源
                }
                trans.Commit();
            }
            return closestPoint;
        }
    }
}
