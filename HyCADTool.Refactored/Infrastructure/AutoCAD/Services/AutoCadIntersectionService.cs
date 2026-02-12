using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services
{
    /// <summary>
    /// AutoCAD 平台的射线-多段线交点计算实现
    /// 封装 Line.IntersectWith + Intersect.ExtendThis
    /// 对应旧代码 GetIntersectionByLinetWithBoundary
    /// </summary>
    public class AutoCadIntersectionService : ILineIntersectionService
    {
        public Point2D GetNearestForwardIntersection(
            Point2D segmentEndPoint, Vector2D direction, Polyline2D boundary)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    // 转换为 AutoCAD 类型
                    var pt1 = new Point3d(segmentEndPoint.X, segmentEndPoint.Y, 0);
                    var rawDir = new Vector3d(direction.X, direction.Y, 0);
                    if (rawDir.Length < 1e-10)
                        return segmentEndPoint; // 零向量方向无法求交
                    var dir = rawDir.GetNormal();
                    var pt2 = pt1 + dir;

                    // 创建临时射线（用 Line 模拟）
                    var line = new Line(pt1, pt2);

                    // 创建临时边界 Polyline
                    var acadBoundary = ToAcadPolyline(boundary);

                    // 求交点
                    var points = new Point3dCollection();
                    line.IntersectWith(acadBoundary, Intersect.ExtendThis, points, IntPtr.Zero, IntPtr.Zero);

                    // 筛选正方向交点
                    var forwardPoints = new List<Point3d>();
                    for (int i = 0; i < points.Count; i++)
                    {
                        var vec = points[i] - pt1;
                        if (vec.Length > 1e-10)
                        {
                            var vecNorm = vec.GetNormal();
                            // 同向判断（点积 > 0）
                            if (dir.DotProduct(vecNorm) > 0.99)
                            {
                                forwardPoints.Add(points[i]);
                            }
                        }
                    }

                    // 清理临时对象
                    line.Dispose();
                    acadBoundary.Dispose();

                    Point3d closest;
                    if (forwardPoints.Any())
                    {
                        closest = forwardPoints.OrderBy(p => pt1.DistanceTo(p)).First();
                    }
                    else if (points.Count > 0)
                    {
                        // 退化：取最近的任意交点
                        closest = Enumerable.Range(0, points.Count)
                            .Select(idx => points[idx])
                            .OrderBy(p => pt1.DistanceTo(p))
                            .First();
                    }
                    else
                    {
                        // 没有交点，沿方向延伸一个默认距离
                        closest = pt1 + dir * 1000;
                    }

                    tr.Commit();
                    return new Point2D(closest.X, closest.Y);
                }
                catch (System.Exception)
                {
                    return segmentEndPoint;
                }
            }
        }

        private static Polyline ToAcadPolyline(Polyline2D poly)
        {
            var acadPoly = new Polyline();
            for (int i = 0; i < poly.VertexCount; i++)
            {
                var pt = poly.GetPointAt(i);
                acadPoly.AddVertexAt(i, new Point2d(pt.X, pt.Y), 0, 0, 0);
            }
            acadPoly.Closed = poly.IsClosed;
            return acadPoly;
        }
    }
}
