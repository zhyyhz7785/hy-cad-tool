using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Config;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
namespace HyCADTool.HelpClass.CreatBase
{
    public partial class ElevationModelGenerator
    {
        // 辅助方法：判断两条线是否相等
        private bool IsLineEqual(Line line1, Line line2, double tolerance)
        {
            return line1.StartPoint.IsEqualTo(line2.StartPoint, new Tolerance(tolerance, tolerance)) &&
                   line1.EndPoint.IsEqualTo(line2.EndPoint, new Tolerance(tolerance, tolerance));
        }
        // 辅助方法：反转线的方向
        private Line ReverseLine(Line line)
        {
            return new Line(line.EndPoint, line.StartPoint);
        }
        private static bool IsPointInsidePolygon(Point3d point, Polyline polyline)
        {
            int intersections = 0;
            int n = polyline.NumberOfVertices;
            for (int i = 0; i < n; i++)
            {
                Point3d p1 = polyline.GetPoint3dAt(i);
                Point3d p2 = polyline.GetPoint3dAt((i + 1) % n);
                if ((p1.Y <= point.Y && p2.Y > point.Y) || (p2.Y <= point.Y && p1.Y > point.Y))
                {
                    double xIntersection = p1.X + (point.Y - p1.Y) * (p2.X - p1.X) / (p2.Y - p1.Y);
                    if (xIntersection > point.X)
                        intersections++;
                }
            }
            return (intersections % 2) == 1; // 奇数次交叉表示在内部
        }
        // 辅助方法：判断两条线是否相交
        private bool AreLinesIntersecting(Line line1, Line line2)
        {
            Point3dCollection intersectionPoints = new Point3dCollection();
            line1.IntersectWith(line2, Intersect.OnBothOperands, intersectionPoints, IntPtr.Zero, IntPtr.Zero);
            return intersectionPoints.Count > 0;
        }
        // 辅助方法：添加警告实体
        private static void AddWarningEntity(BlockTableRecord btr, Transaction tr, Polyline polyline, string layerName, string message)
        {
            Point3d centroid = GetPolylineCentroid(polyline); // 计算质心
            var dbText = new DBText
            {
                Position = centroid,
                TextString = message,
                Layer = layerName,
                Height = 2.0 * BaseConfig.Scale
            };
            btr.AppendEntity(dbText);
            tr.AddNewlyCreatedDBObject(dbText, true);
            var warningPline = (Polyline)polyline.Clone(); // 复制原始 Polyline
            warningPline.Layer = layerName;
            btr.AppendEntity(warningPline);
            tr.AddNewlyCreatedDBObject(warningPline, true);
        }
        // 辅助方法：解析标高值
        private static double ParseExtrudeDistance(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Contains("%%P0.000") || text == "0.000" || text.Contains("±"))
                return 0.0;
            string pattern = @"[-+]?[0-9]*\.?[0-9]+";
            Match match = Regex.Match(text, pattern);
            return match.Success && double.TryParse(match.Value, out double distance) ? distance * 1000 : 0.0;
        }
        // 辅助方法：检查 Polyline 是否有效（简单检查自相交）
        private static bool IsValidPolyline(Polyline polyline)
        {
            // AutoCAD 的 Polyline 如果闭合且顶点数大于2，通常是有效的
            // 这里简单检查是否闭合且无重复顶点，复杂自相交检查需要更高级算法
            if (!polyline.Closed || polyline.NumberOfVertices < 3) return false;
            var points = new HashSet<Point3d>();
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                Point3d pt = polyline.GetPoint3dAt(i);
                if (!points.Add(pt)) return false; // 检测重复顶点
            }
            return true;
        }
        // 辅助方法：计算 Polyline 的质心
        private static Point3d GetPolylineCentroid(Polyline polyline)
        {
            double xSum = 0, ySum = 0;
            int n = polyline.NumberOfVertices;
            for (int i = 0; i < n; i++)
            {
                Point3d pt = polyline.GetPoint3dAt(i);
                xSum += pt.X;
                ySum += pt.Y;
            }
            return new Point3d(xSum / n, ySum / n, 0); // 简单平均质心
        }
        private Line GetSectionLine(Editor ed, Transaction tr)
        {
            PromptEntityOptions peo = new PromptEntityOptions("\n请选择一条直线作为剖断线: ");
            peo.SetRejectMessage("\n所选对象必须是一条直线!");
            peo.AddAllowedClass(typeof(Line), true);
            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK) return null;
            return tr.GetObject(per.ObjectId, OpenMode.ForRead) as Line;
        }
        private double GetOffsetDistance(Editor ed)
        {
            PromptDoubleOptions pdo = new PromptDoubleOptions("\n请输入剖面图的偏移距离: ")
            {
                AllowNegative = true,
                DefaultValue = DEFAULT_OFFSET
            };
            PromptDoubleResult pdr = ed.GetDouble(pdo);
            return pdr.Status == PromptStatus.OK ? pdr.Value : double.NaN;
        }
        private Tuple<Vector3d, double> CalculateTransformParameters(Line acadLine, double offsetDistance)
        {
            Vector3d lineVector = acadLine.EndPoint - acadLine.StartPoint;
            Vector3d normalVector = lineVector.GetPerpendicularVector().Negate()
                .GetNormal() * offsetDistance;
            double angle = Vector3d.XAxis.GetAngleTo(lineVector, Vector3d.ZAxis);
            return new Tuple<Vector3d, double>(normalVector, angle);
        }
        private double CalculateXDistance(Point3d point, Line sectionLine, double angle)
        {
            return (point.X - sectionLine.StartPoint.X) * Math.Cos(angle) +
                   (point.Y - sectionLine.StartPoint.Y) * Math.Sin(angle);
        }
        /// <summary>
        /// 确保图层存在（参考 EnsureLayers）
        /// </summary>
        private static void EnsureLayers(Database db, Transaction tr)
        {
            LayerTable lt = tr.GetObject(db.LayerTableId, OpenMode.ForWrite) as LayerTable;
            if (!lt.Has(CROSS_SECTION_LAYER))
            {
                LayerTableRecord ltr = new LayerTableRecord
                {
                    Name = CROSS_SECTION_LAYER,
                    Color = Color.FromColorIndex(ColorMethod.ByAci, 4) // 青色
                };
                lt.Add(ltr);
                tr.AddNewlyCreatedDBObject(ltr, true);
            }
            if (!lt.Has(BASE_LAYER))
            {
                LayerTableRecord ltr = new LayerTableRecord
                {
                    Name = BASE_LAYER,
                    Color = Color.FromColorIndex(ColorMethod.ByAci, 8) // 灰色
                };
                lt.Add(ltr);
                tr.AddNewlyCreatedDBObject(ltr, true);
            }
            if (!lt.Has(WALL_LAYER))
            {
                LayerTableRecord ltr = new LayerTableRecord
                {
                    Name = WALL_LAYER,
                    Color = Color.FromColorIndex(ColorMethod.ByAci, 1) // 红色
                };
                lt.Add(ltr);
                tr.AddNewlyCreatedDBObject(ltr, true);
            }
        }
        private static Tuple<Vector3d, double> CalculateTransformParameters(Polyline sectionLine, double offsetDistance)
        {
            var startPoint = sectionLine.GetPoint3dAt(0);
            var endPoint = sectionLine.GetPoint3dAt(sectionLine.NumberOfVertices - 1);
            Vector3d direction = endPoint - startPoint;
            direction = direction.GetNormal();
            Vector3d normalVector = direction.CrossProduct(Vector3d.ZAxis).GetNormal();
            normalVector = normalVector * offsetDistance;
            double angle = Math.Atan2(direction.Y, direction.X);
            return new Tuple<Vector3d, double>(normalVector, angle);
        }
        /// <summary>
        /// 将 Polyline 绘制到 AutoCAD
        /// </summary>
        private static void DrawPolylineToAutoCAD(Polyline polyline, Point3d basePoint, Vector3d normalVector, Database db, Transaction tr, string layer)
        {
            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
            polyline.Layer = layer;
            Matrix3d transform = Matrix3d.Displacement(normalVector) *
                                Matrix3d.Displacement(new Vector3d(basePoint.X, basePoint.Y, 0));
            polyline.TransformBy(transform);
            btr.AppendEntity(polyline);
            tr.AddNewlyCreatedDBObject(polyline, true);
        }
        private static bool IsPointInside(Polyline pline, Point3d point)
        {
            int intersections = 0;
            int nvert = pline.NumberOfVertices;
            for (int i = 0, j = nvert - 1; i < nvert; j = i++)
            {
                Point3d pi = pline.GetPoint3dAt(i);
                Point3d pj = pline.GetPoint3dAt(j);
                if (((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                    (point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y) + pi.X))
                {
                    intersections++;
                }
            }
            return (intersections % 2) == 1; // 奇数次相交表示点在内部
        }
    }
}