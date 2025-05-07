using Autodesk.AutoCAD.Colors;              // AutoCAD颜色支持
using Autodesk.AutoCAD.DatabaseServices;    // AutoCAD数据库服务
using Autodesk.AutoCAD.Geometry;
using System.Text.RegularExpressions;            // AutoCAD几何工具

namespace CadUtils
{
    /// <summary>
    /// AutoCAD交互工具类，提供图层管理和绘制方法
    /// </summary>
    public static class GeometryUtils
    {
        /// <summary>
        /// 确保指定图层存在，不存在则创建
        /// </summary>
        /// <param name="db">数据库</param>
        /// <param name="tr">事务</param>
        public static void EnsureLayers(Database db, Transaction tr)
        {
            if (db == null) throw new ArgumentNullException(nameof(db));         // 检查数据库是否为空
            if (tr == null) throw new ArgumentNullException(nameof(tr));         // 检查事务是否为空

            var layerTable = tr.GetObject(db.LayerTableId, OpenMode.ForWrite) as LayerTable; // 获取图层表

            // 定义所需图层
            string[] requiredLayers = { "Walls", "Base", "Section" };
            foreach (var layerName in requiredLayers)                           // 遍历图层名称
            {
                if (!layerTable.Has(layerName))                                 // 检查图层是否存在
                {
                    var layer = new LayerTableRecord { Name = layerName };      // 创建新图层
                    layer.Color = Color.FromColorIndex(ColorMethod.ByAci, 1);   // 设置默认颜色（红色）
                    layerTable.Add(layer);                                      // 添加到图层表
                    tr.AddNewlyCreatedDBObject(layer, true);                    // 提交新图层
                }
            }
        }
        /// <summary>
        /// 将多边形绘制到AutoCAD模型空间
        /// </summary>
        /// <param name="polyline">要绘制的多边形</param>
        /// <param name="basePoint">基准点</param>
        /// <param name="normalVector">法向量</param>
        /// <param name="db">数据库</param>
        /// <param name="tr">事务</param>
        /// <param name="layer">目标图层名称</param>
        public static void DrawPolylineToAutoCAD(Polyline polyline, Point3d basePoint, Vector3d normalVector,
            Database db, Transaction tr, string layer)
        {
            if (polyline == null) throw new ArgumentNullException(nameof(polyline)); // 检查多边形是否为空
            if (db == null) throw new ArgumentNullException(nameof(db));         // 检查数据库是否为空
            if (tr == null) throw new ArgumentNullException(nameof(tr));         // 检查事务是否为空
            if (string.IsNullOrEmpty(layer)) throw new ArgumentException("图层名称不能为空", nameof(layer)); // 检查图层名称

            var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable; // 获取块表
            var btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord; // 获取模型空间

            polyline.TransformBy(Matrix3d.Displacement(basePoint.GetAsVector())); // 移动到基准点
            polyline.Normal = normalVector;                                     // 设置法向量
            polyline.Layer = layer;                                             // 设置图层

            btr.AppendEntity(polyline);                                         // 添加到模型空间
            tr.AddNewlyCreatedDBObject(polyline, true);                         // 提交新对象
        }
        public static bool IsPointInside(Polyline pline, Point3d point)
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
            return (intersections % 2) == 1;
        }
        // 辅助方法：判断两条线是否相等
        public static bool IsLineEqual(Line line1, Line line2, double tolerance)
        {
            return line1.StartPoint.IsEqualTo(line2.StartPoint, new Tolerance(tolerance, tolerance)) &&
                   line1.EndPoint.IsEqualTo(line2.EndPoint, new Tolerance(tolerance, tolerance));
        }
        // 辅助方法：判断两条线是否相交
        public static bool AreLinesIntersecting(Line line1, Line line2)
        {
            Point3dCollection intersectionPoints = new Point3dCollection();
            line1.IntersectWith(line2, Intersect.OnBothOperands, intersectionPoints, IntPtr.Zero, IntPtr.Zero);
            return intersectionPoints.Count > 0;
        }
        // 辅助方法：反转线的方向
        public static Line ReverseLine(Line line)
        {
            return new Line(line.EndPoint, line.StartPoint);
        }
        public static bool IsPointInsidePolygon(Point3d point, Polyline polyline)
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

        // 辅助方法：添加警告实体
        public static void AddWarningEntity(BlockTableRecord btr, Transaction tr, Polyline polyline, string layerName, string message)
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

        // 辅助方法：检查 Polyline 是否有效（简单检查自相交）
        public static bool IsValidPolyline(Polyline polyline)
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
        public static Point3d GetPolylineCentroid(Polyline polyline)
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


        // 辅助方法：解析标高值
        public static double ParseExtrudeDistance(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Contains("%%P0.000") || text == "0.000" || text.Contains("±"))
                return 0.0;
            string pattern = @"[-+]?[0-9]*\.?[0-9]+";
            Match match = Regex.Match(text, pattern);
            return match.Success && double.TryParse(match.Value, out double distance) ? distance * 1000 : 0.0;
        }



        public static Tuple<Vector3d, double> CalculateTransformParameters(Polyline sectionLine, double offsetDistance)
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








    }

}