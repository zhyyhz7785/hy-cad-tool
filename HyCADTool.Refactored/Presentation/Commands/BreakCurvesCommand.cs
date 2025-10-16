using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;

[assembly: CommandClass(typeof(HyCADTool.Refactored.Presentation.Commands.BreakCurvesCommand))]

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// BreakCurves 命令 - 在交点处打断曲线
    /// 功能：
    /// 1. 选择曲线（LINE, ARC, CIRCLE, LWPOLYLINE, POLYLINE, SPLINE）
    /// 2. Polyline 先分解为 Line 段
    /// 3. 计算所有交点
    /// 4. 在交点处分割曲线
    /// 5. 删除原曲线，创建新曲线段
    /// </summary>
    public class BreakCurvesCommand
    {
        [CommandMethod("HYBC")]
        public void Execute()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;

            // 提示用户选择曲线
            PromptSelectionOptions selOpts = new PromptSelectionOptions
            {
                MessageForAdding = "\n请选择要处理的曲线："
            };

            // 设置过滤器，选择曲线对象
            SelectionFilter filter = new SelectionFilter(new[]
            {
                new TypedValue((int)DxfCode.Start, "LINE,ARC,CIRCLE,LWPOLYLINE,POLYLINE,SPLINE")
            });

            PromptSelectionResult selRes = ed.GetSelection(selOpts, filter);
            if (selRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n未选择任何曲线。");
                    return;
                }

            // 获取选择的对象集合
            ObjectId[] selectedObjects = selRes.Value.GetObjectIds();

            // 开始事务
            using (Transaction trans = doc.Database.TransactionManager.StartTransaction())
            {
                // 存储所有曲线对象（使用整数索引而非 ObjectId）
                Dictionary<int, Curve> curveObjects = new Dictionary<int, Curve>();
                
                // 存储每条曲线的交点参数列表
                Dictionary<int, List<double>> curveIntersections = new Dictionary<int, List<double>>();
                
                // 存储需要删除的原始对象
                List<ObjectId> objectsToDelete = new List<ObjectId>();

                int curveIndex = 0;

                // 第一步：处理所有选中对象
                foreach (ObjectId objId in selectedObjects)
                {
                    Entity ent = trans.GetObject(objId, OpenMode.ForRead) as Entity;
                    
                    if (ent is Polyline pline)
                    {
                        // Polyline：分解为 Line 段
                        var segments = ExplodePolylineToLines(pline);
                        foreach (var seg in segments)
                        {
                            curveObjects[curveIndex] = seg;
                            curveIntersections[curveIndex] = new List<double>();
                            curveIndex++;
                        }
                        objectsToDelete.Add(objId); // 标记删除原 Polyline
                    }
                    else if (ent is Polyline2d pline2d)
                    {
                        // Polyline2d：分解为 Line 段
                        var segments = ExplodePolyline2dToLines(pline2d, trans);
                        foreach (var seg in segments)
                        {
                            curveObjects[curveIndex] = seg;
                            curveIntersections[curveIndex] = new List<double>();
                            curveIndex++;
                        }
                        objectsToDelete.Add(objId); // 标记删除原 Polyline2d
                    }
                    else if (ent is Circle circle)
                    {
                        // Circle：直接处理
                        curveObjects[curveIndex] = circle;
                        curveIntersections[curveIndex] = new List<double>();
                        curveIndex++;
                        objectsToDelete.Add(objId); // 也要删除原对象
                    }
                    else if (ent is Curve curve)
                    {
                        // Line, Arc, Spline 等其他曲线：直接处理
                        curveObjects[curveIndex] = curve;
                        curveIntersections[curveIndex] = new List<double>();
                        curveIndex++;
                        objectsToDelete.Add(objId); // 也要删除原对象
                    }
                }

                if (curveObjects.Count == 0)
                {
                    ed.WriteMessage("\n没有找到有效的曲线。");
                    return;
                }

                // 第二步：计算所有曲线对之间的交点
            var curveIds = curveObjects.Keys.ToList();
            int totalIntersections = 0;

            for (int i = 0; i < curveIds.Count; i++)
            {
                for (int j = i + 1; j < curveIds.Count; j++)
                {
                        Curve curve1 = curveObjects[curveIds[i]];
                        Curve curve2 = curveObjects[curveIds[j]];

                        // 计算交点
                        Point3dCollection intersectionPoints = new Point3dCollection();
                        curve1.IntersectWith(curve2, Intersect.OnBothOperands, intersectionPoints, IntPtr.Zero, IntPtr.Zero);

                    if (intersectionPoints.Count > 0)
                    {
                        totalIntersections += intersectionPoints.Count;

                        foreach (Point3d intersectionPoint in intersectionPoints)
                        {
                            // 检查交点是否在曲线1上
                            if (IsPointOnCurve(curve1, intersectionPoint))
                            {
                                double param1 = GetParameterAtPoint(curve1, intersectionPoint);
                                if (!double.IsNaN(param1))
                                    curveIntersections[curveIds[i]].Add(param1);
                            }

                            // 检查交点是否在曲线2上
                            if (IsPointOnCurve(curve2, intersectionPoint))
                            {
                                double param2 = GetParameterAtPoint(curve2, intersectionPoint);
                                if (!double.IsNaN(param2))
                                    curveIntersections[curveIds[j]].Add(param2);
                            }
                        }
                    }
                }
            }

                // 第三步：分割曲线
                List<Curve> newCurves = new List<Curve>();

                foreach (var item in curveIntersections)
                {
                    int curveId = item.Key;
                    List<double> intersectionParams = item.Value.Distinct().ToList();
                    Curve curve = curveObjects[curveId];

                    if (intersectionParams.Count > 0)
                    {
                        // 添加起始和结束参数
                        intersectionParams.Add(curve.StartParam);
                        intersectionParams.Add(curve.EndParam);

                        // 对参数进行排序并去重
                        intersectionParams = intersectionParams.Distinct().OrderBy(p => p).ToList();

                        // 创建新的曲线段
                        List<Curve> segments = new List<Curve>();
                        for (int i = 0; i < intersectionParams.Count - 1; i++)
                        {
                            double paramStart = intersectionParams[i];
                            double paramEnd = intersectionParams[i + 1];

                            if (Math.Abs(paramEnd - paramStart) > Tolerance.Global.EqualPoint)
                            {
                                Curve segment = GetCurveSegment(curve, paramStart, paramEnd);
                                if (segment != null)
                                {
                                    segments.Add(segment);
                                }
                            }
                        }

                        if (segments.Count > 0)
                        {
                            newCurves.AddRange(segments);
                        }
                        else
                        {
                            // 分割全部失败，保留原曲线
                            Curve clonedCurve = curve.Clone() as Curve;
                            if (clonedCurve != null)
                                newCurves.Add(clonedCurve);
                        }
                    }
                    else
                    {
                        // 如果没有交点，保留原曲线
                        Curve newCurve = curve.Clone() as Curve;
                        if (newCurve != null)
                            newCurves.Add(newCurve);
                    }
                }

                // 第四步：更新图纸
                BlockTable bt = trans.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

                // 添加新曲线段
                foreach (Curve newCurve in newCurves)
                {
                    btr.AppendEntity(newCurve);
                    trans.AddNewlyCreatedDBObject(newCurve, true);
                }

                // 删除原始对象
                foreach (ObjectId id in objectsToDelete)
                {
                    Entity ent = trans.GetObject(id, OpenMode.ForWrite) as Entity;
                    ent?.Erase();
                }

                trans.Commit();

                ed.WriteMessage($"\n处理完成：{objectsToDelete.Count} → {newCurves.Count} 曲线");
            }
        }

        /// <summary>
        /// 将 Polyline 分解为 Line 段
        /// </summary>
        private List<Curve> ExplodePolylineToLines(Polyline pline)
        {
            var lines = new List<Curve>();

            for (int i = 0; i < pline.NumberOfVertices - 1; i++)
            {
                if (pline.GetSegmentType(i) == SegmentType.Line)
                {
                    // 直线段
                    Point3d start = pline.GetPoint3dAt(i);
                    Point3d end = pline.GetPoint3dAt(i + 1);
                    lines.Add(new Line(start, end));
                }
                else if (pline.GetSegmentType(i) == SegmentType.Arc)
                {
                    // 弧线段：通过 GetArcSegment2dAt 获取
                    CircularArc2d arc2d = pline.GetArcSegment2dAt(i);
                    if (arc2d != null)
                    {
                        // 将 2D 弧转换为 3D 弧
                        Point3d center3d = new Point3d(arc2d.Center.X, arc2d.Center.Y, pline.Elevation);
                        Point3d start3d = pline.GetPoint3dAt(i);
                        Point3d end3d = pline.GetPoint3dAt(i + 1);
                        
                        Vector3d normal = pline.Normal;
                        Arc arc3d = new Arc(center3d, normal, arc2d.Radius, arc2d.StartAngle, arc2d.EndAngle);
                        lines.Add(arc3d);
                    }
                }
            }

            // 处理闭合多段线
            if (pline.Closed && pline.NumberOfVertices > 2)
            {
                int lastIndex = pline.NumberOfVertices - 1;
                if (pline.GetSegmentType(lastIndex) == SegmentType.Line)
                {
                    Point3d start = pline.GetPoint3dAt(lastIndex);
                    Point3d end = pline.GetPoint3dAt(0);
                    lines.Add(new Line(start, end));
                }
                else if (pline.GetSegmentType(lastIndex) == SegmentType.Arc)
                {
                    CircularArc2d arc2d = pline.GetArcSegment2dAt(lastIndex);
                    if (arc2d != null)
                    {
                        Point3d center3d = new Point3d(arc2d.Center.X, arc2d.Center.Y, pline.Elevation);
                        Vector3d normal = pline.Normal;
                        Arc arc3d = new Arc(center3d, normal, arc2d.Radius, arc2d.StartAngle, arc2d.EndAngle);
                        lines.Add(arc3d);
                    }
                }
            }

            return lines;
        }

        /// <summary>
        /// 将 Polyline2d 分解为 Line 段
        /// </summary>
        private List<Curve> ExplodePolyline2dToLines(Polyline2d pline2d, Transaction trans)
        {
            var lines = new List<Curve>();
            var vertices = new List<Point3d>();

            // 获取所有顶点
            foreach (ObjectId vId in pline2d)
            {
                Vertex2d vertex = trans.GetObject(vId, OpenMode.ForRead) as Vertex2d;
                if (vertex != null)
                {
                    vertices.Add(vertex.Position);
                }
            }

            // 创建线段
            for (int i = 0; i < vertices.Count - 1; i++)
            {
                lines.Add(new Line(vertices[i], vertices[i + 1]));
            }

            // 处理闭合
            if (pline2d.Closed && vertices.Count > 2)
            {
                lines.Add(new Line(vertices[vertices.Count - 1], vertices[0]));
            }

            return lines;
        }

        /// <summary>
        /// 检查点是否在曲线上
        /// </summary>
        private bool IsPointOnCurve(Curve curve, Point3d point)
        {
            double param;
            try
            {
                param = curve.GetParameterAtPoint(point);
            }
            catch
            {
                return false;
            }

            return param >= curve.StartParam - Tolerance.Global.EqualPoint &&
                   param <= curve.EndParam + Tolerance.Global.EqualPoint;
        }

        /// <summary>
        /// 获取点在曲线上的参数
        /// </summary>
        private double GetParameterAtPoint(Curve curve, Point3d point)
        {
            try
            {
                return curve.GetParameterAtPoint(point);
            }
            catch
            {
                return double.NaN;
            }
        }

        /// <summary>
        /// 获取曲线的指定参数范围的段
        /// </summary>
        private Curve GetCurveSegment(Curve curve, double paramStart, double paramEnd)
        {
            if (curve == null)
                return null;

            // 确保参数顺序正确
            if (paramStart > paramEnd)
            {
                double temp = paramStart;
                paramStart = paramEnd;
                paramEnd = temp;
            }

            try
            {
                // 获取对应参数的点
                Point3d startPt = curve.GetPointAtParameter(paramStart);
                Point3d endPt = curve.GetPointAtParameter(paramEnd);

                // 创建分割点集合
                Point3dCollection splitPoints = new Point3dCollection { startPt, endPt };

                // 使用GetSplitCurves方法
                DBObjectCollection segments = curve.GetSplitCurves(splitPoints);

                // 找到起点和终点匹配的曲线段
                Curve result = null;
                int matchedIndex = -1;
                
                for (int i = 0; i < segments.Count; i++)
                {
                    Curve segment = segments[i] as Curve;
                    if (segment != null)
                    {
                        Point3d segStart = segment.StartPoint;
                        Point3d segEnd = segment.EndPoint;

                        // 检查曲线段的起点和终点是否匹配
                        bool matchForward = segStart.IsEqualTo(startPt, Tolerance.Global) && segEnd.IsEqualTo(endPt, Tolerance.Global);
                        bool matchReverse = segStart.IsEqualTo(endPt, Tolerance.Global) && segEnd.IsEqualTo(startPt, Tolerance.Global);
                        
                        if (matchForward || matchReverse)
                        {
                            matchedIndex = i;
                            result = segment.Clone() as Curve;
                            break;
                        }
                    }
                }

                // 释放所有未使用的段
                for (int i = 0; i < segments.Count; i++)
                {
                    if (i != matchedIndex && segments[i] != null)
                    {
                        segments[i].Dispose();
                    }
                }
                
                return result;
            }
            catch
            {
                return null;
            }
        }
    }
}
