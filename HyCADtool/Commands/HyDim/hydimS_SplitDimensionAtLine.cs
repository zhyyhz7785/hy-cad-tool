using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Command
{
    public static partial class HyCommand
    {
        [CommandMethod("sd")]
        public static void SplitDimensionAtLine()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;

            try
            {
                // 提示用户选择尺寸
                PromptSelectionOptions dimOpts = new PromptSelectionOptions();
                dimOpts.MessageForAdding = "\n请选择尺寸: ";
                TypedValue[] dimFilter = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Start, "DIMENSION")
                };
                SelectionFilter dimSf = new SelectionFilter(dimFilter);
                PromptSelectionResult dimRes = ed.GetSelection(dimOpts, dimSf);
                if (dimRes.Status != PromptStatus.OK || dimRes.Value.Count == 0)
                {
                    ed.WriteMessage("\n未选择尺寸，操作取消。请重新选择。");
                    return;
                }

                // 提示用户选择多条直线
                PromptSelectionOptions lineOpts = new PromptSelectionOptions();
                lineOpts.MessageForAdding = "\n请选择直线（可多选）: ";
                TypedValue[] lineFilter = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Start, "LINE")
                };
                SelectionFilter lineSf = new SelectionFilter(lineFilter);
                PromptSelectionResult lineRes = ed.GetSelection(lineOpts, lineSf);
                if (lineRes.Status != PromptStatus.OK || lineRes.Value.Count == 0)
                {
                    ed.WriteMessage("\n未选择直线，操作取消。请重新选择。");
                    return;
                }

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTableRecord btr = tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;

                    // 获取并排序直线
                    var lines = lineRes.Value.GetObjectIds()
                        .Select(id => tr.GetObject(id, OpenMode.ForRead) as Line)
                        .Where(line => line != null)
                        .OrderBy(line => line.StartPoint.X)
                        .ThenBy(line => line.StartPoint.Y)
                        .ToList();

                    foreach (ObjectId dimId in dimRes.Value.GetObjectIds())
                    {
                        Dimension dim = tr.GetObject(dimId, OpenMode.ForWrite) as Dimension;
                        if (dim == null || !(dim is RotatedDimension rotatedDim))
                        {
                            ed.WriteMessage("\n仅支持旋转尺寸（RotatedDimension），跳过不支持的类型。");
                            continue;
                        }

                        // 获取原始图层和尺寸线
                        string originalLayer = dim.Layer;
                        Point3d p1 = rotatedDim.XLine1Point;
                        Point3d p2 = rotatedDim.XLine2Point;
                        Line dimLine = new Line(p1, p2);
                        double dimLineLength = p1.DistanceTo(p2);

                        ed.WriteMessage($"\n调试信息: 尺寸 {dimId}, 起点 = {p1}, 终点 = {p2}, 长度 = {dimLineLength:F2}");

                        // 计算当前尺寸与所有直线的交点
                        List<Point3d> intersectionPoints = new List<Point3d>();
                        foreach (Line line in lines)
                        {
                            Point3dCollection intersections = new Point3dCollection();
                            dimLine.IntersectWith(line, Intersect.OnBothOperands, intersections, IntPtr.Zero, IntPtr.Zero);
                            foreach (Point3d pt in intersections)
                            {
                                double param = dimLine.GetParameterAtPoint(pt);
                                double distanceToStart = p1.DistanceTo(pt);
                                double distanceToEnd = p2.DistanceTo(pt);

                                if (param >= 0 && distanceToStart <= dimLineLength + Point3dEqualityComparerLocal.ToleranceValue && distanceToEnd <= dimLineLength + Point3dEqualityComparerLocal.ToleranceValue)
                                {
                                    intersectionPoints.Add(pt);
                                    ed.WriteMessage($"\n调试信息: 尺寸 {dimId} 找到交点 {pt}, param = {param:F2}, 距起点 = {distanceToStart:F2}");
                                }
                                else
                                {
                                    ed.WriteMessage($"\n调试信息: 尺寸 {dimId} 交点 {pt} 超出范围, param = {param:F2}, 距起点 = {distanceToStart:F2}");
                                }
                            }
                        }

                        // 按参数值排序交点并去重
                        intersectionPoints = intersectionPoints
                            .OrderBy(pt => dimLine.GetParameterAtPoint(pt))
                            .Distinct(new Point3dEqualityComparerLocal())
                            .ToList();

                        if (intersectionPoints.Count > 0)
                        {
                            // 创建多个新尺寸
                            List<RotatedDimension> newDims = new List<RotatedDimension>();
                            Point3d startPt = p1;

                            for (int i = 0; i <= intersectionPoints.Count; i++)
                            {
                                Point3d endPt = i < intersectionPoints.Count ? intersectionPoints[i] : p2;

                                // 检查新尺寸是否有效（起点和终点不重合）
                                if (startPt.DistanceTo(endPt) > Point3dEqualityComparerLocal.ToleranceValue)
                                {
                                    RotatedDimension newDim = new RotatedDimension
                                    {
                                        Rotation = rotatedDim.Rotation,
                                        DimLinePoint = rotatedDim.DimLinePoint,
                                        XLine1Point = startPt,
                                        XLine2Point = endPt,
                                        DimensionStyle = rotatedDim.DimensionStyle,
                                        Layer = originalLayer
                                    };

                                    btr.AppendEntity(newDim);
                                    tr.AddNewlyCreatedDBObject(newDim, true);
                                    newDims.Add(newDim);
                                    ed.WriteMessage($"\n调试信息: 新尺寸创建，起点 = {startPt}, 终点 = {endPt}");
                                }
                                else
                                {
                                    ed.WriteMessage($"\n调试信息: 跳过无效尺寸段，起点 = {startPt}, 终点 = {endPt} (距离过小)");
                                }

                                startPt = endPt;
                            }

                            if (newDims.Count > 0)
                            {
                                // 删除原始尺寸
                                dim.Erase();
                                ed.WriteMessage($"\n尺寸 {dimId} 被分割为 {newDims.Count} 段，图层保持为 {originalLayer}。");
                            }
                            else
                            {
                                ed.WriteMessage($"\n尺寸 {dimId} 无有效分割段，保持不变。");
                            }
                        }
                        else
                        {
                            ed.WriteMessage($"\n尺寸 {dimId} 未找到有效交点，保持不变。");
                        }
                    }

                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n发生错误: {ex.Message}");
            }
        }
    }

    // 用于比较Point3d的相等性（去重）
    public class Point3dEqualityComparerLocal : IEqualityComparer<Point3d>
    {
        public const double ToleranceValue = 0.1;

        public bool Equals(Point3d p1, Point3d p2)
        {
            return p1.GetVectorTo(p2).Length < ToleranceValue;
        }

        public int GetHashCode(Point3d p)
        {
            return p.X.GetHashCode() ^ p.Y.GetHashCode() ^ p.Z.GetHashCode();
        }
    }
}