using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.GraphicsInterface;
using AcDbPolyline = Autodesk.AutoCAD.DatabaseServices.Polyline;

[assembly: CommandClass(typeof(HyCADTool.Refactored.Presentation.Commands.JoinParallelLinesCommand))]

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 连接平行直线命令
    /// 选择直线和多段线，连接共线且端点距离相近的线段
    /// </summary>
    public class JoinParallelLinesCommand
    {
        // 容差设置（统一放在文件开头）
        private const double COLLINEAR_TOLERANCE = 1.0; // 共线角度容差（度）
        private const double DISTANCE_TOLERANCE = 0.001; // 向量长度容差
        private const double PARALLEL_LINE_DISTANCE_THRESHOLD = 1.0; // 两条平行线之间的距离容差（用于判断是否共线）

        [CommandMethod("HYJP")]
        public void Execute()
        {
            short originalOrthoMode = 0;
            bool orthoCaptured = false;
            Editor ed = null;

            try
            {
                originalOrthoMode = Convert.ToInt16(Application.GetSystemVariable("ORTHOMODE"));
                orthoCaptured = true;
                if (originalOrthoMode != 0)
                {
                    Application.SetSystemVariable("ORTHOMODE", 0);
                }

                Document doc = Application.DocumentManager.MdiActiveDocument;
                if (doc == null)
                    return;

                Database db = doc.Database;
                ed = doc.Editor;

                // 2. 获取第一个点
                PromptPointOptions ppo1 = new PromptPointOptions("\n指定第一个角点：");
                PromptPointResult ppr1 = ed.GetPoint(ppo1);
                if (ppr1.Status != PromptStatus.OK)
                    return;

                Point3d pt1 = ppr1.Value;
                Point3d pt2;

                // 3. 使用Jig获取第二个点，显示虚线矩形选择框
                SelectionWindowJig jig = new SelectionWindowJig(pt1);
                PromptResult jigResult = jig.StartJig();
                if (jigResult.Status != PromptStatus.OK)
                    return;

                pt2 = jig.SecondPoint;

                // 计算选择框范围
                double minX = Math.Min(pt1.X, pt2.X);
                double maxX = Math.Max(pt1.X, pt2.X);
                double minY = Math.Min(pt1.Y, pt2.Y);
                double maxY = Math.Max(pt1.Y, pt2.Y);
                double minZ = Math.Min(pt1.Z, pt2.Z);
                double maxZ = Math.Max(pt1.Z, pt2.Z);

                Extents3d selectionExtents = new Extents3d(
                    new Point3d(minX, minY, minZ),
                    new Point3d(maxX, maxY, maxZ));

                // 2. 自动在选择框内选择对象
                TypedValue[] filterList = new TypedValue[]
                {
                    new TypedValue((int)DxfCode.Operator, "<OR"),
                    new TypedValue((int)DxfCode.Start, "LINE"),
                    new TypedValue((int)DxfCode.Start, "LWPOLYLINE"),
                    new TypedValue((int)DxfCode.Operator, "OR>")
                };

                SelectionFilter filter = new SelectionFilter(filterList);
                
                // 使用交叉窗口选择（Crossing Window）- 选择与窗口相交或完全在窗口内的对象
                PromptSelectionResult psr = ed.SelectCrossingWindow(pt1, pt2, filter);

                if (psr.Status != PromptStatus.OK || psr.Value == null || psr.Value.Count == 0)
                {
                    ed.WriteMessage("\n未选择任何对象。");
                    return;
                }

                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    BlockTableRecord btr = tr.GetObject(db.CurrentSpaceId, OpenMode.ForWrite) as BlockTableRecord;
                    List<AcDbPolyline> polylines = new List<AcDbPolyline>();

                    // 步骤3: 将所有直线转换为多段线
                    foreach (SelectedObject selObj in psr.Value)
                    {
                        Entity ent = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Entity;

                        if (ent is Line line)
                        {
                            AcDbPolyline pline = ConvertLineToPolyline(line);
                            ObjectId newId = btr.AppendEntity(pline);
                            tr.AddNewlyCreatedDBObject(pline, true);

                            // 删除原直线
                            line.UpgradeOpen();
                            line.Erase();

                            polylines.Add(pline);
                        }
                        else if (ent is AcDbPolyline pline)
                        {
                            polylines.Add(pline);
                        }
                    }

                    // 步骤4: 获取所有多段线的端点，并筛选在选择框内的端点
                    List<EndpointInfo> allEndpoints = GetAllPolylineEndpoints(polylines);
                    List<EndpointInfo> endpointsInBounds = FilterEndpointsInBounds(allEndpoints, selectionExtents);
                    
                    // 步骤5: 连接共线的多段线
                    int mergedCount = MergeCollinearPolylines(polylines, endpointsInBounds, tr);

                    tr.Commit();

                    ed.WriteMessage($"\n处理完成：合并 {mergedCount} 对");
                }
            }
            catch (System.Exception ex)
            {
                ed?.WriteMessage($"\n错误: {ex.Message}");
            }
            finally
            {
                if (orthoCaptured)
                {
                    Application.SetSystemVariable("ORTHOMODE", originalOrthoMode);
                }
            }
        }


        // 将直线转换为多段线
        private AcDbPolyline ConvertLineToPolyline(Line line)
        {
            AcDbPolyline pline = new AcDbPolyline();
            pline.AddVertexAt(0, new Point2d(line.StartPoint.X, line.StartPoint.Y), 0, 0, 0);
            pline.AddVertexAt(1, new Point2d(line.EndPoint.X, line.EndPoint.Y), 0, 0, 0);
            pline.Layer = line.Layer;
            pline.Color = line.Color;
            pline.LineWeight = line.LineWeight;
            return pline;
        }

        // 获取所有多段线的端点（起点和终点）
        private List<EndpointInfo> GetAllPolylineEndpoints(List<AcDbPolyline> polylines)
        {
            List<EndpointInfo> allEndpoints = new List<EndpointInfo>();

            foreach (AcDbPolyline pline in polylines)
            {
                // 只检查起点和终点，跳过中间点
                int[] vertexIndices = { 0, pline.NumberOfVertices - 1 };

                foreach (int i in vertexIndices)
                {
                    Point3d pt = pline.GetPoint3dAt(i);

                    allEndpoints.Add(new EndpointInfo
                    {
                        Point = pt,
                        Polyline = pline,
                        VertexIndex = i,
                        IsStartPoint = (i == 0),
                        IsEndPoint = (i == pline.NumberOfVertices - 1)
                    });
                }
            }

            return allEndpoints;
        }

        // 筛选在选择框内的端点
        private List<EndpointInfo> FilterEndpointsInBounds(List<EndpointInfo> allEndpoints, Extents3d bounds)
        {
            List<EndpointInfo> endpointsInBounds = new List<EndpointInfo>();

            foreach (var epInfo in allEndpoints)
            {
                Point3d pt = epInfo.Point;

                // 检查点是否在选择框内
                bool isInsideBounds =
                    pt.X >= bounds.MinPoint.X && pt.X <= bounds.MaxPoint.X &&
                    pt.Y >= bounds.MinPoint.Y && pt.Y <= bounds.MaxPoint.Y;

                if (isInsideBounds)
                {
                    endpointsInBounds.Add(epInfo);
                }
            }

            return endpointsInBounds;
        }


        // 合并共线的多段线
        private int MergeCollinearPolylines(List<AcDbPolyline> polylines, List<EndpointInfo> allEndpoints, Transaction tr)
        {
            int mergedCount = 0;
            List<AcDbPolyline> toRemove = new List<AcDbPolyline>();

            for (int i = 0; i < allEndpoints.Count; i++)
            {
                for (int j = i + 1; j < allEndpoints.Count; j++)
                {
                    EndpointInfo ep1 = allEndpoints[i];
                    EndpointInfo ep2 = allEndpoints[j];

                    // 不处理同一多段线
                    if (ep1.Polyline == ep2.Polyline)
                        continue;

                    // 检查是否共线
                    if (AreSegmentsCollinear(ep1, ep2))
                    {
                        // 合并多段线
                        if (MergePolylines(ep1, ep2, tr))
                        {
                            mergedCount++;

                            // 标记第二条线为待删除
                            if (!toRemove.Contains(ep2.Polyline))
                                toRemove.Add(ep2.Polyline);
                        }
                    }
                }
            }

            // 删除已合并的多段线
            foreach (AcDbPolyline pline in toRemove)
            {
                pline.UpgradeOpen();
                pline.Erase();
            }

            return mergedCount;
        }

        // 检查两个线段是否共线
        // 共线判断：1. 角度平行（容差1度） 2. 两条平行线之间的距离小于阈值（如1）
        private bool AreSegmentsCollinear(EndpointInfo ep1, EndpointInfo ep2)
        {
            Vector3d vec1 = GetSegmentDirection(ep1);
            Vector3d vec2 = GetSegmentDirection(ep2);

            if (vec1.Length < DISTANCE_TOLERANCE || vec2.Length < DISTANCE_TOLERANCE)
                return false;

            // 1. 检查角度是否平行
            double angle = vec1.GetAngleTo(vec2);
            double angleDegrees = angle * 180.0 / Math.PI;

            bool isParallel = angleDegrees < COLLINEAR_TOLERANCE ||
                             Math.Abs(angleDegrees - 180) < COLLINEAR_TOLERANCE;

            if (!isParallel)
                return false;

            // 2. 检查两条平行线之间的距离
            double lineDistance = GetDistanceBetweenParallelLines(ep1, ep2);
            bool isCollinear = lineDistance < PARALLEL_LINE_DISTANCE_THRESHOLD;

            return isCollinear;
        }

        /// <summary>
        /// 计算两条平行线之间的距离
        /// </summary>
        private double GetDistanceBetweenParallelLines(EndpointInfo ep1, EndpointInfo ep2)
        {
            AcDbPolyline pline1 = ep1.Polyline;
            AcDbPolyline pline2 = ep2.Polyline;

            // 获取线段1的两个点
            Point3d seg1Start, seg1End;
            if (ep1.IsStartPoint && pline1.NumberOfVertices > 1)
            {
                seg1Start = pline1.GetPoint3dAt(0);
                seg1End = pline1.GetPoint3dAt(1);
            }
            else if (ep1.IsEndPoint && pline1.NumberOfVertices > 1)
            {
                seg1Start = pline1.GetPoint3dAt(pline1.NumberOfVertices - 1);
                seg1End = pline1.GetPoint3dAt(pline1.NumberOfVertices - 2);
            }
            else
            {
                return double.MaxValue; // 无法计算
            }

            // 获取线段2的两个点
            Point3d seg2Start, seg2End;
            if (ep2.IsStartPoint && pline2.NumberOfVertices > 1)
            {
                seg2Start = pline2.GetPoint3dAt(0);
                seg2End = pline2.GetPoint3dAt(1);
            }
            else if (ep2.IsEndPoint && pline2.NumberOfVertices > 1)
            {
                seg2Start = pline2.GetPoint3dAt(pline2.NumberOfVertices - 1);
                seg2End = pline2.GetPoint3dAt(pline2.NumberOfVertices - 2);
            }
            else
            {
                return double.MaxValue; // 无法计算
            }

            // 计算两条线段之间的距离
            // 方法：计算线段1上任意一点到线段2所在直线的距离
            Vector3d line1Dir = (seg1End - seg1Start).GetNormal();
            Vector3d line2Dir = (seg2End - seg2Start).GetNormal();

            // 计算从seg1Start到seg2Start的向量
            Vector3d toLine2 = seg2Start - seg1Start;

            // 计算垂直于line1Dir的向量（即两条平行线之间的距离方向）
            Vector3d perpVector = toLine2 - (toLine2.DotProduct(line1Dir) * line1Dir);

            // 返回距离
            return perpVector.Length;
        }

        // 获取端点所在线段的方向
        private Vector3d GetSegmentDirection(EndpointInfo ep)
        {
            AcDbPolyline pline = ep.Polyline;
            int idx = ep.VertexIndex;
            Point3d pt1, pt2;

            if (ep.IsStartPoint && pline.NumberOfVertices > 1)
            {
                pt1 = pline.GetPoint3dAt(0);
                pt2 = pline.GetPoint3dAt(1);
            }
            else if (ep.IsEndPoint && pline.NumberOfVertices > 1)
            {
                pt1 = pline.GetPoint3dAt(pline.NumberOfVertices - 1);
                pt2 = pline.GetPoint3dAt(pline.NumberOfVertices - 2);
            }
            else
            {
                return Vector3d.XAxis; // 默认值
            }

            return (pt2 - pt1).GetNormal();
        }

        // 合并两条多段线
        private bool MergePolylines(EndpointInfo ep1, EndpointInfo ep2, Transaction tr)
        {
            try
            {
                AcDbPolyline pline1 = ep1.Polyline;
                AcDbPolyline pline2 = ep2.Polyline;
                pline1.UpgradeOpen();

                // 根据端点位置决定如何连接
                if (ep1.IsEndPoint && ep2.IsStartPoint)
                {
                    // 删除连接点（pline1尾点）
                    if (pline1.NumberOfVertices > 0)
                    {
                        pline1.RemoveVertexAt(pline1.NumberOfVertices - 1);
                    }

                    // pline1的尾部连接pline2的头部（跳过连接点）
                    for (int i = 1; i < pline2.NumberOfVertices; i++)
                    {
                        Point2d pt = pline2.GetPoint2dAt(i);
                        pline1.AddVertexAt(pline1.NumberOfVertices, pt, 0, 0, 0);
                    }
                }
                else if (ep1.IsStartPoint && ep2.IsEndPoint)
                {
                    // 删除连接点（pline1首点）
                    if (pline1.NumberOfVertices > 0)
                    {
                        pline1.RemoveVertexAt(0);
                    }

                    // pline2的尾部连接pline1的头部（跳过连接点）
                    for (int i = pline2.NumberOfVertices - 2; i >= 0; i--)
                    {
                        Point2d pt = pline2.GetPoint2dAt(i);
                        pline1.AddVertexAt(0, pt, 0, 0, 0);
                    }
                }
                else
                {
                    return false; // 不支持的连接方式
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        // 端点信息类
        private class EndpointInfo
        {
            public Point3d Point { get; set; }
            public AcDbPolyline Polyline { get; set; }
            public int VertexIndex { get; set; }
            public bool IsStartPoint { get; set; }
            public bool IsEndPoint { get; set; }
        }

        // 选择事件处理方法（用于捕获窗口选择的两个对角点）
        private void OnSelectionAdded(object sender, SelectionAddedEventArgs e)
        {
            // 尝试从事件参数中获取窗口选择的两个对角点
            // 注意：SelectionAddedEventArgs 可能不直接提供窗口角点
            // 这里我们需要使用其他方法来获取
        }
    }

    /// <summary>
    /// 选择窗口Jig，用于显示虚线矩形选择框
    /// </summary>
    public class SelectionWindowJig : DrawJig
    {
        private Point3d _firstPoint;
        private Point3d _secondPoint;
        private Editor _editor;

        public SelectionWindowJig(Point3d firstPoint)
        {
            _firstPoint = firstPoint;
            _secondPoint = firstPoint;
            _editor = Application.DocumentManager.MdiActiveDocument.Editor;
        }

        public Point3d SecondPoint => _secondPoint;

        /// <summary>
        /// 启动 Jig 交互
        /// </summary>
        public PromptResult StartJig()
        {
            return _editor.Drag(this);
        }

        protected override SamplerStatus Sampler(JigPrompts prompts)
        {
            JigPromptPointOptions opts = new JigPromptPointOptions("\n指定第二个角点：");
            opts.BasePoint = _firstPoint;
            opts.UseBasePoint = true;
            opts.Cursor = CursorType.RubberBand;

            PromptPointResult result = prompts.AcquirePoint(opts);
            if (result.Status == PromptStatus.OK)
            {
                if (_secondPoint.DistanceTo(result.Value) < Tolerance.Global.EqualPoint)
                {
                    return SamplerStatus.NoChange;
                }
                _secondPoint = result.Value;
                return SamplerStatus.OK;
            }
            return SamplerStatus.Cancel;
        }

        protected override bool WorldDraw(WorldDraw draw)
        {
            if (draw == null)
                return false;

            // 计算矩形的四个角点
            double minX = Math.Min(_firstPoint.X, _secondPoint.X);
            double maxX = Math.Max(_firstPoint.X, _secondPoint.X);
            double minY = Math.Min(_firstPoint.Y, _secondPoint.Y);
            double maxY = Math.Max(_firstPoint.Y, _secondPoint.Y);

            Point3d pt1 = new Point3d(minX, minY, 0);
            Point3d pt2 = new Point3d(maxX, minY, 0);
            Point3d pt3 = new Point3d(maxX, maxY, 0);
            Point3d pt4 = new Point3d(minX, maxY, 0);

            // 绘制虚线矩形（使用绿色，类似AutoCAD的交叉选择）
            short colorIndex = 3; // 绿色
            draw.SubEntityTraits.Color = colorIndex;
            
            // 绘制虚线矩形（通过绘制多个短线段来模拟虚线效果）
            DrawDashedLine(draw, pt1, pt2);
            DrawDashedLine(draw, pt2, pt3);
            DrawDashedLine(draw, pt3, pt4);
            DrawDashedLine(draw, pt4, pt1);

            return true;
        }

        /// <summary>
        /// 绘制虚线（通过绘制多个短线段来模拟）
        /// </summary>
        private void DrawDashedLine(WorldDraw draw, Point3d start, Point3d end)
        {
            const double dashLength = 50.0; // 虚线段的长度
            const double gapLength = 25.0;  // 间隔的长度
            
            Vector3d direction = (end - start).GetNormal();
            double totalLength = start.DistanceTo(end);

            Point3d currentPoint = start;
            double remainingLength = totalLength;
            bool drawDash = true;
            
            while (remainingLength > 0)
            {
                double currentSegmentLength = Math.Min(remainingLength, drawDash ? dashLength : gapLength);
                Point3d nextPoint = currentPoint + direction * currentSegmentLength;
                
                if (drawDash)
                {
                    // 绘制实线段
                    draw.Geometry.WorldLine(currentPoint, nextPoint);
                }
                
                currentPoint = nextPoint;
                remainingLength -= currentSegmentLength;
                drawDash = !drawDash;
            }
        }
    }
}
