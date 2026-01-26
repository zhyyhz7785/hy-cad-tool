using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.HelpClass.Jig;
using System;
namespace HyCADTool.Tools
{
    public static partial class ZTools
    {
        //public static (Polyline Polyline, Point3d ClosestPoint, double param)? GetPolylineInfo()
        //{
        //    Document doc = Application.DocumentManager.MdiActiveDocument;
        //    Database db = doc.Database;
        //    Editor ed = doc.Editor;
        //    try
        //    {
        //        // 1. 提示用户选择一个多段线对象
        //        PromptEntityOptions peo = new PromptEntityOptions("\n请选择一个多段线：");
        //        peo.SetRejectMessage("\n请选择一个多段线对象。");
        //        peo.AddAllowedClass(typeof(Polyline), true);
        //        PromptEntityResult per = ed.GetEntity(peo);
        //        if (per.Status != PromptStatus.OK)
        //            return null;
        //        ObjectId plId = per.ObjectId;
        //        Point3d selPt = per.PickedPoint;
        //        using (Transaction tr = db.TransactionManager.StartTransaction())
        //        {
        //            // 获取多段线对象
        //            Polyline pl = tr.GetObject(plId, OpenMode.ForRead) as Polyline;
        //            if (pl == null)
        //                throw new InvalidOperationException("选择的对象不是有效的多段线。");
        //            // 获取选择点在多段线上的最近点
        //            Point3d closestPoint = pl.GetClosestPointTo(selPt, false);
        //            // 获取最近点对应的线段索引
        //            double param = pl.GetParameterAtPoint(closestPoint);             
        //            // 打印多段线信息
        //            //ed.WriteMessage("\n多段线详细信息：");
        //            //ed.WriteMessage($"\n  顶点数: {pl.NumberOfVertices}");
        //            //ed.WriteMessage($"\n  是否闭合: {pl.Closed}");
        //       //ed.WriteMessage($"\n  最近点: ({closestPoint.X}, {closestPoint.Y}, {closestPoint.Z})");
        //            //ed.WriteMessage($"\n  最近点所在段索引: {segmentIndex}");
        //            // 提交事务并返回结果
        //            tr.Commit();
        //            return (pl, closestPoint, param);
        //        }
        //    }
        //    catch (System.Exception ex)
        //    {
        //        ed.WriteMessage($"\n出错：{ex.Message}");
        //        return null;
        //    }
        //}
        /// <summary>
        /// 获取用户选择的多段线信息，包括最近点、参数值和选中的线段。
        /// 用户可以通过右键点击或按 ESC 键随时取消选择。
        /// </summary>
        /// <param name="peoString">提示字符串</param>
        /// <returns>
        /// 如果选择成功，返回包含 Polyline、最近点、参数值和选中线段的元组；
        /// 如果用户取消选择，返回 null。
        /// </returns>
        public static (Polyline Polyline, Point3d ClosestPoint, double Parameter, LineSegment3d SelectedSegment)?
            GetPolylineInfo(string peoString)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Database db = doc.Database;
            Editor ed = doc.Editor;
            try
            {
                // 创建选择选项，并提示用户
                PromptEntityOptions peo = new PromptEntityOptions($"\n{peoString}");
                peo.SetRejectMessage("\n请选择一个多段线对象。");
                peo.AddAllowedClass(typeof(Polyline), true);
                peo.AllowNone = true; // 允许用户取消选择
                                      // 获取用户输入
                PromptEntityResult per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK)
                {
                    // 用户取消了选择
                    return null;
                }
                ObjectId plId = per.ObjectId;
                Point3d selPt = per.PickedPoint;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    // 获取多段线对象
                    Polyline pl = tr.GetObject(plId, OpenMode.ForRead) as Polyline;
                    if (pl == null)
                    {
                        ed.WriteMessage("\n选择的对象不是有效的多段线。");
                        return null;
                    }
                    // 获取选择点在多段线上的最近点
                    Point3d closestPoint = pl.GetClosestPointTo(selPt, false);
                    // 获取最近点对应的参数值
                    double parameter = pl.GetParameterAtPoint(closestPoint);
                    // 获取所在段的索引
                    int segmentIndex = pl.GetSegmentIndexAtParameter(parameter);
                    // 构建选中的段
                    var selectedSegment = pl.GetLineSegmentAt(segmentIndex);
                    tr.Commit();
                    return (pl, closestPoint, parameter, selectedSegment);
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n出错：{ex.Message}");
                return null;
            }
        }
        public static int GetSegmentIndexAtParameter(this Polyline pl, double parameter)
        {
            if (parameter < 0 || parameter > pl.NumberOfVertices)
            {
                throw new ArgumentOutOfRangeException(nameof(parameter), "参数值超出多段线范围。");
            }
            int index = (int)Math.Floor(parameter);
            // 处理闭合多段线的特殊情况
            //if (index == pl.NumberOfVertices && pl.Closed)
            //{
            //    index = pl.NumberOfVertices - 1;
            //}
            if (index >= 0 && index < pl.NumberOfVertices)
            {
                return index;
            }
            throw new InvalidOperationException("无法确定点所属的段索引。");
        }
        /// <summary>
        /// 判断最近点（ClosestPoint）是否靠近线段（LineSegment3d）的正方向（即更靠近终点而非起点）。
        /// </summary>
        /// <param name="SelectedSegment">输入的三维线段对象。</param>
        /// <param name="ClosestPoint">给定的最近点，需要判断其相对位置。</param>
        /// <returns>
        /// 如果最近点距离线段的起点比距离终点更远，则返回 true，表示方向为正方向；
        /// 否则返回 false，表示方向为负方向。
        /// </returns>
        public static bool IsClosestPointDirectionPositive(this LineSegment3d SelectedSegment, Point3d ClosestPoint)
        {
            // 计算最近点到线段起点的距离
            double distToStart = ClosestPoint.DistanceTo(SelectedSegment.StartPoint);
            // 计算最近点到线段终点的距离
            double distToEnd = ClosestPoint.DistanceTo(SelectedSegment.EndPoint);
            // 如果最近点到起点的距离大于到终点的距离，说明最近点更靠近正方向
            return distToStart > distToEnd;
        }
        public static void AddAnchor(this Polyline polyline, Transaction tr, bool isVertical, double hookLength)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            // 确保多段线在事务中被打开
            Polyline pline = tr.GetObject(polyline.ObjectId, OpenMode.ForWrite) as Polyline;
            if (pline == null)
            {
                ed.WriteMessage("\n无法访问多段线对象。\n");
                return;
            }
            // 创建 Jig
            HookJig jig = new HookJig(pline, hookLength, isVertical);
            // 运行 Jig
            PromptResult pr = ed.Drag(jig);
            if (pr.Status == PromptStatus.OK)
            {
                // 如果成功，直接返回               
                return;
            }
            else
            {
                ed.WriteMessage("\n操作已取消或失败。\n");
            }
        }
        public static void AddAnchor(this Polyline polyline, Transaction tr, LineSegment3d seg, double hookLength)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            // 确保多段线在事务中被打开
            Polyline pline = tr.GetObject(polyline.ObjectId, OpenMode.ForWrite) as Polyline;
            if (pline == null)
            {
                ed.WriteMessage("\n无法访问多段线对象。\n");
                return;
            }
            // 创建 Jig
            HookJigSeg jig = new HookJigSeg(pline, hookLength, seg);
            // 运行 Jig
            PromptResult pr = ed.Drag(jig);
            if (pr.Status == PromptStatus.OK)
            {
                // 如果成功，直接返回               
                return;
            }
            else
            {
                ed.WriteMessage("\n操作已取消或失败。\n");
            }
        }
        public static void AddAnchor(this Polyline polyline, Transaction tr, double hookLength)
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            // 获取Polyline的数据库
            Database db = doc.Database;
            // 确保Polyline不是空的
            if (polyline.NumberOfVertices < 2)
                return;
            // 获取起点和终点
            Point3d startPoint = polyline.GetPoint3dAt(0);
            Point3d endPoint = polyline.GetPoint3dAt(polyline.NumberOfVertices - 1);
            // 计算起点和终点的方向向量
            Vector3d startDir = polyline.GetFirstDerivative(0).GetNormal();
            Vector3d endDir = polyline.GetFirstDerivative(polyline.NumberOfVertices - 1).GetNormal();
            // 计算45度角向量
            Vector3d startHookDir = startDir.RotateBy(Math.PI / 4.0, Vector3d.ZAxis); // 45度顺时针旋转
            Vector3d endHookDir = endDir.RotateBy(3 * Math.PI / 4.0, Vector3d.ZAxis); // 45度逆时针旋转            
                                                                                      // 添加起点钩子
            Point3d startHookEnd = startPoint + startHookDir * hookLength;
            int startIndex = 0; // 我们将在起点处插入
            polyline.AddVertexAt(startIndex, new Point2d(startHookEnd.X, startHookEnd.Y), 0, 0, 0);
            // 这里我们不需要再次添加起点，因为它已经在 polyline 中了
            // 添加终点钩子
            Point3d endHookEnd = endPoint + endHookDir * hookLength;
            int endIndex = polyline.NumberOfVertices;
            polyline.AddVertexAt(endIndex, new Point2d(endHookEnd.X, endHookEnd.Y), 0, 0, 0);
            // 我们需要在钩子的末端添加原始的终点
            polyline.AddVertexAt(endIndex + 1, new Point2d(endPoint.X, endPoint.Y), 0, 0, 0);
        }
        // 辅助函数：找到包含指定点的段索引
        public static int FindSegmentIndex(this Polyline pl, Point3d pt)
        {
            // 确保点在多段线的几何范围内
            Point3d closestPoint = pl.GetClosestPointTo(pt, false);
            if (!pt.IsEqualTo(closestPoint, Tolerance.Global))
            {
                throw new ArgumentException("点不在多段线范围内。");
            }
            // 获取点在多段线上的参数值
            double param = pl.GetParameterAtPoint(pt);
            // 验证参数值是否在有效范围内
            if (param < 0 || param > pl.NumberOfVertices)
            {
                throw new InvalidOperationException("点的参数值超出多段线范围。");
            }
            // 计算段索引
            int idx = (int)Math.Floor(param);
            // 确保索引在有效范围内
            if (idx >= 0 && idx <= pl.NumberOfVertices - 1)
            {
                return idx;
            }
            // 处理闭合段索引
            //if (pl.Closed && idx == pl.NumberOfVertices - 1)
            //{
            //    return idx; // 返回最后一段的索引，不再归为 0
            //}
            // 索引无效，返回 -1 或抛出异常
            throw new InvalidOperationException("无法确定点所属的段索引。");
        }
        /// <summary>
        /// 打断封闭的多段线，从指定的断点生成新的多段线。
        /// </summary>
        /// <param name="polyline">原始的封闭多段线</param>
        /// <param name="breakPoint">断点（必须是多段线的节点）</param>
        /// <param name="trans">当前事务</param>
        /// <returns>生成的新多段线</returns>
        public static Polyline BreakClosedPoly(this Polyline polyline, int breakIndex)
        {
            // 获取多段线的顶点数量
            int vertexCount = polyline.NumberOfVertices;
            // 创建新的多段线
            Polyline newPolyline = new Polyline();
            int newVertexIndex = 0;
            // 从断点开始构建新的多段线
            for (int i = 0; i < vertexCount + 1; i++)
            {
                int currentIndex = (breakIndex + i) % vertexCount; // 循环遍历
                Point3d currentPoint = polyline.GetPoint3dAt(currentIndex);
                newPolyline.AddVertexAt(newVertexIndex, currentPoint.ConvertPoint3dTo2d(), 0, 0, 0);
                newVertexIndex++;
            }
            return newPolyline;
        }
        public static void BreakPoly()
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            var doc = Application.DocumentManager.MdiActiveDocument;
            var pl = ZTools.SelectAEntity<Polyline>(db);
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                // 获取选中的多段线
                Polyline oldPolyline = (Polyline)trans.GetObject(pl.ObjectId, OpenMode.ForWrite);
                var newPolyline = oldPolyline.BreakClosedPoly(3);
                BlockTableRecord btr = (BlockTableRecord)trans.GetObject(oldPolyline.OwnerId, OpenMode.ForWrite);
                btr.AppendEntity(newPolyline);
                trans.AddNewlyCreatedDBObject(newPolyline, true);
                // 删除旧的多段线
                oldPolyline.Erase();
                // 提交事务
                trans.Commit();
                ed.WriteMessage("\n已生成新的多段线，并删除旧的多段线！");
            }
        }
    }
}
