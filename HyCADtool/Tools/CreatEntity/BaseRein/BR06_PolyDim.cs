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
        public static void Poly4Dim()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            // 用户选择一条轴线
            var axisLine = SelectAxisLine();
            if (axisLine == null)
            {
                ed.WriteMessage("\n未选择轴线。操作取消。");
                return;
            }
            // 获取轴线图层中的所有直线
            var axisLayerLines = GetLinesFromLayer(axisLine.Layer);
            if (axisLayerLines == null || axisLayerLines.Count == 0)
            {
                ed.WriteMessage("\n未找到轴线图层中的直线。操作取消。");
                return;
            }
            // 用户选择一个代表配筋区域的Polyline
            var reinforcementPolyline = SelectReinforcementPolyline();
            if (reinforcementPolyline == null)
            {
                ed.WriteMessage("\n未选择配筋区域Polyline。操作取消。");
                return;
            }
            // 获取配筋区域图层中的所有正四边形Polyline
            var reinforcementLayerPolylines = GetRectangularPolylinesFromLayer(reinforcementPolyline.Layer);
            if (reinforcementLayerPolylines == null || reinforcementLayerPolylines.Count == 0)
            {
                ed.WriteMessage("\n未找到配筋区域图层中的正四边形Polyline。操作取消。");
                return;
            }
            var lines = new List<Line>(axisLayerLines);
            var polylines = new List<Polyline>(reinforcementLayerPolylines);
            //调用扩展方法调整Polyline的点顺序
            using (DocumentLock docLock = doc.LockDocument())
            {
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    foreach (var polylineId in polylines.Select(p => p.ObjectId))
                    {
                        var polyline = tr.GetObject(polylineId, OpenMode.ForWrite) as Polyline;
                        if (polyline != null)
                        {
                            polyline.ResetPolyVertex();
                        }
                    }
                    tr.Commit();
                }
            }
            // 后续逻辑不变
            if (InterDirection == IntersectionsDirection.LeftRight)
            {
                var dic = GetPolylineIntersectionsLeftRight(lines, polylines, InterDirection, AxisExtend);
                DeletePolylinesInCad(dic);
                dic = dic.AdjustPolylinesLeftRight(Interval);
                Et.SetCurrentLayer("00_hy_筏板附加配筋x_标注");
                AnnotatePolylineIntersectionsLeftRight(dic, 100, DimensionDistanceWithDim, InterDirection);
            }
            else
            {
                var dic = GetPolylineIntersectionsUpDown(lines, polylines, InterDirection, AxisExtend);
                DeletePolylinesInCad(dic);
                dic = dic.AdjustPolylinesUpDown(Interval);
                Et.SetCurrentLayer("00_hy_筏板附加配筋y_标注");
                AnnotatePolylineIntersectionsUpDown(dic, 100, DimensionDistanceWithDim, InterDirection);
            }
        }
        #region 选择轴线和配筋区域
        private static Line SelectAxisLine()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            PromptEntityOptions peo = new PromptEntityOptions("\n请选择一条轴线:");
            peo.SetRejectMessage("\n选择的对象必须是直线。");
            peo.AddAllowedClass(typeof(Line), true);
            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK)
                return null;
            Line axisLine;
            using (Transaction tr = HostApplicationServices.WorkingDatabase.TransactionManager.StartTransaction())
            {
                axisLine = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Line;
                tr.Commit();
            }
            return axisLine;
        }
        private static Polyline SelectReinforcementPolyline()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            PromptEntityOptions peo = new PromptEntityOptions("\n请选择代表配筋区域的Polyline:");
            peo.SetRejectMessage("\n选择的对象必须是Polyline。");
            peo.AddAllowedClass(typeof(Polyline), true);
            PromptEntityResult per = ed.GetEntity(peo);
            if (per.Status != PromptStatus.OK)
                return null;
            Polyline reinforcementPolyline;
            using (Transaction tr = HostApplicationServices.WorkingDatabase.TransactionManager.StartTransaction())
            {
                reinforcementPolyline = tr.GetObject(per.ObjectId, OpenMode.ForRead) as Polyline;
                ReinforcementPolylineLayerId = reinforcementPolyline.LayerId;
                tr.Commit();
            }
            return reinforcementPolyline;
        }
        public static void ReorderVertices(this Polyline polyline, Transaction tr)
        {
            // 获取所有顶点
            int numVertices = polyline.NumberOfVertices;
            var vertices = new List<Point2d>();
            for (int i = 0; i < numVertices; i++)
            {
                vertices.Add(polyline.GetPoint2dAt(i));
            }
            // 找到 x 最小、y 最小的点
            Point2d startPoint = vertices.OrderBy(v => v.X).ThenBy(v => v.Y).First();
            // 重新排序顶点，使得 startPoint 为第一个顶点，逆时针排序
            var sortedVertices = new List<Point2d>();
            int startIndex = vertices.IndexOf(startPoint);
            for (int i = 0; i < numVertices; i++)
            {
                sortedVertices.Add(vertices[(startIndex + i) % numVertices]);
            }
            // 重新创建一个新的 Polyline，并按新的顺序添加顶点
            var newPolyline = new Polyline();
            for (int i = 0; i < sortedVertices.Count; i++)
            {
                newPolyline.AddVertexAt(i, sortedVertices[i], 0, 0, 0);
            }
            // 复制原来的 Polyline 的属性
            newPolyline.Layer = polyline.Layer;
            newPolyline.Color = polyline.Color;
            newPolyline.Linetype = polyline.Linetype;
            newPolyline.LineWeight = polyline.LineWeight;
            newPolyline.Normal = polyline.Normal;
            // 将新的 Polyline 添加到数据库
            var btr = (BlockTableRecord)tr.GetObject(polyline.BlockId, OpenMode.ForWrite);
            btr.AppendEntity(newPolyline);
            tr.AddNewlyCreatedDBObject(newPolyline, true);
            // 删除原来的 Polyline
            polyline.UpgradeOpen();
            polyline.Erase();
        }
        private static List<Line> GetLinesFromLayer(string layerName)
        {
            var lines = new List<Line>();
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            // 定义选择过滤器
            TypedValue[] filter = new TypedValue[]
            {
             new TypedValue((int)DxfCode.LayerName, layerName),
             new TypedValue((int)DxfCode.Start, "LINE")
            };
            // 创建选择过滤器
            SelectionFilter selectionFilter = new SelectionFilter(filter);
            PromptSelectionOptions opts = new PromptSelectionOptions();
            opts.MessageForAdding = "\n请选择图层为 " + layerName + " 的直线:";
            // 获取选择集
            PromptSelectionResult res = ed.GetSelection(opts, selectionFilter);
            if (res.Status == PromptStatus.OK)
            {
                SelectionSet selSet = res.Value;
                using (Transaction tr = Application.DocumentManager.MdiActiveDocument.TransactionManager.StartTransaction())
                {
                    foreach (SelectedObject selObj in selSet)
                    {
                        if (selObj != null)
                        {
                            Line line = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Line;
                            if (line != null)
                            {
                                lines.Add(line);
                            }
                        }
                    }
                    tr.Commit();
                }
            }
            return lines;
        }
        private static List<Polyline> GetRectangularPolylinesFromLayer(string layerName)
        {
            var polylines = new List<Polyline>();
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            // 定义选择过滤器
            TypedValue[] filter = new TypedValue[]
            {
            new TypedValue((int)DxfCode.LayerName, layerName),
            new TypedValue((int)DxfCode.Start, "LWPOLYLINE")
            };
            // 创建选择过滤器
            SelectionFilter selectionFilter = new SelectionFilter(filter);
            PromptSelectionOptions opts = new PromptSelectionOptions();
            opts.MessageForAdding = "\n请选择图层为 " + layerName + " 的多段线:";
            // 获取选择集
            PromptSelectionResult res = ed.GetSelection(opts, selectionFilter);
            if (res.Status == PromptStatus.OK)
            {
                SelectionSet selSet = res.Value;
                using (Transaction tr = Application.DocumentManager.MdiActiveDocument.TransactionManager.StartTransaction())
                {
                    foreach (SelectedObject selObj in selSet)
                    {
                        if (selObj != null)
                        {
                            Polyline polyline = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Polyline;
                            if (polyline != null && IsRectangularPolyline(polyline))
                            {
                                polylines.Add(polyline);
                            }
                        }
                    }
                    tr.Commit();
                }
            }
            return polylines;
        }
        /// <summary>
        /// 判断给定的多段线是否为正四边形（每条边相邻且垂直）。
        /// </summary>
        /// <param name="polyline">要检查的多段线。</param>
        /// <returns>如果多段线为正四边形，则返回 true；否则返回 false。</returns>
        private static bool IsRectangularPolyline(Polyline polyline)
        {
            // 检查多段线是否有4个顶点且是闭合的
            if (polyline.NumberOfVertices != 4 || !polyline.Closed)
                return false;
            // 遍历多段线的每一条边
            for (int i = 0; i < 4; i++)
            {
                // 获取当前边的线段
                var segment = polyline.GetLineSegmentAt(i);
                // 获取下一条边的线段，使用取模操作确保索引在0-3之间循环
                var nextSegment = polyline.GetLineSegmentAt((i + 1) % 4);
                // 检查当前边和下一条边是否垂直
                if (!segment.Direction.IsPerpendicularTo(nextSegment.Direction))
                    return false;
            }
            // 如果所有边都满足垂直条件，则返回true
            return true;
        }
        #endregion
    }
}
