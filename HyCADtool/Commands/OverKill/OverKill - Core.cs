using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
[assembly: CommandClass(typeof(HyCADTool.Command.HyCommand))]
namespace HyCADTool.Command
{
    public static partial class HyCommand
    {
        #region Drawing Operations
        // 获取用户选择的线段
        private static PromptSelectionResult GetLineSelection(Editor ed)
        {
            PromptSelectionOptions selOpts = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择要处理的线段: "
            };
            TypedValue[] filter = { new TypedValue(0, "LINE") }; // 只选择LINE类型对象
            return ed.GetSelection(selOpts, new SelectionFilter(filter));
        }
        // 获取模型空间
        private static BlockTableRecord GetModelSpace(Transaction tr, Database db)
        {
            BlockTable bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            return tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
        }
        // 更新图纸：删除旧线段，添加新线段
        //private static void UpdateDrawing(Transaction tr, BlockTableRecord btr,
        //    List<ObjectId> lineIds, List<Line> newLines)
        //{
        //    foreach (ObjectId id in lineIds)
        //    {
        //        Entity ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
        //        ent.Erase(); // 删除原始线段
        //    }
        //    foreach (Line newLine in newLines)
        //    {
        //        btr.AppendEntity(newLine); // 添加新线段到模型空间
        //        tr.AddNewlyCreatedDBObject(newLine, true);
        //    }
        //}
        private static void UpdateDrawing(Transaction tr, BlockTableRecord btr,
    List<ObjectId> oldLineIds, List<Line> newLines)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            // 删除旧线段（如果存在）
            if (oldLineIds != null && oldLineIds.Count > 0)
            {
                foreach (ObjectId id in oldLineIds)
                {
                    try
                    {
                        if (!id.IsValid || id.IsNull || id.IsErased)
                        {
                            ed.WriteMessage($"\nObjectId {id} 无效或已被删除，跳过。");
                            continue;
                        }
                        Entity ent = tr.GetObject(id, OpenMode.ForWrite) as Entity;
                        if (ent != null && !ent.IsErased)
                        {
                            ent.Erase();
                        }
                        else
                        {
                            ed.WriteMessage($"\nObjectId {id} 不是 Entity 类型或已被删除。");
                        }
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\n删除旧线段 {id} 失败: {ex.Message}");
                    }
                }
            }
            // 添加新线段（仅添加未入库的对象）
            foreach (Line newLine in newLines)
            {
                if (newLine.IsNewObject) // 检查是否为新对象
                {
                    try
                    {
                        btr.AppendEntity(newLine);
                        tr.AddNewlyCreatedDBObject(newLine, true);
                    }
                    catch (System.Exception ex)
                    {
                        ed.WriteMessage($"\n添加新线段失败: {ex.Message}");
                    }
                }
            }
        }
        #endregion
        #region Line Processing
        // 处理重叠线段
        private static List<Line> ProcessOverlappingLines(List<Line> lines, Tolerance tol)
        {
            List<Line> resultLines = new List<Line>();
            HashSet<int> processed = new HashSet<int>();
            for (int i = 0; i < lines.Count; i++)
            {
                if (processed.Contains(i)) continue;
                Line current = lines[i];
                processed.Add(i);
                for (int j = i + 1; j < lines.Count; j++)
                {
                    if (processed.Contains(j)) continue;
                    if (GeometryUtils.CheckOverlap(current, lines[j], tol, DEFAULT_TOLERANCE, lines,
                        out Point3d newStart, out Point3d newEnd))
                    {
                        current = new Line(newStart, newEnd);
                        processed.Add(j);
                    }
                }
                // 将合并后的线段添加到数据库               
                resultLines.Add(current);
            }
            return resultLines;
        }
        #endregion
        // 处理端点连接
        // 查找独立端点的直线
        private static List<(Line line, bool isStart)> FindIndependentEndpoints(List<Line> lines, Tolerance tol, double d)
        {
            List<(Line line, bool isStart)> independentLines = new List<(Line line, bool isStart)>();
            // 遍历每条直线
            for (int i = 0; i < lines.Count; i++)
            {
                Line currentLine = lines[i];
                Point3d start = currentLine.StartPoint;
                Point3d end = currentLine.EndPoint;
                // 检查起点是否独立
                bool startIsIndependent = true;
                for (int j = 0; j < lines.Count; j++)
                {
                    if (j == i) continue; // 跳过当前直线
                    Point3d otherStart = lines[j].StartPoint;
                    Point3d otherEnd = lines[j].EndPoint;
                    if (start.IsEqualTo(otherStart, tol) || start.IsEqualTo(otherEnd, tol))
                    {
                        startIsIndependent = false;
                        break;
                    }
                }
                // 检查终点是否独立
                bool endIsIndependent = true;
                for (int j = 0; j < lines.Count; j++)
                {
                    if (j == i) continue; // 跳过当前直线
                    Point3d otherStart = lines[j].StartPoint;
                    Point3d otherEnd = lines[j].EndPoint;
                    if (end.IsEqualTo(otherStart, tol) || end.IsEqualTo(otherEnd, tol))
                    {
                        endIsIndependent = false;
                        break;
                    }
                }
                // 如果至少有一个端点是独立的，则添加该直线
                if (startIsIndependent || endIsIndependent)
                {
                    // 如果两个端点都独立，添加两次
                    if (startIsIndependent && endIsIndependent)
                    {
                        independentLines.Add((currentLine, true));  // 添加起点
                        independentLines.Add((currentLine, false)); // 添加终点
                    }
                    // 如果只有起点独立
                    else if (startIsIndependent)
                    {
                        independentLines.Add((currentLine, true));
                    }
                    // 如果只有终点独立
                    else if (endIsIndependent)
                    {
                        independentLines.Add((currentLine, false));
                    }
                }
            }
            return independentLines;
        }
        //修改后的 ProcessEndpointConnections 方法
        private static List<Line> ProcessEndpointConnections(List<Line> lines, Tolerance tol, double d, Transaction tr, BlockTableRecord btr)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            List<Line> resultLines = new List<Line>(lines);
            // 获取独立端点的直线
            List<(Line line, bool isStart)> independentEndpoints = FindIndependentEndpoints(lines, tol, d);
            foreach (Line originalLine in lines)
            {
                int lineIndex = lines.IndexOf(originalLine);
                var lineEndpoints = independentEndpoints.Where(e => e.line == originalLine).ToList();
                if (lineEndpoints.Count == 0) continue;
                foreach (var (line, isStart) in lineEndpoints)
                {
                    Point3d freeEnd = isStart ? line.StartPoint : line.EndPoint;
                    Point3d oppositeEnd = isStart ? line.EndPoint : line.StartPoint;
                    Vector3d direction = (freeEnd - oppositeEnd).GetNormal();
                    Point3d extendedPoint = freeEnd + direction * d;
                    Line extendedLine = new Line(freeEnd, extendedPoint);
                    bool hasIntersection = false;
                    Point3d intersectionPoint = Point3d.Origin;
                    for (int i = 0; i < resultLines.Count; i++)
                    {
                        if (i == lineIndex) continue;
                        Line otherLine = resultLines[i];
                        Point3dCollection intersectionPoints = new Point3dCollection();
                        extendedLine.IntersectWith(
                            otherLine,
                            Intersect.OnBothOperands,
                            intersectionPoints,
                            IntPtr.Zero,
                            IntPtr.Zero
                        );
                        if (intersectionPoints.Count > 0)
                        {
                            hasIntersection = true;
                            intersectionPoint = intersectionPoints[0];
                            break;
                        }
                    }
                    if (hasIntersection)
                    {
                        Line updatedLine = new Line(
                            isStart ? oppositeEnd : intersectionPoint,
                            isStart ? intersectionPoint : oppositeEnd
                        );
                        resultLines[lineIndex] = updatedLine;
                    }
                    else
                    {
                        CreateWarningRectangle(tr, btr, freeEnd, direction, 20 * d, "00_HY_警告_红色", 1);
                        ed.WriteMessage($"\n无交点，绘制警告矩形于 {freeEnd}");
                    }
                }
            }
            return resultLines;
        }
        //自定义点比较器（保持不变）
        private class Point3dComparer : IEqualityComparer<Point3d>
        {
            private readonly Tolerance _tolerance;
            public Point3dComparer(Tolerance tolerance)
            {
                _tolerance = tolerance;
            }
            public bool Equals(Point3d p1, Point3d p2)
            {
                return p1.IsEqualTo(p2, _tolerance);
            }
            public int GetHashCode(Point3d p)
            {
                return p.GetHashCode();
            }
        }
        // 计算两线段的交点（保持不变）
        private static bool GetIntersection(Point3d p1, Point3d p2, Point3d p3, Point3d p4, Tolerance tol, out Point3d intersection)
        {
            intersection = Point3d.Origin;
            Vector2d v1 = new Vector2d(p2.X - p1.X, p2.Y - p1.Y);
            Vector2d v2 = new Vector2d(p4.X - p3.X, p4.Y - p3.Y);
            Vector2d w = new Vector2d(p1.X - p3.X, p1.Y - p3.Y);
            double denom = v1.X * v2.Y - v1.Y * v2.X;
            if (Math.Abs(denom) < tol.EqualVector) return false;
            double s = (v2.Y * w.X - v2.X * w.Y) / denom;
            double t = (v1.X * w.Y - v1.Y * w.X) / denom;
            if (s >= 0 && s <= 1 && t >= 0 && t <= 1)
            {
                intersection = new Point3d(p1.X + s * v1.X, p1.Y + s * v1.Y, 0);
                return true;
            }
            return false;
        }
        // 创建警告矩形（保持不变）
        private static void CreateWarningRectangle(Transaction tr, BlockTableRecord btr, Point3d center,
            Vector3d direction, double length, string layerName, short color)
        {
            double width = length / 2;
            Vector3d perpendicular = direction.RotateBy(Math.PI / 2, Vector3d.ZAxis);
            Point3d p1 = center + perpendicular * (width / 2) - direction * (length / 2);
            Point3d p2 = center + perpendicular * (width / 2) + direction * (length / 2);
            Point3d p3 = center - perpendicular * (width / 2) + direction * (length / 2);
            Point3d p4 = center - perpendicular * (width / 2) - direction * (length / 2);
            Polyline rect = new Polyline();
            rect.AddVertexAt(0, new Point2d(p1.X, p1.Y), 0, 0, 0);
            rect.AddVertexAt(1, new Point2d(p2.X, p2.Y), 0, 0, 0);
            rect.AddVertexAt(2, new Point2d(p3.X, p3.Y), 0, 0, 0);
            rect.AddVertexAt(3, new Point2d(p4.X, p4.Y), 0, 0, 0);
            rect.Closed = true;
            LayerTable lt = tr.GetObject(btr.Database.LayerTableId, OpenMode.ForWrite) as LayerTable;
            if (!lt.Has(layerName))
            {
                LayerTableRecord ltr = new LayerTableRecord
                {
                    Name = layerName,
                    Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(Autodesk.AutoCAD.Colors.ColorMethod.ByAci, color)
                };
                lt.UpgradeOpen();
                lt.Add(ltr);
                tr.AddNewlyCreatedDBObject(ltr, true);
            }
            rect.Layer = layerName;
            btr.AppendEntity(rect);
            tr.AddNewlyCreatedDBObject(rect, true);
        }
    }
}