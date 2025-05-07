using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Colors;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
[assembly: CommandClass(typeof(HyCADTool.Command.DrawBaseReinforcement))]
namespace HyCADTool.Command
{
    public static partial class DrawBaseReinforcement
    {
        public static void ReinforcementStepB(Dictionary<Polyline, (int Count, double Min, double Max, double Average, double StdDev)> keyValuePairs)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            Database db = doc.Database;
            Dictionary<ObjectId, (int Count, double Min, double Max, double Average, double StdDev)> updatedKeyValuePairs = new Dictionary<ObjectId, (int Count, double Min, double Max, double Average, double StdDev)>();
            List<ObjectId> toBeErased = new List<ObjectId>();
            using (DocumentLock docLock = doc.LockDocument()) // 添加文档锁
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    foreach (var kvp in keyValuePairs)
                    {
                        var polyline = kvp.Key;
                        var stats = kvp.Value;
                        double maxArea = stats.Max * 100;
                        double singleRebarArea = Math.PI * Math.Pow(RebarDiameter / 2, 2);
                        double customerArea = 1000 / RebarSpacing * singleRebarArea;
                        double additionalArea = 0;
                        double additionalDiameter = MinAdditionalDiameter;
                        double additionalSpacing = RebarSpacing;
                        foreach (var dia in AllowedDiameters)
                        {
                            additionalDiameter = dia;
                            double additionalSingleRebarArea = Math.PI * Math.Pow(additionalDiameter / 2, 2);
                            additionalArea = 1000 / additionalSpacing * additionalSingleRebarArea;
                            if (additionalArea + customerArea > maxArea)
                            {
                                break;
                            }
                        }
                        Polyline newPolyline = (Polyline)polyline.Clone();
                        if (Direction == "x")
                        {
                            DrawRebarsInXDirection(tr, newPolyline, additionalDiameter, additionalSpacing, Scale, AnchorFactor);
                            Point3d point0 = newPolyline.GetPoint3dAt(0);
                            Point3d point3 = newPolyline.GetPoint3dAt(3);
                            Point3d point1 = newPolyline.GetPoint3dAt(1);
                            Point3d point2 = newPolyline.GetPoint3dAt(2);
                            newPolyline.SetPointAt(0, new Point2d(point0.X - AnchorFactor * additionalDiameter, point0.Y));
                            newPolyline.SetPointAt(3, new Point2d(point3.X - AnchorFactor * additionalDiameter, point3.Y));
                            newPolyline.SetPointAt(1, new Point2d(point1.X + AnchorFactor * additionalDiameter, point1.Y));
                            newPolyline.SetPointAt(2, new Point2d(point2.X + AnchorFactor * additionalDiameter, point2.Y));
                            newPolyline.ToSpace();
                            updatedKeyValuePairs.Add(newPolyline.ObjectId, stats);
                            toBeErased.Add(polyline.ObjectId);
                        }
                        else if (Direction == "y")
                        {
                            DrawRebarsInYDirection(tr, polyline, additionalDiameter, additionalSpacing, Scale, AnchorFactor);
                            updatedKeyValuePairs.Add(polyline.ObjectId, stats);
                        }
                    }
                    AddTableToDrawing(tr, updatedKeyValuePairs);
                    tr.Commit();
                }
            }
            using (DocumentLock docLock = doc.LockDocument()) // 添加文档锁
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    foreach (var polylineId in toBeErased)
                    {
                        var polyline = tr.GetObject(polylineId, OpenMode.ForWrite) as Polyline;
                        if (polyline != null)
                        {
                            polyline.Erase();
                        }
                    }
                    tr.Commit();
                }
            }
            // 用修改后的字典替换原来的字典
            keyValuePairs.Clear();
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                foreach (var kvp in updatedKeyValuePairs)
                {
                    var polyline = tr.GetObject(kvp.Key, OpenMode.ForRead) as Polyline;
                    keyValuePairs.Add(polyline, kvp.Value);
                }
            }
        }
        private static void AddTableToDrawing(Transaction tr, Dictionary<ObjectId, (int Count, double Min, double Max, double Average, double StdDev)> keyValuePairs)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            // 表格位置
            Point3d tablePosition = new Point3d(0, 0, 0);
            // 创建表格对象
            using (Table table = new Table())
            {
                table.TableStyle = db.Tablestyle;
                table.SetSize(keyValuePairs.Count + 1, 6); // 行数 = 数据数 + 表头, 列数 = 6 (根据需要调整)
                // 设置表头
                table.Cells[0, 0].TextString = "Polyline ID";
                table.Cells[0, 1].TextString = "Count";
                table.Cells[0, 2].TextString = "Min";
                table.Cells[0, 3].TextString = "Max";
                table.Cells[0, 4].TextString = "Average";
                table.Cells[0, 5].TextString = "StdDev";
                // 填充数据
                int rowIndex = 1;
                foreach (var kvp in keyValuePairs)
                {
                    table.Cells[rowIndex, 0].TextString = kvp.Key.ToString();
                    table.Cells[rowIndex, 1].TextString = kvp.Value.Count.ToString();
                    table.Cells[rowIndex, 2].TextString = kvp.Value.Min.ToString("F2");
                    table.Cells[rowIndex, 3].TextString = kvp.Value.Max.ToString("F2");
                    table.Cells[rowIndex, 4].TextString = kvp.Value.Average.ToString("F2");
                    table.Cells[rowIndex, 5].TextString = kvp.Value.StdDev.ToString("F2");
                    rowIndex++;
                }
                // 设置表格位置和大小
                table.Position = tablePosition;
                table.SetRowHeight(3);
                table.SetColumnWidth(15);
                // 将表格添加到图形中
                BlockTableRecord btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
                btr.AppendEntity(table);
                tr.AddNewlyCreatedDBObject(table, true);
            }
        }
        private static void DrawRebarsInXDirection(Transaction tr, Polyline polyline, double diameter, double spacing, double scale, double anchorFactor)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            // 获取polyline起点和终点
            var startPoint = polyline.GetPoint3dAt(0);
            var endPoint = polyline.GetPoint3dAt(1);
            var point3 = polyline.GetPoint3dAt(2);
            // 计算y向中心位置
            double yCenter = (startPoint.Y + point3.Y) / 2;
            // 创建钢筋Polyline
            Polyline rebarPolyline = new Polyline();
            rebarPolyline.AddVertexAt(0, new Point2d(startPoint.X - AnchorFactor * diameter, yCenter - HookLength * Scale), 0, PolylineWidth * Scale, PolylineWidth * Scale); // 设置起始宽度和结束宽度
            rebarPolyline.AddVertexAt(1, new Point2d(startPoint.X - AnchorFactor * diameter, yCenter), 0, PolylineWidth * Scale, PolylineWidth * Scale);
            rebarPolyline.AddVertexAt(2, new Point2d(endPoint.X + AnchorFactor * diameter, yCenter), 0, PolylineWidth * Scale, PolylineWidth * Scale);
            rebarPolyline.AddVertexAt(3, new Point2d(endPoint.X + AnchorFactor * diameter, yCenter - HookLength * Scale), 0, PolylineWidth * Scale, PolylineWidth * Scale);
            rebarPolyline.Layer = "00_hy_筏板附加配筋x";
            // 设置颜色
            rebarPolyline.Color = Color.FromColorIndex(ColorMethod.ByAci, 1); // 红色            
                                                                              // 添加钢筋Polyline到图形
            var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            btr.AppendEntity(rebarPolyline);
            tr.AddNewlyCreatedDBObject(rebarPolyline, true);
            // 创建标注
            CreateRebarLabel(tr, rebarPolyline, diameter, spacing, scale);
        }
        private static void DrawRebarsInYDirection(Transaction tr, Polyline polyline, double diameter, double spacing, double scale, double anchorFactor)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            // 计算锚固长度
            double anchorLength = diameter * anchorFactor;
            // 获取Polyline的第二条边的长度
            double length = polyline.GetPoint3dAt(2).DistanceTo(polyline.GetPoint3dAt(1));
            // 获取形心位置
            var centroid = polyline.GeometricExtents.MinPoint + (polyline.GeometricExtents.MaxPoint - polyline.GeometricExtents.MinPoint) / 2;
            // 获取polyline起点和终点
            var startPoint = polyline.GetPoint3dAt(1);
            var endPoint = polyline.GetPoint3dAt(2);
            // 创建钢筋Polyline
            Polyline rebarPolyline = new Polyline();
            rebarPolyline.AddVertexAt(0, new Point2d(startPoint.X - AnchorFactor * Scale, startPoint.Y - HookLength * Scale), 0, PolylineWidth * Scale, PolylineWidth * Scale); // 设置起始宽度和结束宽度
            rebarPolyline.AddVertexAt(1, new Point2d(startPoint.X - AnchorFactor * Scale, startPoint.Y), 0, PolylineWidth * Scale, PolylineWidth * Scale);
            rebarPolyline.AddVertexAt(2, new Point2d(endPoint.X + AnchorFactor * Scale, endPoint.Y), 0, PolylineWidth * Scale, PolylineWidth * Scale);
            rebarPolyline.AddVertexAt(3, new Point2d(endPoint.X + AnchorFactor * Scale, endPoint.Y - HookLength * Scale), 0, PolylineWidth * Scale, PolylineWidth * Scale);
            rebarPolyline.Layer = "00_hy_筏板附加配筋y";
            // 设置颜色
            rebarPolyline.Color = Color.FromColorIndex(ColorMethod.ByAci, 1); // 红色
            // 添加钢筋Polyline到图形
            var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            btr.AppendEntity(rebarPolyline);
            tr.AddNewlyCreatedDBObject(rebarPolyline, true);
            // 创建标注
            CreateRebarLabel(tr, rebarPolyline, diameter, spacing, scale);
        }
        private static void CreateRebarLabel(Transaction tr, Polyline rebarPolyline, double diameter, double spacing, double scale)
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            // 获取钢筋中心点
            var midpoint = rebarPolyline.GetPoint3dAt(0).GetMidPoint(rebarPolyline.GetPoint3dAt(2));
            // 创建DBText对象
            DBText rebarLabel = new DBText
            {
                Position = new Point3d(midpoint.X, midpoint.Y + TextToLineDistance * scale, 0),
                Height = 3 * scale,
                WidthFactor = 0.7,
                TextString = $"\\u+e532{diameter}@{spacing}",
                Layer = "00_hy_筏板附加配筋文字",
                Color = Color.FromColorIndex(ColorMethod.ByAci, 7) // 白色
            };
            // 添加DBText到图形
            var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);
            btr.AppendEntity(rebarLabel);
            tr.AddNewlyCreatedDBObject(rebarLabel, true);
        }
        public static Point3d GetMidPoint(this Point3d pt1, Point3d pt2)
        {
            return new Point3d((pt1.X + pt2.X) / 2, (pt1.Y + pt2.Y) / 2, (pt1.Z + pt2.Z) / 2);
        }
    }
}
