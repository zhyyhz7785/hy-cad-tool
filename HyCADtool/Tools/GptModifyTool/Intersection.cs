using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using NetTopologySuite.Geometries;
using NetTopologySuite;
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using static HyCADTool.Tools.EtGpt;
using HyCADTool.Log;
using Exception = Autodesk.AutoCAD.Runtime.Exception;

namespace HyCADTool.Tools
{
    public static partial class EtGpt
    {
        /// <summary>
        /// 交点求解方法选项
        /// </summary>
        public enum IntersectionMethod
        {
            AutoCAD,       // 使用 AutoCAD 自带 IntersectWith
            ManualFormula, // 使用手动解析公式
            NetTopology    // 使用 NetTopologySuite
        }

        /// <summary>
        /// 通用直线交点求解
        /// </summary>
        public static List<Point3d> GetIntersectionPoints(Line line1, Line line2, IntersectionMethod method = IntersectionMethod.NetTopology, double tolerance = 1e-6)
        {
            switch (method)
            {
                case IntersectionMethod.AutoCAD:
                    return GetIntersectionPointsAutoCAD(line1, line2);
                case IntersectionMethod.ManualFormula:
                    return GetIntersectionPointsManual(line1, line2, tolerance);
                case IntersectionMethod.NetTopology:
                default:
                    return GetIntersectionPointsNetTopology(line1, line2);
            }
        }

        #region 各种内部实现

        private static List<Point3d> GetIntersectionPointsAutoCAD(Line line1, Line line2)
        {
            var points = new List<Point3d>();

            try
            {
                using (var tr = line1.Database.TransactionManager.StartTransaction())
                {
                    Point3dCollection col = new Point3dCollection();
                    line1.IntersectWith(line2, Intersect.OnBothOperands, col, IntPtr.Zero, IntPtr.Zero);

                    foreach (Point3d pt in col)
                        points.Add(pt);

                    tr.Commit();
                }
            }
            catch
            {
                // 忽略错误
            }

            return points;
        }

        private static List<Point3d> GetIntersectionPointsManual(Line line1, Line line2, double tolerance = 1e-6)
        {
            var points = new List<Point3d>();

            if (!BoundingBoxIntersects(line1, line2))
                return points;

            double x1 = line1.StartPoint.X, y1 = line1.StartPoint.Y;
            double x2 = line1.EndPoint.X, y2 = line1.EndPoint.Y;
            double x3 = line2.StartPoint.X, y3 = line2.StartPoint.Y;
            double x4 = line2.EndPoint.X, y4 = line2.EndPoint.Y;

            double denominator = (x1 - x2) * (y3 - y4) - (y1 - y2) * (x3 - x4);
            if (Math.Abs(denominator) < tolerance)
                return points;

            double px = ((x1 * y2 - y1 * x2) * (x3 - x4) - (x1 - x2) * (x3 * y4 - y3 * x4)) / denominator;
            double py = ((x1 * y2 - y1 * x2) * (y3 - y4) - (y1 - y2) * (x3 * y4 - y3 * x4)) / denominator;
            var intersection = new Point3d(px, py, 0);

            if (IsPointOnLineSegment(intersection, line1, tolerance) && IsPointOnLineSegment(intersection, line2, tolerance))
            {
                points.Add(intersection);
            }

            return points;
        }

        private static List<Point3d> GetIntersectionPointsNetTopology(Line line1, Line line2)
        {
            var factory = NtsGeometryServices.Instance.CreateGeometryFactory();

            var ntsLine1 = factory.CreateLineString(new[]
            {
                new Coordinate(line1.StartPoint.X, line1.StartPoint.Y),
                new Coordinate(line1.EndPoint.X, line1.EndPoint.Y),
            });

            var ntsLine2 = factory.CreateLineString(new[]
            {
                new Coordinate(line2.StartPoint.X, line2.StartPoint.Y),
                new Coordinate(line2.EndPoint.X, line2.EndPoint.Y),
            });

            var intersection = ntsLine1.Intersection(ntsLine2);

            var points = new List<Point3d>();
            if (intersection.IsEmpty) return points;

            if (intersection is NetTopologySuite.Geometries.Point pt)
            {
                points.Add(new Point3d(pt.X, pt.Y, 0));
            }
            else if (intersection is MultiPoint mp)
            {
                foreach (var geom in mp.Geometries)
                {
                    if (geom is NetTopologySuite.Geometries.Point p)
                        points.Add(new Point3d(p.X, p.Y, 0));
                }
            }

            return points;
        }

        private static bool BoundingBoxIntersects(Line l1, Line l2)
        {
            var ext1 = new Extents3d(l1.StartPoint, l1.EndPoint);
            var ext2 = new Extents3d(l2.StartPoint, l2.EndPoint);

            return ext1.MinPoint.X <= ext2.MaxPoint.X && ext1.MaxPoint.X >= ext2.MinPoint.X &&
                   ext1.MinPoint.Y <= ext2.MaxPoint.Y && ext1.MaxPoint.Y >= ext2.MinPoint.Y;
        }

        private static bool IsPointOnLineSegment(Point3d p, Line l, double tolerance)
        {
            double minX = Math.Min(l.StartPoint.X, l.EndPoint.X) - tolerance;
            double maxX = Math.Max(l.StartPoint.X, l.EndPoint.X) + tolerance;
            double minY = Math.Min(l.StartPoint.Y, l.EndPoint.Y) - tolerance;
            double maxY = Math.Max(l.StartPoint.Y, l.EndPoint.Y) + tolerance;

            return p.X >= minX && p.X <= maxX && p.Y >= minY && p.Y <= maxY;
        }

        #endregion

            [CommandMethod("TEST_ALL_INTERSECTIONS")]
           public static void TestIntersectionManual()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;

            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    // 提示选择直线
                    PromptSelectionOptions pso = new PromptSelectionOptions();
                    pso.MessageForAdding = "\n请选择多条直线：";

                    TypedValue[] filter = new TypedValue[]
                    {
                        new TypedValue((int)DxfCode.Start, "LINE")
                    };
                    SelectionFilter selFilter = new SelectionFilter(filter);
                    var res = ed.GetSelection(pso, selFilter);

                    if (res.Status != PromptStatus.OK)
                    {
                        ed.WriteMessage("\n未选择直线，测试取消。");
                        return;
                    }

                    var selectedIds = res.Value.GetObjectIds();
                    var lines = selectedIds
                        .Select(id => tr.GetObject(id, OpenMode.ForRead) as Line)
                        .Where(l => l != null)
                        .ToList();

                    if (lines.Count < 2)
                    {
                        ed.WriteMessage("\n直线数量不足，测试取消。");
                        return;
                    }

                    // 提示用户选择使用哪种方法
                    var method = IntersectionMethod.ManualFormula;

                    //IntersectionMethod method = IntersectionMethod.NetTopology;
                    //if (methodRes.Status == PromptStatus.OK)
                    //{
                    //    if (methodRes.StringResult == "AutoCAD")
                    //        method = IntersectionMethod.AutoCAD;
                    //    else if (methodRes.StringResult == "Manual")
                    //        method = IntersectionMethod.ManualFormula;
                    //    else if (methodRes.StringResult == "NetTopology")
                    //        method = IntersectionMethod.NetTopology;
                    //}

                    ed.WriteMessage($"\n使用方法：{method}，开始测试...");

                    // 日志记录
                    SimpleLogger.StartTiming(method.ToString());

                    int totalPoints = 0;

                    for (int i = 0; i < lines.Count - 1; i++)
                    {
                        for (int j = i + 1; j < lines.Count; j++)
                        {
                            try
                            {
                                var pts = GetIntersectionPoints(lines[i], lines[j], method);
                                totalPoints += pts.Count;

                                // 绘制交点
                                foreach (var pt in pts)
                                {
                                    DBPoint dbPt = new DBPoint(pt)
                                    {
                                        Layer = "0" // 可自行扩展不同方法不同图层
                                    };
                                    BlockTable bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                                    BlockTableRecord btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                                    btr.AppendEntity(dbPt);
                                    tr.AddNewlyCreatedDBObject(dbPt, true);
                                }
                            }
                            catch (Autodesk.AutoCAD.Runtime.Exception ex)
                            {
                                SimpleLogger.LogWarning($"交点求解失败：{ex.Message}");
                                // 单组异常跳过，不中断整体
                                continue;
                            }
                           
                        }
                    }

                    SimpleLogger.StopTiming(method.ToString());
                    ed.WriteMessage($"\n方法 {method}: 共找到 {totalPoints} 个交点。");

                    tr.Commit();
                }
            }
            catch (Exception ex)
            {
                SimpleLogger.LogError("测试交点异常", ex);
                ed.WriteMessage($"\n测试过程中出现异常: {ex.Message}");
            }
        }
        }
    }