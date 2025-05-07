using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
namespace HyCADTool.Tools
{
    public static class BreakCurves
    {
        public static List<Curve> BreakCurvesAtIntersections()
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
                new TypedValue((int)DxfCode.Start, "LINE,ARC,LWPOLYLINE,POLYLINE,SPLINE")
            });
            PromptSelectionResult selRes = ed.GetSelection(selOpts, filter);
            if (selRes.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n未选择任何曲线。");
                return null;
            }
            // 获取选择的对象集合
            ObjectId[] selectedCurves = selRes.Value.GetObjectIds();
            // 开始事务
            using (Transaction trans = doc.Database.TransactionManager.StartTransaction())
            {
                // 存储所有的曲线对象
                Dictionary<ObjectId, Curve> curveObjects = new Dictionary<ObjectId, Curve>();
                // 存储每条曲线的交点参数列表
                Dictionary<ObjectId, List<double>> curveIntersections = new Dictionary<ObjectId, List<double>>();
                // 初始化
                foreach (ObjectId curveId in selectedCurves)
                {
                    Curve curve = trans.GetObject(curveId, OpenMode.ForRead) as Curve;
                    if (curve != null)
                    {
                        curveObjects[curveId] = curve;
                        curveIntersections[curveId] = new List<double>();
                    }
                }
                // 计算所有曲线对之间的交点
                var curveIds = curveObjects.Keys.ToList();
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
                            foreach (Point3d intersectionPoint in intersectionPoints)
                            {
                                // 检查交点是否在曲线的范围内
                                if (IsPointOnCurve(curve1, intersectionPoint))
                                {
                                    double param1 = GetParameterAtPoint(curve1, intersectionPoint);
                                    if (!double.IsNaN(param1))
                                        curveIntersections[curveIds[i]].Add(param1);
                                }
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
                // 存储新的曲线段
                List<Curve> newCurves = new List<Curve>();
                // 分割曲线
                foreach (var item in curveIntersections)
                {
                    ObjectId curveId = item.Key;
                    List<double> intersectionParams = item.Value.Distinct().ToList();
                    Curve curve = curveObjects[curveId];
                    if (intersectionParams.Count > 0)
                    {
                        // 添加起始和结束参数
                        intersectionParams.Add(curve.StartParam);
                        intersectionParams.Add(curve.EndParam);
                        // 对参数进行排序
                        intersectionParams = intersectionParams.Distinct().OrderBy(p => p).ToList();
                        // 创建新的曲线段
                        for (int i = 0; i < intersectionParams.Count - 1; i++)
                        {
                            double paramStart = intersectionParams[i];
                            double paramEnd = intersectionParams[i + 1];
                            if (Math.Abs(paramEnd - paramStart) > Tolerance.Global.EqualPoint)
                            {
                                Curve segment = GetCurveSegment(curve, paramStart, paramEnd);
                                if (segment != null)
                                    newCurves.Add(segment);
                            }
                        }
                    }
                    else
                    {
                        // 如果没有交点，直接添加原曲线的副本
                        Curve newCurve = curve.Clone() as Curve;
                        if (newCurve != null)
                            newCurves.Add(newCurve);
                    }
                }
                // 获取模型空间
                BlockTable bt = trans.GetObject(doc.Database.BlockTableId, OpenMode.ForRead) as BlockTable;
                BlockTableRecord btr = trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
                // 将新的曲线段添加到模型空间
                foreach (Entity newCurve in newCurves)
                {
                    btr.AppendEntity(newCurve);
                    trans.AddNewlyCreatedDBObject(newCurve, true);
                }
                // 删除原始曲线
                foreach (ObjectId id in selectedCurves)
                {
                    Entity ent = trans.GetObject(id, OpenMode.ForWrite) as Entity;
                    ent.Erase();
                }
                // 提交事务
                trans.Commit();
                return (newCurves);
            }
        }
        // 辅助方法：检查点是否在曲线上
        private static bool IsPointOnCurve(Curve curve, Point3d point)
        {
            // 获取点对应的参数
            double param;
            try
            {
                param = curve.GetParameterAtPoint(point);
            }
            catch
            {
                // 点不在曲线上
                return false;
            }
            // 检查参数是否在曲线的参数范围内
            return param >= curve.StartParam - Tolerance.Global.EqualPoint &&
                   param <= curve.EndParam + Tolerance.Global.EqualPoint;
        }
        // 辅助方法：获取点在曲线上的参数
        private static double GetParameterAtPoint(Curve curve, Point3d point)
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
        // 辅助方法：获取曲线的指定参数范围的段
        private static Curve GetCurveSegment(Curve curve, double paramStart, double paramEnd)
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
                // segments包含分割后的曲线段
                // 我们需要找到起点和终点与startPt和endPt匹配的曲线段
                foreach (DBObject obj in segments)
                {
                    Curve segment = obj as Curve;
                    if (segment != null)
                    {
                        Point3d segStart = segment.StartPoint;
                        Point3d segEnd = segment.EndPoint;
                        // 检查曲线段的起点和终点是否与我们指定的点匹配（考虑容差）
                        if ((segStart.IsEqualTo(startPt, Tolerance.Global) && segEnd.IsEqualTo(endPt, Tolerance.Global)) ||
                            (segStart.IsEqualTo(endPt, Tolerance.Global) && segEnd.IsEqualTo(startPt, Tolerance.Global)))
                        {
                            // 返回曲线段的克隆
                            return segment.Clone() as Curve;
                        }
                    }
                }
                // 如果没有找到匹配的曲线段，返回null
                return null;
            }
            catch (System.Exception ex)
            {
                // 输出错误信息，便于调试
                Application.DocumentManager.MdiActiveDocument.Editor.WriteMessage($"\n分割曲线时出错：{ex.Message}");
                return null;
            }
        }
    }
}
