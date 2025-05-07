using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
[assembly: CommandClass(typeof(HyCADTool.Command.HyCommand))]
namespace HyCADTool.Command
{
    public static partial class HyCommand
    {
        // CommandMethod 特性表明这是一个 AutoCAD 命令        
        [CommandMethod("HYBL")]
        public static void BreakLinesAtIntersections()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            // 提示用户选择直线
            PromptSelectionOptions selOpts = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择要处理的直线："
            };
            // 设置过滤器，只选择直线对象
            SelectionFilter filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "LINE")
            });
            PromptSelectionResult selRes = ed.GetSelection(selOpts, filter);
            if (selRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n未选择任何直线。");
                return;
            }
            // 获取选择的对象集合
            ObjectId[] selectedLines = selRes.Value.GetObjectIds();
            // 开始事务
            using (Transaction trans = doc.Database.TransactionManager.StartTransaction())
            {
                // 存储所有的直线对象
                Dictionary<ObjectId, Line> lineObjects = new Dictionary<ObjectId, Line>();
                // 存储每条直线的交点列表
                Dictionary<ObjectId, List<Point3d>> lineIntersections = new Dictionary<ObjectId, List<Point3d>>();
                // 初始化
                foreach (ObjectId lineId in selectedLines)
                {
                    Line line = trans.GetObject(lineId, OpenMode.ForRead) as Line;

                    if (line != null)
                    {
                        lineObjects[lineId] = line;
                        lineIntersections[lineId] = new List<Point3d>();
                    }
                }
                // 计算所有直线对之间的交点
                var lineIds = lineObjects.Keys.ToList();
                for (int i = 0; i < lineIds.Count; i++)
                {
                    for (int j = i + 1; j < lineIds.Count; j++)
                    {
                        Line line1 = lineObjects[lineIds[i]];
                        Line line2 = lineObjects[lineIds[j]];
                        // 计算交点
                        Point3dCollection intersectionPoints = new Point3dCollection();
                        line1.IntersectWith(line2, Intersect.OnBothOperands, intersectionPoints, IntPtr.Zero, IntPtr.Zero);
                        if (intersectionPoints.Count > 0)
                        {
                            Point3d intersectionPoint = intersectionPoints[0];
                            // 检查交点是否在两条直线的范围内
                            if (IsPointOnLineSegment(line1, intersectionPoint))
                            {
                                lineIntersections[lineIds[i]].Add(intersectionPoint);
                            }
                            if (IsPointOnLineSegment(line2, intersectionPoint))
                            {
                                lineIntersections[lineIds[j]].Add(intersectionPoint);
                            }
                        }
                    }
                }
                // 存储新的直线段
                List<Line> newLines = new List<Line>();
                // 分割直线
                foreach (var item in lineIntersections)
                {
                    ObjectId lineId = item.Key;
                    List<Point3d> intersectionPts = item.Value;
                    Line line = lineObjects[lineId];
                    if (intersectionPts.Count > 0)
                    {
                        // 将交点按在线上的距离排序
                        intersectionPts = intersectionPts
                            .Distinct()
                            .OrderBy(pt => line.GetDistAtPoint(pt))
                            .ToList();
                        // 添加起点和终点
                        List<Point3d> points = new List<Point3d>();
                        points.Add(line.StartPoint);
                        points.AddRange(intersectionPts);
                        points.Add(line.EndPoint);
                        // 创建新的直线段
                        for (int i = 0; i < points.Count - 1; i++)
                        {
                            if (points[i].DistanceTo(points[i + 1]) > Tolerance.Global.EqualPoint)
                            {
                                Line newLine = new Line(points[i], points[i + 1]);
                                newLines.Add(newLine);
                            }
                        }
                    }
                    else
                    {
                        // 如果没有交点，直接添加原直线的副本
                        Line newLine = new Line(line.StartPoint, line.EndPoint);
                        newLines.Add(newLine);
                    }
                }
                // 获取模型空间
                BlockTable bt = trans.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                // 将新的直线段添加到模型空间a
                foreach (Line newLine in newLines)
                {
                    btr.AppendEntity(newLine);
                    trans.AddNewlyCreatedDBObject(newLine, true);
                }
                // 删除原始直线
                foreach (ObjectId id in selectedLines)
                {
                    Entity ent = trans.GetObject(id, OpenMode.ForWrite) as Entity;
                    ent.Erase();
                }
                // 提交事务
                trans.Commit();
            }
        }
        // 辅助方法：检查点是否在直线段上
        private static bool IsPointOnLineSegment(Line line, Point3d point)
        {
            // 检查点是否在线段的范围内
            double totalLength = line.Length;
            double distToStart = line.StartPoint.DistanceTo(point);
            double distToEnd = line.EndPoint.DistanceTo(point);
            // 考虑浮点数精度误差
            return Math.Abs((distToStart + distToEnd) - totalLength) <= Tolerance.Global.EqualPoint;
        }
    }
}
