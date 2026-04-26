using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Shell.Contracts;
using HyCADTool.Shell.Configuration.User;
using HyCADTool.Shared.AutoCAD.Configuration;
using HyCADTool.Shared.AutoCAD.Extensions;
using HyCADTool.Shared.AutoCAD.Services;
using HyCADTool.Shared.AutoCAD.Interactive;
using HyCADTool.Shared.AutoCAD.Utilities;
using HyCADTool.App.Bootstrap;
using HyCADTool.Presentation.ViewModels;
using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Reinforcement
{
    /// <summary>
    /// 延伸钢筋命令（对应旧命令 ge）
    /// 流程：
    ///   1. 选择钢筋的非弯钩段 → 删除该端弯钩
    ///   2. 射线求交找最近钢筋线 → 计算延伸点 a = 交点 - 保护层厚度
    ///   3. 添加点 a 到多段线末端
    ///   4. HookJig 实时预览 15d 垂直弯折方向（用户选左/右）→ 确认
    /// </summary>
    public class ReinExtendCommand
    {
        private readonly ILayerService _layerService;

        private static string LayerLineRein => UserLayerNameResolver.Get(LayerSemanticIds.ReinLine, LayerBuiltinDefaults.ReinLine);

        public ReinExtendCommand()
        {
            _layerService = ServiceLocator.Resolve<ILayerService>();
        }

        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var vm = SettingsPanelViewModel.Current;
            double scale = vm?.Scale ?? 40.0;
            double protectionThickness = (vm?.ProtectionThickness ?? 1.0) * scale; // 绿色参数 × Scale
            double rebarDiameter = vm?.RebarDiameter ?? 14.0;                      // 红色参数，直接 mm
            double hookLength15d = 15.0 * rebarDiameter;                           // 15d = 210mm
            double reinWidth = (vm?.PolylineWidth ?? 0.4) * scale;

            #region agent log
            AgentDebugLogger.Log("initial", "H1", "ReinExtendCommand.Execute", "ge width parameters",
                new
                {
                    hasViewModel = vm != null,
                    scale,
                    polylineWidth = vm?.PolylineWidth,
                    protectionThickness,
                    rebarDiameter,
                    hookLength15d,
                    reinWidth
                });
            #endregion

            // 确保样式已同步
            vm?.EnsureStylesApplied();

            try
            {
                // 1. 选择钢筋多段线（非弯钩段）
                var peo = new PromptEntityOptions("\n请选择钢筋多段线（点击非弯钩段）：");
                peo.SetRejectMessage("\n请选择一个多段线对象。");
                peo.AddAllowedClass(typeof(Polyline), true);
                var per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) return;

                using (var trans = db.TransactionManager.StartTransaction())
                {
                    var poly = trans.GetObject(per.ObjectId, OpenMode.ForWrite) as Polyline;
                    if (poly == null || poly.NumberOfVertices < 3) return;

                    // 2. 判断靠近哪端
                    Point3d closestPt = poly.GetClosestPointTo(per.PickedPoint, false);
                    double param = poly.GetParameterAtPoint(closestPt);
                    int segIndex = Math.Min((int)Math.Floor(param), poly.NumberOfVertices - 2);
                    bool nearStart = segIndex < poly.NumberOfVertices / 2;

                    // 3. 删除弯钩顶点
                    if (nearStart)
                        poly.RemoveVertexAt(0);
                    else
                        poly.RemoveVertexAt(poly.NumberOfVertices - 1);

                    if (poly.NumberOfVertices < 2) { trans.Abort(); return; }

                    // 4. 计算延伸方向（删除弯钩后的端点 → 向外）
                    int endIdx = nearStart ? 0 : poly.NumberOfVertices - 1;
                    int adjIdx = nearStart ? 1 : poly.NumberOfVertices - 2;
                    Point3d endPt = poly.GetPoint3dAt(endIdx);
                    Point3d adjPt = poly.GetPoint3dAt(adjIdx);
                    Vector3d rawDir = endPt - adjPt;
                    if (rawDir.Length < 1e-6)
                    {
                        ed.WriteMessage("\n端点与相邻点重合，无法计算方向。");
                        trans.Abort();
                        return;
                    }
                    Vector3d extDir = rawDir.GetNormal();

                    // 5. 射线求交：找同图层最近钢筋线（含自身非相邻段）
                    Point3d hitPt = FindNearestReinIntersection(db, trans, endPt, extDir, per.ObjectId, poly, endIdx);
                    if (hitPt == Point3d.Origin)
                    {
                        ed.WriteMessage("\n未找到射线方向上的钢筋线。");
                        trans.Abort();
                        return;
                    }

                    double rawDist = endPt.DistanceTo(hitPt);
                    double extDist = rawDist - protectionThickness;
                    if (extDist <= 0)
                    {
                        ed.WriteMessage("\n距离不足，无法延伸。");
                        trans.Abort();
                        return;
                    }

                    // 6. 计算点 a（延伸终点 = 交点 - 保护层）
                    Point3d pointA = endPt + extDir * extDist;

                    // 7. 确保延伸端在多段线末尾（HookJig 在末端操作）
                    if (nearStart)
                        poly.ReverseCurve();

                    // 8. 在末尾添加点 a
                    poly.AddVertexAt(poly.NumberOfVertices,
                        new Point2d(pointA.X, pointA.Y), 0, 0, 0);

                    // 9. HookJig 实时预览 15d 垂直弯折（isVertical=true → 90°/270°）
                    //    用户移动光标选择弯折方向（左/右），点击确认
                    var jig = new HookJig(poly, hookLength15d, true);
                    var pr = ed.Drag(jig);

                    if (pr.Status == PromptStatus.OK)
                    {
                        poly.ApplyReinforcementWidth(reinWidth);
                        trans.Commit();
                    }
                    // 用户取消 → 事务自动回滚，恢复原始多段线
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}");
            }
        }

        // ================================================================
        //  射线求交
        // ================================================================

        /// <summary>
        /// 在钢筋图层上发射射线，找到最近的钢筋交点（含自身非相邻段 + 其他钢筋线）
        /// </summary>
        /// <param name="selfId">当前多段线 ObjectId（自身求交时逐段检查）</param>
        /// <param name="selfPoly">当前多段线（已删除弯钩后的状态）</param>
        /// <param name="extEndIdx">延伸端顶点索引，相邻线段会被排除</param>
        private static Point3d FindNearestReinIntersection(
            Database db, Transaction trans,
            Point3d origin, Vector3d direction,
            ObjectId selfId, Polyline selfPoly, int extEndIdx)
        {
            Point3d nearest = Point3d.Origin;
            double nearestDist = double.MaxValue;

            var ray = new Line(origin, origin + direction * 1e8);

            try
            {
                // --- 1. 自身求交（逐段检查，跳过延伸端相邻线段） ---
                CheckSelfIntersection(ray, selfPoly, extEndIdx, origin, direction,
                    ref nearest, ref nearestDist);

                // --- 2. 其他钢筋求交 ---
                var bt = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

                foreach (ObjectId id in btr)
                {
                    if (id == selfId) continue; // 自身已在上面处理

                    var ent = trans.GetObject(id, OpenMode.ForRead) as Polyline;
                    if (ent == null) continue;
                    if (ent.Layer != LayerLineRein) continue;

                    var pts = new Point3dCollection();
                    ray.IntersectWith(ent, Intersect.ExtendThis, pts, IntPtr.Zero, IntPtr.Zero);

                    foreach (Point3d pt in pts)
                    {
                        Vector3d toHit = pt - origin;
                        if (toHit.DotProduct(direction) <= 0) continue;

                        double dist = origin.DistanceTo(pt);
                        if (dist < nearestDist && dist > 1e-6)
                        {
                            nearest = pt;
                            nearestDist = dist;
                        }
                    }
                }
            }
            finally
            {
                ray.Dispose();
            }

            return nearest;
        }

        /// <summary>
        /// 自身求交：逐段与射线求交，跳过延伸端直接相邻的线段（避免误判）
        /// </summary>
        private static void CheckSelfIntersection(
            Line ray, Polyline poly, int extEndIdx,
            Point3d origin, Vector3d direction,
            ref Point3d nearest, ref double nearestDist)
        {
            int numSegs = poly.NumberOfVertices - 1;
            if (numSegs < 2) return; // 少于2段不可能自交

            // 延伸端相邻线段索引（需跳过）
            // extEndIdx=0 → 跳过 seg 0
            // extEndIdx=last → 跳过 seg last-1
            int skipA = extEndIdx > 0 ? extEndIdx - 1 : -1;
            int skipB = extEndIdx < numSegs ? extEndIdx : -1;

            for (int i = 0; i < numSegs; i++)
            {
                if (i == skipA || i == skipB) continue;

                Point3d sp = poly.GetPoint3dAt(i);
                Point3d ep = poly.GetPoint3dAt(i + 1);
                var seg = new Line(sp, ep);

                try
                {
                    var pts = new Point3dCollection();
                    ray.IntersectWith(seg, Intersect.OnBothOperands, pts, IntPtr.Zero, IntPtr.Zero);

                    foreach (Point3d pt in pts)
                    {
                        Vector3d toHit = pt - origin;
                        if (toHit.DotProduct(direction) <= 0) continue;

                        double dist = origin.DistanceTo(pt);
                        if (dist < nearestDist && dist > 1e-6)
                        {
                            nearest = pt;
                            nearestDist = dist;
                        }
                    }
                }
                finally
                {
                    seg.Dispose();
                }
            }
        }

        // ================================================================
        //  共享静态方法（供 ge1 等命令复用）
        // ================================================================

        /// <summary>
        /// 延伸多段线某段，沿线段方向移动该端及之后/之前的所有顶点
        /// </summary>
        internal static void ExtendSegment(Polyline poly, int segIndex, Point3d clickPt, double distance)
        {
            if (distance <= 0) return;
            if (segIndex < 0 || segIndex >= poly.NumberOfVertices - 1) return;

            Point3d p1 = poly.GetPoint3dAt(segIndex);
            Point3d p2 = poly.GetPoint3dAt(segIndex + 1);

            bool forward = clickPt.DistanceTo(p1) > clickPt.DistanceTo(p2);

            Vector3d vec = forward
                ? (p2 - p1).GetNormal() * distance
                : (p1 - p2).GetNormal() * distance;

            if (forward)
            {
                for (int i = segIndex + 1; i < poly.NumberOfVertices; i++)
                {
                    Point3d pt = poly.GetPoint3dAt(i);
                    poly.SetPointAt(i, new Point2d(pt.X + vec.X, pt.Y + vec.Y));
                }
            }
            else
            {
                for (int i = segIndex; i >= 0; i--)
                {
                    Point3d pt = poly.GetPoint3dAt(i);
                    poly.SetPointAt(i, new Point2d(pt.X + vec.X, pt.Y + vec.Y));
                }
            }
        }

        /// <summary>
        /// 延伸多段线某段至边界多段线，再减去指定距离
        /// </summary>
        internal static bool ExtendSegmentToBoundary(Polyline poly, int segIndex, Point3d clickPt, Polyline boundary, double reduceDistance)
        {
            if (segIndex < 0 || segIndex >= poly.NumberOfVertices - 1) return false;

            Point3d p1 = poly.GetPoint3dAt(segIndex);
            Point3d p2 = poly.GetPoint3dAt(segIndex + 1);

            bool forward = clickPt.DistanceTo(p1) > clickPt.DistanceTo(p2);
            Point3d extensionPt = forward ? p2 : p1;
            Vector3d extensionDir = forward ? (p2 - p1).GetNormal() : (p1 - p2).GetNormal();

            Point3d boundaryPt = FindClosestBoundaryPoint(extensionPt, extensionDir, boundary);
            if (boundaryPt == Point3d.Origin) return false;

            double extensionDistance = extensionPt.DistanceTo(boundaryPt) - reduceDistance;
            if (extensionDistance <= 0) return false;

            Vector3d vec = extensionDir * extensionDistance;

            if (forward)
            {
                for (int i = segIndex + 1; i < poly.NumberOfVertices; i++)
                {
                    Point3d pt = poly.GetPoint3dAt(i);
                    poly.SetPointAt(i, new Point2d(pt.X + vec.X, pt.Y + vec.Y));
                }
            }
            else
            {
                for (int i = segIndex; i >= 0; i--)
                {
                    Point3d pt = poly.GetPoint3dAt(i);
                    poly.SetPointAt(i, new Point2d(pt.X + vec.X, pt.Y + vec.Y));
                }
            }
            return true;
        }

        /// <summary>
        /// 沿延伸方向找到与边界多段线最近的交点
        /// </summary>
        internal static Point3d FindClosestBoundaryPoint(Point3d startPt, Vector3d direction, Polyline boundary)
        {
            var ray = new Line(startPt, startPt + direction * 1e8);
            Point3d closest = Point3d.Origin;
            double closestDist = double.MaxValue;

            try
            {
                for (int i = 0; i < boundary.NumberOfVertices; i++)
                {
                    int next = (i + 1) % boundary.NumberOfVertices;
                    if (next == i) continue;

                    Point3d bp1 = boundary.GetPoint3dAt(i);
                    Point3d bp2 = boundary.GetPoint3dAt(next);
                    var seg = new Line(bp1, bp2);

                    var intersections = new Point3dCollection();
                    ray.IntersectWith(seg, Intersect.OnBothOperands, intersections, IntPtr.Zero, IntPtr.Zero);

                    foreach (Point3d pt in intersections)
                    {
                        double dist = startPt.DistanceTo(pt);
                        if (dist < closestDist)
                        {
                            closest = pt;
                            closestDist = dist;
                        }
                    }

                    seg.Dispose();
                }
            }
            finally
            {
                ray.Dispose();
            }

            return closest;
        }
    }
}
