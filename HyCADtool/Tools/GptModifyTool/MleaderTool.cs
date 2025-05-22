using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.BoundaryRepresentation;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Config;
using System;
using System.Collections.Generic;
using System.Linq;
using Exception = Autodesk.AutoCAD.BoundaryRepresentation.Exception;
namespace HyCADTool.Tools
{
    public static partial class HyTool
    {
       
        public static MLeader AddMleader(this Point3d[] points, double distance, string content)
        {
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            var startP = points.FirstOrDefault();
            var endP = points.LastOrDefault();
            // 创建 MLeader 对象
            MLeader ml = new MLeader();
            // 确定标注集中点位置（引线最终集中到此点）
            var vecH = (endP - startP).GetNormal();
            var line = new Line(startP, endP);
            // 调整引线方向（如果在第二三象限，起点终点互换）
            if (line.Angle > Math.PI / 2 && line.Angle <= Math.PI * 3 / 2)
            {
                points = points.Reverse().ToArray();
                line = new Line(points.First(), points.Last());
            }
            // 计算垂直偏移向量
            var vecV = vecH.RotateBy(-Math.PI / 2, Vector3d.ZAxis);
            // 集中点为最后一个点偏移后的位置
            var centralPoint = endP + vecV * distance;
            // 添加每个点的引线，集中于 centralPoint
            foreach (var point in points)
            {
                // 创建引线组和引线
                int leaderIndex = ml.AddLeader();
                int leaderLineIndex = ml.AddLeaderLine(leaderIndex);
                // 添加起点和终点
                ml.AddFirstVertex(leaderLineIndex, point);
                ml.AddLastVertex(leaderLineIndex, centralPoint);
            }
            // 5设置引线样式为系统当前样式
            ml.MLeaderStyle = db.MLeaderstyle;
            // 设置标注字体
            MText mt = new MText();
            mt.TextStyleId = ml.TextStyleId;
            mt.Color = ml.TextColor;
            mt.TextHeight = ml.TextHeight;
            mt.Contents = content;
            //var angle = ChangeLineRotationForMleader(line.Angle);
            var angle = line.Angle;
            mt.Rotation = angle;
            ml.MText = mt;
            return ml;
        }
        public static MLeader AddMleaderSinglePoint(this Point3d point, Point3d endPoint, string content)
        {
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            MLeader ml = new MLeader();
            ml.MLeaderStyle = db.MLeaderstyle;
            ml.EnableDogleg = true; // 启用 Dogleg
            ml.DoglegLength = 2.5; // 设置 Dogleg 长度（单位：图纸单位，可调整）
            // 创建 MText
            MText mt = new MText();
            mt.TextStyleId = ml.TextStyleId;
            mt.Color = ml.TextColor;
            mt.TextHeight = ml.TextHeight;
            // 添加字宽比例格式化代码
            double widthFactor = 0.7; // 设置字宽比例（可调整）
            string formattedContent = $"\\W{widthFactor};{content}";
            mt.Contents = formattedContent;
            // 设置 MText 的附着点为顶部左端，使基线位于第一行下方
            mt.Attachment = AttachmentPoint.TopLeft;
            mt.Location = endPoint; // 初始位置设置为引线终点
            // 添加引线
            int leaderIndex = ml.AddLeader();
            int leaderLineIndex = ml.AddLeaderLine(leaderIndex);
            ml.AddFirstVertex(leaderLineIndex, point);   // 引线起点
            ml.AddLastVertex(leaderLineIndex, endPoint); // 引线终点
            // 将 MText 附加到 MLeader
            ml.MText = mt;
            return ml;
        }
        public static MLeader AddMleaderOne(this Point3d[] points, double distance, string content)
        {
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            var startP = points.FirstOrDefault();
            var endP = points.LastOrDefault();
            var centerP = CalculateMidPoint(startP, endP);
            // 创建 MLeader 对象
            MLeader ml = new MLeader();
            // 确定标注集中点位置（引线最终集中到此点）
            var vecH = (endP - startP).GetNormal();
            var line = new Line(startP, endP);
            // 调整引线方向（如果在第二三象限，起点终点互换）
            if (line.Angle > Math.PI / 2 && line.Angle <= Math.PI * 3 / 2)
            {
                points = points.Reverse().ToArray();
                line = new Line(points.First(), points.Last());
            }
            // 计算垂直偏移向量
            var vecV = vecH.RotateBy(-Math.PI / 2, Vector3d.ZAxis);
            // 集中点为最后一个点偏移后的位置
            var centralPoint = centerP + vecV * distance;
            // 添加每个点的引线，集中于 centralPoint
            int leaderIndex = ml.AddLeader();
            int leaderLineIndex = ml.AddLeaderLine(leaderIndex);
            // 添加起点和终点
            ml.AddFirstVertex(leaderLineIndex, points[1]);
            ml.AddLastVertex(leaderLineIndex, centralPoint);
            // 5设置引线样式为系统当前样式
            ml.MLeaderStyle = db.MLeaderstyle;
            // 设置标注字体
            MText mt = new MText();
            mt.TextStyleId = ml.TextStyleId;
            mt.Color = ml.TextColor;
            mt.TextHeight = ml.TextHeight;
            mt.Contents = content;
            //var angle = ChangeLineRotationForMleader(line.Angle);
            var angle = line.Angle;
            mt.Rotation = angle;
            ml.MText = mt;
            return ml;
        }
        public static MLeader AddMleaderSix(this Point3d[] points, double distance, string content)
        {
            double DimDistance = 465;
            var db = Application.DocumentManager.MdiActiveDocument.Database;
            var startP = points.FirstOrDefault();
            var endP = points.LastOrDefault();
            var centerP = CalculateMidPoint(points[1], points[4]);
            // 创建 MLeader 对象
            MLeader ml = new MLeader();
            // 确定标注集中点位置（引线最终集中到此点）
            var vecH = (endP - startP).GetNormal();
            var line = new Line(startP, endP);
            // 调整引线方向（如果在第二三象限，起点终点互换）
            if (line.Angle > Math.PI / 2 && line.Angle <= Math.PI * 3 / 2)
            {
                points = points.Reverse().ToArray();
                line = new Line(points.First(), points.Last());
            }
            // 计算垂直偏移向量
            var vecV = vecH.RotateBy(-Math.PI / 2, Vector3d.ZAxis);
            // 集中点为最后一个点偏移后的位置
            //var centralPoint = centerP + vecV * distance;
            var centralPoint = centerP + Vector3d.XAxis * DimDistance;
            // 添加每个点的引线，集中于 centralPoint
            // 添加起点和终点
            foreach (var point in points)
            {
                // 创建引线组和引线
                int leaderIndex = ml.AddLeader();
                int leaderLineIndex = ml.AddLeaderLine(leaderIndex);
                // 添加起点和终点
                ml.AddFirstVertex(leaderLineIndex, point);
                ml.AddLastVertex(leaderLineIndex, centralPoint);
            }
            // 5设置引线样式为系统当前样式
            ml.MLeaderStyle = db.MLeaderstyle;
            // 设置标注字体
            MText mt = new MText();
            mt.TextStyleId = ml.TextStyleId;
            mt.Color = ml.TextColor;
            mt.TextHeight = ml.TextHeight;
            mt.Contents = content;
            //var angle = ChangeLineRotationForMleader(line.Angle);
            //var angle = line.Angle;
            mt.Rotation = 0;
            ml.MText = mt;
            return ml;
        }
        private static Point3d CalculateMidPoint(Point3d p1, Point3d p2)
        {
            double midX = (p1.X + p2.X) / 2.0;
            double midY = (p1.Y + p2.Y) / 2.0;
            double midZ = (p1.Z + p2.Z) / 2.0;
            return new Point3d(midX, midY, midZ);
        }
        public static List<Point3d> GetReinPoints()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            List<Point3d> points = new List<Point3d>();
            var plResult = HyTool.GetPolylineInfo("请选择一个需要标注多段线对象");
            if (plResult == null)
            {
                return null;
            }
            var poly = plResult.Value.Polyline;
            var point = plResult.Value.ClosestPoint;
            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Polyline pl = tr.GetObject(poly.ObjectId, OpenMode.ForRead) as Polyline;
                    if (pl == null)
                    {
                        ed.WriteMessage("\n无法获取所选的多段线对象。");
                        return points; // 返回空列表
                    }
                    // 调用方法获取三个点
                    points = GetPointsAlongPolyline(pl, point, 100, 17.5);
                    tr.Commit();
                }
                return points;
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n出错：" + ex.Message);
                return points; // 返回空列表
            }
        }
        public static List<Point3d> GetReinPointsByPoint(Point3d p)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            List<Point3d> points = new List<Point3d>();
            var plResult = HyTool.GetPolylineInfo("请选择一个需要标注多段线对象");
            if (plResult == null)
            {
                return null;
            }
            var poly = plResult.Value.Polyline;
            var lineSegment3D = plResult.Value.SelectedSegment;
            try
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    Polyline pl = tr.GetObject(poly.ObjectId, OpenMode.ForRead) as Polyline;
                    if (pl == null)
                    {
                        ed.WriteMessage("\n无法获取所选的多段线对象。");
                        return points; // 返回空列表
                    }
                    var point = GetPerpendicularPoint(lineSegment3D, p);
                    // 调用方法获取三个点
                    points = GetPointsAlongPolyline(pl, point, 100, 17.5);
                    tr.Commit();
                }
                return points;
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage("\n出错：" + ex.Message);
                return points; // 返回空列表
            }
        }
        public static List<Point3d> GetReinPointsSix()
        {
            var a = GetReinPoints();
            if (a == null)
            {
                return null;
            }
            var p = a[1];
            var b = GetReinPointsByPoint(p);
            if (b == null)
            {
                return null;
            }
            var result = new List<Point3d>(a);
            result.AddRange(b);
            return result;
        }
        public static Point3d GetPerpendicularPoint(LineSegment3d segment, Point3d point)
        {
            // 获取线段的起点和终点
            Point3d start = segment.StartPoint;
            Point3d end = segment.EndPoint;
            // 计算线段的方向向量
            Vector3d segmentVector = end - start;
            // 计算点到线段起点的向量
            Vector3d pointVector = point - start;
            // 计算向量的投影比例 t = (pointVector ⋅ segmentVector) / (segmentVector ⋅ segmentVector)
            double segmentLengthSquared = segmentVector.DotProduct(segmentVector);
            if (segmentLengthSquared == 0) // 避免除以零的情况
                return start; // 线段退化为一个点，返回起点
            double t = pointVector.DotProduct(segmentVector) / segmentLengthSquared;
            // 限制 t 的范围 [0, 1]，确保交点在线段范围内
            t = Math.Max(0, Math.Min(1, t));
            // 计算垂线交点的坐标
            Point3d perpendicularPoint = start + t * segmentVector;
            return perpendicularPoint;
        }
        private static List<Point3d> GetPointsAlongPolyline(Polyline pl, Point3d selPt, double offset, double dv)
        {
            List<Point3d> points = new List<Point3d>();
            // 找到多段线上距离选择点最近的点 a
            Point3d a = pl.GetClosestPointTo(selPt, false);
            // 获取点 a 在多段线上的累计距离
            double distA = pl.GetDistAtPoint(a);
            // 计算距离 a -offset 和 a +offset 的累计距离
            double distMinusOffset = distA - offset;
            double distPlusOffset = distA + offset;
            // 确保累计距离在有效范围内
            if (distMinusOffset < 0)
                distMinusOffset = 0;
            double totalLength = pl.Length;
            if (distPlusOffset > totalLength)
                distPlusOffset = totalLength;
            // 获取对应累计距离的点
            Point3d ptMinusOffset = pl.GetPointAtDist(distMinusOffset);
            Point3d ptPlusOffset = pl.GetPointAtDist(distPlusOffset);
            // 获取点的切线方向并计算法线
            Vector3d tangentMinus = pl.GetFirstDerivative(ptMinusOffset).GetNormal();
            Vector3d normalMinus = tangentMinus.RotateBy(Math.PI / 2, Vector3d.ZAxis).GetNormal();
            Vector3d tangentPlus = pl.GetFirstDerivative(ptPlusOffset).GetNormal();
            Vector3d normalPlus = tangentPlus.RotateBy(Math.PI / 2, Vector3d.ZAxis).GetNormal();
            // 按法线方向偏移点
            ptMinusOffset = ptMinusOffset + normalMinus * dv;
            ptPlusOffset = ptPlusOffset + normalPlus * dv;
            // 将三个点添加到列表中
            points.Add(ptMinusOffset);
            points.Add(a);
            points.Add(ptPlusOffset);
            return points;
        }
    }
}
