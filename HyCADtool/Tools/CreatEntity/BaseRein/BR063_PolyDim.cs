using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Tools;
using System.Collections.Generic;
using System.Linq;
using Line = Autodesk.AutoCAD.DatabaseServices.Line;
using Polyline = Autodesk.AutoCAD.DatabaseServices.Polyline;
[assembly: CommandClass(typeof(HyCADTool.BaseRein))]
namespace HyCADTool
{
    public static partial class BaseRein
    {
        public static void DeletePolylinesInCad(Dictionary<Polyline, Dictionary<IntersectionType, Point3d>> dic)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    foreach (var kvp in dic)
                    {
                        //Polyline polyline = kvp.Key;
                        //polyline.UpgradeOpen();
                        //polyline.Erase();
                        var polyline = tr.GetObject(kvp.Key.ObjectId, OpenMode.ForWrite) as Polyline;
                        if (polyline != null)
                        {
                            polyline.Erase();
                        }
                    }
                    tr.Commit();
                }
            }
        }
        private static Line CreateExtendedLine(Point3d startPoint, double extendDistance, Vector3d direction)
        {
            return new Line(startPoint, startPoint + direction * extendDistance);
        }
        //标注 右侧
        public static void AnnotatePolylineIntersectionsLeftRight(
         Dictionary<Polyline, Dictionary<IntersectionType, Point3d>> intersections,
            double scale,
            double dimDistance,
                IntersectionsDirection direction)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    foreach (var kvp in intersections)
                    {
                        Polyline polyline = kvp.Key;
                        Dictionary<IntersectionType, Point3d> points = kvp.Value;
                        // 检查 InsideLowerIntersection 和 InsideUpperIntersection 是否有值
                        if (points.ContainsKey(IntersectionType.InsideLowerIntersection) || points.ContainsKey(IntersectionType.InsideUpperIntersection))
                        {
                            // 删除延长线交点
                            points.Remove(IntersectionType.PositiveExtensionIntersection);
                            points.Remove(IntersectionType.NegativeExtensionIntersection);
                        }
                        // 获取交点列表并去除空点和重复点
                        List<Point3d> intersectionPoints = points.Values.Distinct().Where(pt => pt != null).ToList();
                        // 根据标注方向进行排序
                        if (direction == IntersectionsDirection.LeftRight)
                        {
                            // 按 y 坐标排序
                            intersectionPoints.Sort((p1, p2) => p1.Y.CompareTo(p2.Y));
                        }
                        else
                        {
                            // 按 x 坐标排序
                            intersectionPoints.Sort((p1, p2) => p1.X.CompareTo(p2.X));
                        }
                        // 标注点之间的距离
                        for (int i = 0; i < intersectionPoints.Count - 1; i++)
                        {
                            Point3d startPt = intersectionPoints[i];
                            Point3d endPt = intersectionPoints[i + 1];
                            // 根据标注方向创建尺寸标注
                            RotatedDimension dim;
                            if (direction == IntersectionsDirection.LeftRight)
                            {
                                dim = Et.GetDimByTwoPoints(startPt, endPt, dimDistance * scale, Et.DimensionFor.ForRight, false);
                            }
                            else
                            {
                                dim = Et.GetDimByTwoPoints(startPt, endPt, dimDistance * scale, Et.DimensionFor.ForDown, false);
                            }
                            btr.AppendEntity(dim);
                            tr.AddNewlyCreatedDBObject(dim, true);
                        }
                    }
                    tr.Commit();
                }
            }
            ed.WriteMessage("\n交点距离标注已完成。\n");
        }
        //标注 下侧
        public static void AnnotatePolylineIntersectionsUpDown(
        Dictionary<Polyline, Dictionary<IntersectionType, Point3d>> intersections,
           double scale,
           double dimDistance,
               IntersectionsDirection direction)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                    BlockTableRecord btr = tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                    foreach (var kvp in intersections)
                    {
                        Polyline polyline = kvp.Key;
                        Dictionary<IntersectionType, Point3d> points = kvp.Value;
                        // 检查 InsideLowerIntersection 和 InsideUpperIntersection 是否有值
                        if (points.ContainsKey(IntersectionType.InsideLowerIntersection) || points.ContainsKey(IntersectionType.InsideUpperIntersection))
                        {
                            // 删除延长线交点
                            points.Remove(IntersectionType.PositiveExtensionIntersection);
                            points.Remove(IntersectionType.NegativeExtensionIntersection);
                        }
                        // 获取交点列表并去除空点和重复点
                        List<Point3d> intersectionPoints = points.Values.Distinct().Where(pt => pt != null).ToList();
                        // 按 x 坐标排序
                        intersectionPoints.Sort((p1, p2) => p1.X.CompareTo(p2.X));
                        // 标注点之间的距离
                        for (int i = 0; i < intersectionPoints.Count - 1; i++)
                        {
                            Point3d startPt = intersectionPoints[i];
                            Point3d endPt = intersectionPoints[i + 1];
                            // 根据标注方向创建尺寸标注
                            RotatedDimension dim;
                            dim = Et.GetDimByTwoPoints(startPt, endPt, dimDistance * scale, Et.DimensionFor.ForDown, false);
                            btr.AppendEntity(dim);
                            tr.AddNewlyCreatedDBObject(dim, true);
                        }
                    }
                    tr.Commit();
                }
            }
            ed.WriteMessage("\n交点距离标注已完成。\n");
        }
    }
}
