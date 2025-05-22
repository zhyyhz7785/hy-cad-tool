using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Tools;
using System.Collections.Generic;
[assembly: CommandClass(typeof(HyCADTool.BaseRein))]
namespace HyCADTool
{
    public static partial class BaseRein
    {
        // 主方法：根据分组结果创建优化后的包围多边形
        public static Dictionary<Polyline, List<DBText>> CreateOptimizedBoundingPolygonFromPolygons(List<Dictionary<DBText, ObjectId>> groups, string layerName)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Dictionary<Polyline, List<DBText>> resultDict = new Dictionary<Polyline, List<DBText>>();
            using (var documentLock = doc.LockDocument())
            {
                using (Transaction trans = db.TransactionManager.StartTransaction())
                {
                    // 创建图层
                    Et.SetCurrentLayer(layerName);
                    foreach (var group in groups)
                    {
                        // 获取每组小多边形的最小和最大坐标
                        double minX = double.MaxValue;
                        double minY = double.MaxValue;
                        double maxX = double.MinValue;
                        double maxY = double.MinValue;
                        List<DBText> texts = new List<DBText>();
                        foreach (var kvp in group)
                        {
                            Polyline polyline = trans.GetObject(kvp.Value, OpenMode.ForRead) as Polyline;
                            if (polyline != null)
                            {
                                // 获取第一个点的最小x值和最小y值
                                Point3d firstPoint = polyline.GetPoint3dAt(0);
                                if (firstPoint.X < minX) minX = firstPoint.X;
                                if (firstPoint.Y < minY) minY = firstPoint.Y;
                                // 获取第三个点的最大x值和最大y值
                                Point3d thirdPoint = polyline.GetPoint3dAt(2);
                                if (thirdPoint.X > maxX) maxX = thirdPoint.X;
                                if (thirdPoint.Y > maxY) maxY = thirdPoint.Y;
                                // 添加配筋文字
                                texts.Add(kvp.Key);
                            }
                        }
                        // 创建新的多边形
                        Polyline newPolyline = new Polyline();
                        newPolyline.AddVertexAt(0, new Point2d(minX, minY), 0, 0, 0);
                        newPolyline.AddVertexAt(1, new Point2d(maxX, minY), 0, 0, 0);
                        newPolyline.AddVertexAt(2, new Point2d(maxX, maxY), 0, 0, 0);
                        newPolyline.AddVertexAt(3, new Point2d(minX, maxY), 0, 0, 0);
                        newPolyline.Closed = true;
                        newPolyline.Layer = layerName;
                        // 将新的多边形添加到模型空间
                        BlockTable bt = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForRead);
                        BlockTableRecord btr = (BlockTableRecord)trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);
                        btr.AppendEntity(newPolyline);
                        trans.AddNewlyCreatedDBObject(newPolyline, true);
                        // 将新的多边形及其相关的配筋文字添加到结果字典中
                        resultDict.Add(newPolyline, texts);
                    }
                    trans.Commit();
                }
            }
            return resultDict;
        }
    }
}
