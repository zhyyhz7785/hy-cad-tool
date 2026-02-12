using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 分割尺寸线命令（对应旧命令 sd / SplitDimensionAtLine）
    /// 流程：选尺寸 → 选直线 → 在交点处分割尺寸为多段
    /// </summary>
    public class SplitDimensionCommand
    {
        private const double PointTolerance = 0.1;

        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            try
            {
                // 1. 选择尺寸
                var dimOpts = new PromptSelectionOptions { MessageForAdding = "\n请选择尺寸: " };
                var dimFilter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "DIMENSION") });
                PromptSelectionResult dimRes = ed.GetSelection(dimOpts, dimFilter);
                if (dimRes.Status != PromptStatus.OK || dimRes.Value.Count == 0) return;

                // 2. 选择直线
                var lineOpts = new PromptSelectionOptions { MessageForAdding = "\n请选择直线（可多选）: " };
                var lineFilter = new SelectionFilter(new[] { new TypedValue((int)DxfCode.Start, "LINE") });
                PromptSelectionResult lineRes = ed.GetSelection(lineOpts, lineFilter);
                if (lineRes.Status != PromptStatus.OK || lineRes.Value.Count == 0) return;

                using (var tr = db.TransactionManager.StartTransaction())
                {
                    var btr = (BlockTableRecord)tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite);

                    // 获取并排序直线
                    var lines = lineRes.Value.GetObjectIds()
                        .Select(id => tr.GetObject(id, OpenMode.ForRead) as Line)
                        .Where(line => line != null)
                        .OrderBy(line => line.StartPoint.X)
                        .ThenBy(line => line.StartPoint.Y)
                        .ToList();

                    foreach (ObjectId dimId in dimRes.Value.GetObjectIds())
                    {
                        var dim = tr.GetObject(dimId, OpenMode.ForWrite) as Dimension;
                        if (!(dim is RotatedDimension rotatedDim)) continue;

                        string originalLayer = dim.Layer;
                        Point3d p1 = rotatedDim.XLine1Point;
                        Point3d p2 = rotatedDim.XLine2Point;
                        var dimLine = new Line(p1, p2);
                        double dimLineLength = p1.DistanceTo(p2);

                        // 计算所有交点
                        var intersectionPoints = new List<Point3d>();
                        foreach (Line line in lines)
                        {
                            var intersections = new Point3dCollection();
                            dimLine.IntersectWith(line, Intersect.OnBothOperands,
                                intersections, IntPtr.Zero, IntPtr.Zero);

                            foreach (Point3d pt in intersections)
                            {
                                double distToStart = p1.DistanceTo(pt);
                                double distToEnd = p2.DistanceTo(pt);
                                if (distToStart <= dimLineLength + PointTolerance
                                    && distToEnd <= dimLineLength + PointTolerance)
                                {
                                    intersectionPoints.Add(pt);
                                }
                            }
                        }

                        // 排序去重
                        intersectionPoints = intersectionPoints
                            .OrderBy(pt => p1.DistanceTo(pt))
                            .Distinct(new Point3dTolerance(PointTolerance))
                            .ToList();

                        if (intersectionPoints.Count == 0) continue;

                        // 创建分段尺寸
                        var newDims = new List<RotatedDimension>();
                        Point3d startPt = p1;

                        for (int i = 0; i <= intersectionPoints.Count; i++)
                        {
                            Point3d endPt = i < intersectionPoints.Count ? intersectionPoints[i] : p2;

                            if (startPt.DistanceTo(endPt) > PointTolerance)
                            {
                                var newDim = new RotatedDimension
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
                            }
                            startPt = endPt;
                        }

                        if (newDims.Count > 0)
                            dim.Erase();
                    }

                    tr.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n发生错误: {ex.Message}");
            }
        }

        /// <summary>
        /// Point3d 容差比较器
        /// </summary>
        private class Point3dTolerance : IEqualityComparer<Point3d>
        {
            private readonly double _tolerance;
            public Point3dTolerance(double tolerance) { _tolerance = tolerance; }

            public bool Equals(Point3d a, Point3d b)
            {
                return a.DistanceTo(b) < _tolerance;
            }

            public int GetHashCode(Point3d p)
            {
                // 粗粒度哈希，保证容差内的点落入同一桶
                return 0;
            }
        }
    }
}
