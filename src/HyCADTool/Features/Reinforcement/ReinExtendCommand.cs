using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Presentation.ViewModels;
using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.Reinforcement
{
    /// <summary>
    /// 延伸钢筋命令（对应旧命令 ge）。
    /// 规则：
    ///   1. 把钢筋（Polyline）按“锚固长度”延长；
    ///   2. 用户点击靠近哪一端，就延长哪一端；
    ///   3. 弯钩（直弯钩 / 斜弯钩）保持原样——延长时把弯钩随主筋末端整体平移，
    ///      主筋末段加长 = 锚固长度，弯钩形状、角度、线宽不变。
    /// </summary>
    public class ReinExtendCommand
    {
        public void Execute()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            var vm = SettingsPanelViewModel.Current;
            double anchorageLength = vm?.AnchorageLength ?? 500.0; // 红色参数：直接 mm，延长长度 = 锚固长度
            if (anchorageLength <= 0)
            {
                ed.WriteMessage("\n锚固长度无效（需 > 0）。");
                return;
            }

            // 确保样式已同步
            vm?.EnsureStylesApplied();

            try
            {
                // 1. 选择钢筋多段线（点击靠近要延长的一端）
                var peo = new PromptEntityOptions("\n请选择钢筋多段线（点击靠近要延长的一端）：");
                peo.SetRejectMessage("\n请选择一个多段线对象。");
                peo.AddAllowedClass(typeof(Polyline), true);
                var per = ed.GetEntity(peo);
                if (per.Status != PromptStatus.OK) return;

                using (var trans = db.TransactionManager.StartTransaction())
                {
                    var poly = trans.GetObject(per.ObjectId, OpenMode.ForWrite) as Polyline;
                    if (poly == null || poly.NumberOfVertices < 2) { trans.Abort(); return; }

                    // 2. 按点击位置决定延长哪一端（不反转库内多段线）
                    bool extendFromStart = IsPickedNearStart(poly, per.PickedPoint);

                    // 3. 识别弯钩跨度 + 主筋外伸方向（弯钩随主筋整体平移，原样保留）
                    if (!TryGetHookSpan(poly, anchorageLength, extendFromStart, out int mainTipIndex, out int mainPrevIndex))
                    {
                        ed.WriteMessage("\n无法识别延长方向（端点重合）。");
                        trans.Abort();
                        return;
                    }

                    Vector3d dir = poly.GetPoint3dAt(mainTipIndex) - poly.GetPoint3dAt(mainPrevIndex);
                    if (dir.Length < 1e-6)
                    {
                        ed.WriteMessage("\n无法识别延长方向（端点重合）。");
                        trans.Abort();
                        return;
                    }

                    Vector3d shift = dir.GetNormal() * anchorageLength;

                    // 4. 平移“主筋末端 + 弯钩”各顶点：主筋末段加长 = 锚固长度，弯钩形状/线宽不变
                    if (extendFromStart)
                    {
                        for (int i = 0; i <= mainTipIndex; i++)
                        {
                            Point3d p = poly.GetPoint3dAt(i);
                            poly.SetPointAt(i, new Point2d(p.X + shift.X, p.Y + shift.Y));
                        }
                    }
                    else
                    {
                        for (int i = mainTipIndex; i < poly.NumberOfVertices; i++)
                        {
                            Point3d p = poly.GetPoint3dAt(i);
                            poly.SetPointAt(i, new Point2d(p.X + shift.X, p.Y + shift.Y));
                        }
                    }

                    trans.Commit();
                }
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}");
            }
        }

        // ================================================================
        //  弯钩识别 + 延长平移
        // ================================================================

        /// <summary>
        /// 识别多段线“末端”主筋锚点与主筋末段起点索引（供 ge 平移弯钩、ge1 删弯钩复用）。
        /// </summary>
        internal static bool TryGetHookSpan(
            Polyline poly, double hookLengthHint, bool fromStart,
            out int mainTipIndex, out int mainPrevIndex)
            => fromStart
                ? TryGetStartHookSpan(poly, hookLengthHint, out mainTipIndex, out mainPrevIndex)
                : TryGetEndHookSpan(poly, hookLengthHint, out mainTipIndex, out mainPrevIndex);

        internal static bool TryGetEndHookSpan(
            Polyline poly, double hookLengthHint,
            out int mainTipIndex, out int mainPrevIndex)
        {
            mainTipIndex = -1;
            mainPrevIndex = -1;

            int n = poly.NumberOfVertices;
            if (n < 2) return false;

            int last = n - 1;
            const double eps = 1e-3;

            if (n >= 4 && poly.GetPoint3dAt(last).DistanceTo(poly.GetPoint3dAt(last - 2)) < eps)
            {
                mainTipIndex = last - 2;
                mainPrevIndex = last - 3;
            }
            else if (n >= 3 && IsHookBend(poly, last, hookLengthHint))
            {
                mainTipIndex = last - 1;
                mainPrevIndex = last - 2;
            }
            else
            {
                mainTipIndex = last;
                mainPrevIndex = last - 1;
            }

            if (mainPrevIndex < 0) return false;
            return true;
        }

        internal static bool TryGetStartHookSpan(
            Polyline poly, double hookLengthHint,
            out int mainTipIndex, out int mainPrevIndex)
        {
            mainTipIndex = -1;
            mainPrevIndex = -1;

            int n = poly.NumberOfVertices;
            if (n < 2) return false;

            const double eps = 1e-3;

            if (n >= 4 && poly.GetPoint3dAt(0).DistanceTo(poly.GetPoint3dAt(2)) < eps)
            {
                mainTipIndex = 2;
                mainPrevIndex = 3;
            }
            else if (n >= 3 && IsHookBendAtStart(poly, hookLengthHint))
            {
                mainTipIndex = 1;
                mainPrevIndex = 2;
            }
            else
            {
                mainTipIndex = 0;
                mainPrevIndex = 1;
            }

            if (mainPrevIndex >= n) return false;
            return true;
        }

        /// <summary>删除末端弯钩顶点，保留至 <paramref name="mainTipIndex"/>。</summary>
        internal static void RemoveEndHookVertices(Polyline poly, int mainTipIndex)
        {
            while (poly.NumberOfVertices > mainTipIndex + 1)
                poly.RemoveVertexAt(poly.NumberOfVertices - 1);
        }

        /// <summary>删除起点弯钩顶点，保留自 <paramref name="mainTipIndex"/> 起。</summary>
        internal static void RemoveStartHookVertices(Polyline poly, int mainTipIndex)
        {
            while (poly.NumberOfVertices > mainTipIndex + 1)
                poly.RemoveVertexAt(0);
        }

        /// <summary>点击位置是否更靠近多段线起点（待操作端在起点侧）。</summary>
        internal static bool IsPickedNearStart(Polyline polyline, Point3d pickedPoint)
        {
            Point3d closest = polyline.GetClosestPointTo(pickedPoint, false);
            return closest.DistanceTo(polyline.StartPoint) <= closest.DistanceTo(polyline.EndPoint);
        }

        /// <summary>按点击位置把待操作端转到多段线末端（会永久反转顶点顺序，仅用于可回滚流程）。</summary>
        [Obsolete("优先使用 IsPickedNearStart + TryGetHookSpan，避免改写库内多段线方向。")]
        internal static void OrientEndToPickedPoint(Polyline polyline, Point3d pickedPoint)
        {
            if (IsPickedNearStart(polyline, pickedPoint))
                polyline.ReverseCurve();
        }

        /// <summary>射线命中结果（含被命中线段方向，供 ge1 偏移与 15d 弯钩）。</summary>
        internal readonly struct ReinRayHit
        {
            public Point3d Point { get; }
            public Point3d SegStart { get; }
            public Point3d SegEnd { get; }
            public Vector3d SegmentDir { get; }

            public ReinRayHit(Point3d point, Point3d segStart, Point3d segEnd, Vector3d segmentDir)
            {
                Point = point;
                SegStart = segStart;
                SegEnd = segEnd;
                SegmentDir = segmentDir;
            }
        }

        /// <summary>
        /// 沿 <paramref name="direction"/> 发射射线，在指定图层多段线上找最近正向交点（含自身，可跳过一段）。
        /// </summary>
        internal static bool FindNearestForwardRayHit(
            Database db, Transaction trans,
            Point3d origin, Vector3d direction,
            ObjectId selfId, Polyline selfPoly, int skipSegmentIndex,
            string targetLayer,
            out ReinRayHit hit)
        {
            hit = default;
            Point3d nearest = Point3d.Origin;
            Point3d segA = Point3d.Origin, segB = Point3d.Origin;
            double nearestDist = double.MaxValue;
            bool found = false;

            TryAccumulateRayHits(selfPoly, skipSegmentIndex, origin, direction,
                ref nearest, ref segA, ref segB, ref nearestDist, ref found);

            var bt = (BlockTable)trans.GetObject(db.BlockTableId, OpenMode.ForRead);
            var btr = (BlockTableRecord)trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForRead);

            foreach (ObjectId id in btr)
            {
                if (id == selfId) continue;
                var ent = trans.GetObject(id, OpenMode.ForRead) as Polyline;
                if (ent == null || ent.NumberOfVertices < 2) continue;
                if (!string.Equals(ent.Layer, targetLayer, StringComparison.OrdinalIgnoreCase)) continue;

                TryAccumulateRayHits(ent, -1, origin, direction,
                    ref nearest, ref segA, ref segB, ref nearestDist, ref found);
            }

            if (!found) return false;

            Vector3d e = segB - segA;
            if (e.Length < 1e-10) e = direction;
            else e = e.GetNormal();

            hit = new ReinRayHit(nearest, segA, segB, e);
            return true;
        }

        /// <summary>
        /// 将相交线段朝“被延伸钢筋来的一侧”偏移保护层厚度，与延伸射线再求交得最终末端。
        /// </summary>
        internal static bool TryGetOffsetExtensionPoint(
            Point3d rayOrigin, Vector3d rayDir,
            ReinRayHit firstHit, double protectionThickness,
            out Point3d extensionPoint)
        {
            extensionPoint = Point3d.Origin;
            Vector3d e = firstHit.SegmentDir;
            if (e.Length < 1e-10) return false;
            e = e.GetNormal();

            Vector3d n = new Vector3d(-e.Y, e.X, 0);
            if (n.Length < 1e-10) return false;
            n = n.GetNormal();
            if ((rayOrigin - firstHit.Point).DotProduct(n) < 0)
                n = -n;

            Point3d q = firstHit.Point + n * protectionThickness;
            if (!TryRayLineIntersection2d(rayOrigin, rayDir, q, e, out double t) || t < 1e-6)
                return false;

            extensionPoint = rayOrigin + rayDir * t;
            return true;
        }

        private static void TryAccumulateRayHits(
            Polyline poly, int skipSegmentIndex,
            Point3d origin, Vector3d direction,
            ref Point3d nearest, ref Point3d segA, ref Point3d segB,
            ref double nearestDist, ref bool found)
        {
            var o2 = new Point2d(origin.X, origin.Y);
            var d2 = new Vector2d(direction.X, direction.Y);
            if (d2.Length < 1e-10) return;
            d2 = d2.GetNormal();

            int numSegs = poly.NumberOfVertices - 1;
            for (int i = 0; i < numSegs; i++)
            {
                if (i == skipSegmentIndex) continue;
                if (poly.GetSegmentType(i) != SegmentType.Line) continue;

                var a = poly.GetPoint2dAt(i);
                var b = poly.GetPoint2dAt(i + 1);
                if (!TryRayHitSegment(o2, d2, a, b, origin.Z, out Point3d pt)) continue;
                if ((pt - origin).DotProduct(direction) <= 0) continue;

                double dist = origin.DistanceTo(pt);
                if (dist < nearestDist && dist > 1e-6)
                {
                    nearest = pt;
                    segA = poly.GetPoint3dAt(i);
                    segB = poly.GetPoint3dAt(i + 1);
                    nearestDist = dist;
                    found = true;
                }
            }
        }

        private static bool TryRayLineIntersection2d(
            Point3d rayOrigin, Vector3d rayDir, Point3d linePoint, Vector3d lineDir,
            out double rayParameter)
        {
            rayParameter = 0;
            double rdx = rayDir.X, rdy = rayDir.Y;
            double ldx = lineDir.X, ldy = lineDir.Y;
            double cross = rdx * ldy - rdy * ldx;
            if (Math.Abs(cross) < 1e-10) return false;

            double ox = linePoint.X - rayOrigin.X;
            double oy = linePoint.Y - rayOrigin.Y;
            rayParameter = (ox * ldy - oy * ldx) / cross;
            return true;
        }

        private static bool TryRayHitSegment(
            Point2d origin, Vector2d dir, Point2d segStart, Point2d segEnd, double z,
            out Point3d hit)
        {
            hit = Point3d.Origin;
            double sx = segEnd.X - segStart.X;
            double sy = segEnd.Y - segStart.Y;
            double cross = dir.X * sy - dir.Y * sx;
            if (Math.Abs(cross) < 1e-10) return false;

            double ox = segStart.X - origin.X;
            double oy = segStart.Y - origin.Y;
            double t = (ox * sy - oy * sx) / cross;
            double u = (ox * dir.Y - oy * dir.X) / cross;
            if (t < 1e-6 || u < -1e-6 || u > 1.0 + 1e-6) return false;

            hit = new Point3d(origin.X + t * dir.X, origin.Y + t * dir.Y, z);
            return true;
        }

        /// <summary>
        /// 判断末段是否为“折弯弯钩”：末段相对前一段折角足够大（直/斜弯钩），且长度明显短于锚固长度。
        /// </summary>
        private static bool IsHookBend(Polyline poly, int last, double anchorageLength)
        {
            const double bendCos = 0.94; // ≈20°：折角超过该阈值视为弯钩

            Vector3d dMain = poly.GetPoint3dAt(last - 1) - poly.GetPoint3dAt(last - 2);
            Vector3d dLast = poly.GetPoint3dAt(last) - poly.GetPoint3dAt(last - 1);
            if (dMain.Length < 1e-6 || dLast.Length < 1e-6) return false;

            // 末段过长更可能是主筋本体而非弯钩
            if (dLast.Length >= anchorageLength) return false;

            double cos = dMain.GetNormal().DotProduct(dLast.GetNormal());
            return cos < bendCos;
        }

        private static bool IsHookBendAtStart(Polyline poly, double anchorageLength)
        {
            const double bendCos = 0.94;

            Vector3d dMain = poly.GetPoint3dAt(1) - poly.GetPoint3dAt(2);
            Vector3d dFirst = poly.GetPoint3dAt(0) - poly.GetPoint3dAt(1);
            if (dMain.Length < 1e-6 || dFirst.Length < 1e-6) return false;

            if (dFirst.Length >= anchorageLength) return false;

            double cos = dMain.GetNormal().DotProduct(dFirst.GetNormal());
            return cos < bendCos;
        }

        internal static int GetSkipAdjacentSegmentIndex(Polyline poly, int extEndIdx)
        {
            int numSegs = poly.NumberOfVertices - 1;
            if (extEndIdx <= 0) return 0;
            if (extEndIdx >= poly.NumberOfVertices - 1) return numSegs - 1;
            return -1;
        }
    }
}
