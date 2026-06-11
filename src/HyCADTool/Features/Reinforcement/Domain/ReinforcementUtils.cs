using HyCAD.Geometry;
using HyCAD.Geometry.Interfaces;
using HyCADTool.Features.Reinforcement.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Features.Reinforcement.Domain
{
    /// <summary>
    /// 钢筋生成静态工具类（平台无关）
    /// 包含锚固延伸、弯钩计算、点钢筋布点、标注位置计算等算法
    /// </summary>
    public static class ReinforcementUtils
    {
        #region 锚固延伸（对应旧代码 GetSubReinforcementWithAnchors + ExtendSingleReinforcement + ExtendEndingReinforcement）

        /// <summary>
        /// 对所有分段钢筋添加锚固延伸
        /// 返回延伸后的钢筋数组和弯折标记
        /// </summary>
        public static (Polyline2D[] extended, List<Dictionary<int, bool>> bendingFlags) ExtendAllToAnchorage(
            Polyline2D[] subReinforcements,
            Polyline2D boundary,
            ReinParameters parameters,
            ILineIntersectionService intersectionService,
            Polyline2D[] rayTargets = null)
        {
            var targets = rayTargets ?? new[] { boundary };
            var results = new Polyline2D[subReinforcements.Length];
            var bendingFlags = new List<Dictionary<int, bool>>();

            for (int i = 0; i < subReinforcements.Length; i++)
            {
                results[i] = ExtendSingleToAnchorage(
                    subReinforcements[i], boundary, targets, parameters, intersectionService,
                    out Dictionary<int, bool> dic);
                bendingFlags.Add(dic);
            }

            return (results, bendingFlags);
        }

        /// <summary>
        /// 对单根钢筋延伸到锚固长度
        /// 对应旧代码 ExtendSingleReinforcement
        /// 严格按旧代码逻辑：
        ///   startSeg 反向 (EndPoint→StartPoint)
        ///   startDirection = GetBendDirection
        ///   extendStart = ExtendEnding(startSeg反向, startDirection)
        ///   extendEnd   = ExtendEnding(endSeg, -startDirection)
        ///   起点延伸点按原序依次 AddVertexAt(0,…)（等效为反序插入）
        /// </summary>
        public static Polyline2D ExtendSingleToAnchorage(
            Polyline2D subPolyline,
            Polyline2D boundary,
            Polyline2D[] rayTargets,
            ReinParameters parameters,
            ILineIntersectionService intersectionService,
            out Dictionary<int, bool> bendingFlags)
        {
            var result = subPolyline.Clone();

            // 1. 取钢筋的起止线段（与旧代码一致）
            var endSeg = subPolyline.GetSegmentAt(subPolyline.VertexCount - 2);
            var startSeg = subPolyline.GetSegmentAt(0);
            // 1.1 起点方向反向（旧代码: startSeg = new LineSegment3d(startSeg.EndPoint, startSeg.StartPoint)）
            var startSegReversed = new Line2D(startSeg.EndPoint, startSeg.StartPoint);

            // 1.2 得到弯折方向（旧代码: GetDirectionTwoPointInOneLine）
            Vector2D? startDirection = GetBendDirection(subPolyline, rayTargets, intersectionService);

            // 2. 延伸起点（旧代码: ExtendEndingReinforcement(startSeg, startDirection, ...)）
            var extendStart = ExtendEnding(
                startSegReversed, startDirection, boundary, rayTargets, parameters, intersectionService,
                out bool isStartBending);

            // 延伸终点（旧代码: ExtendEndingReinforcement(endSeg, -startDirection, ...)）
            Vector2D? endDirection = startDirection.HasValue
                ? (Vector2D?)(new Vector2D(-startDirection.Value.X, -startDirection.Value.Y))
                : null;
            var extendEnd = ExtendEnding(
                endSeg, endDirection, boundary, rayTargets, parameters, intersectionService,
                out bool isEndBending);

            // 3. 弯折标记
            bendingFlags = new Dictionary<int, bool>
            {
                { 1, isStartBending },
                { 2, isEndBending }
            };

            // 4. 添加延伸点到钢筋
            // 旧代码起点: foreach(point in extendPointsStart) { subPolylineN.AddVertexAt(0, point) }
            // 旧代码 AddVertexAt(0, ...) 在每次调用时都插在最前面，
            // 所以 points[0] 先插到位置0，然后 points[1] 插到位置0 把 points[0] 推到位置1
            // 等效: 最终顺序 = extendStart 的反序
            foreach (var pt in extendStart)
                result.AddVertexAt(0, pt);

            // 终点: foreach(point in extendPointsEnd) { subPolylineN.AddVertexAt(NumberOfVertices, point) }
            foreach (var pt in extendEnd)
                result.AddVertex(pt);

            return result;
        }

        /// <summary>
        /// 延伸钢筋末端到锚固长度
        /// 对应旧代码 ExtendEndingReinforcement
        /// </summary>
        public static List<Point2D> ExtendEnding(
            Line2D seg,
            Vector2D? preferredDirection,
            Polyline2D boundary,
            Polyline2D[] rayTargets,
            ReinParameters parameters,
            ILineIntersectionService intersectionService,
            out bool isBending)
        {
            isBending = false;
            var extendPoints = new List<Point2D>();
            double anchorageLength = parameters.AnchorageLength;
            double protectionThickness = parameters.ProtectionThickness * parameters.Scale;
            double bendingMinLength = parameters.BendingLineMinLength;

            // 钢筋方向（零长度线段直接返回空）
            if (!seg.Direction.TryNormalize(out Vector2D direction01))
                return extendPoints;

            // 沿钢筋方向延伸到轮廓（减2倍保护层厚度）
            if (!TryGetExtendSegment(
                    seg.EndPoint, direction01, rayTargets, protectionThickness, intersectionService,
                    out Point2D extendSeg01End, out Vector2D nextDirection))
            {
                return extendPoints;
            }

            double extendLen01 = seg.EndPoint.DistanceTo(extendSeg01End);

            if (extendLen01 < anchorageLength)
            {
                // 需要弯折
                Vector2D bendDir = preferredDirection ?? nextDirection;

                if (bendDir.IsZero())
                    return extendPoints;

                if (!TryGetExtendSegment(
                        extendSeg01End, bendDir, rayTargets, protectionThickness, intersectionService,
                        out Point2D extendSeg02End, out _))
                {
                    return extendPoints;
                }

                double extendLen02 = extendSeg01End.DistanceTo(extendSeg02End);

                if ((extendLen02 + extendLen01) < anchorageLength)
                {
                    // 两段总长不够，标记但保留
                }
                else
                {
                    double neededLen = anchorageLength - extendLen01;
                    Point2D bendEnd = extendSeg01End.Add(bendDir * neededLen);

                    if (neededLen < bendingMinLength)
                    {
                        double clampedLen = Math.Min(bendingMinLength, extendLen02);
                        bendEnd = extendSeg01End.Add(bendDir * clampedLen);
                    }

                    extendSeg02End = bendEnd;
                }

                extendPoints.Add(extendSeg01End);
                extendPoints.Add(extendSeg02End);
                isBending = true;
            }
            else
            {
                Point2D straightAnchorEnd = seg.EndPoint.Add(direction01 * anchorageLength);
                extendPoints.Add(straightAnchorEnd);
            }

            return extendPoints;
        }

        /// <summary>
        /// 判断两端点是否在同一条边界线段上，返回弯折方向
        /// 对应旧代码 GetDirectionTwoPointInOneLine
        /// </summary>
        public static Vector2D? GetBendDirection(
            Polyline2D subPoly,
            Polyline2D[] rayTargets,
            ILineIntersectionService intersectionService)
        {
            var startSeg = subPoly.GetSegmentAt(0);
            var endSeg = subPoly.GetSegmentAt(subPoly.VertexCount - 2);

            if (!startSeg.EndPoint.VectorTo(startSeg.StartPoint).TryNormalize(out Vector2D startDir))
                return null;
            if (!endSeg.Direction.TryNormalize(out Vector2D endDir))
                return null;

            if (!TryGetNearestForwardIntersection(
                    startSeg.StartPoint, startDir, rayTargets, intersectionService,
                    out Point2D boundStartPt, out Polyline2D hitStart))
                return null;
            if (!TryGetNearestForwardIntersection(
                    endSeg.EndPoint, endDir, rayTargets, intersectionService,
                    out Point2D boundEndPt, out Polyline2D hitEnd))
                return null;

            try
            {
                var (segStart, _) = hitStart.GetSegmentAtPoint(boundStartPt);
                var (segEnd, _) = hitEnd.GetSegmentAtPoint(boundEndPt);

                if (segStart.StartPoint.IsEqualTo(segEnd.StartPoint) &&
                    segStart.EndPoint.IsEqualTo(segEnd.EndPoint))
                {
                    if (boundEndPt.VectorTo(boundStartPt).TryNormalize(out Vector2D bendDir))
                        return bendDir;
                    return null;
                }
            }
            catch
            {
                // 点不在多段线上，忽略
            }

            return null;
        }

        public static (Point2D endPoint, Vector2D nextDirection) GetExtendSegment(
            Point2D basePoint,
            Vector2D direction,
            Polyline2D boundary,
            double protectionThickness,
            ILineIntersectionService intersectionService)
        {
            if (TryGetExtendSegment(
                    basePoint, direction, new[] { boundary }, protectionThickness, intersectionService,
                    out Point2D endPoint, out Vector2D nextDirection))
            {
                return (endPoint, nextDirection);
            }

            return (basePoint, direction.Perpendicular().IsZero() ? Vector2D.UnitX : direction.Perpendicular());
        }

        public static bool TryGetExtendSegment(
            Point2D basePoint,
            Vector2D direction,
            Polyline2D[] rayTargets,
            double protectionThickness,
            ILineIntersectionService intersectionService,
            out Point2D endPoint,
            out Vector2D nextDirection)
        {
            endPoint = basePoint;
            nextDirection = Vector2D.UnitX;

            if (!TryGetNearestForwardIntersection(
                    basePoint, direction, rayTargets, intersectionService,
                    out Point2D boundaryPoint, out Polyline2D hitBoundary))
            {
                return false;
            }

            double fullLen = basePoint.DistanceTo(boundaryPoint);
            double adjustedLen = Math.Max(0, fullLen - 2 * protectionThickness);
            endPoint = basePoint.Add(direction * adjustedLen);

            try
            {
                var (_, dir) = hitBoundary.GetSegmentAtPoint(boundaryPoint);
                nextDirection = dir.IsZero() ? direction.Perpendicular() : dir;
            }
            catch
            {
                nextDirection = direction.Perpendicular();
            }

            if (nextDirection.IsZero())
                nextDirection = Vector2D.UnitX;

            return true;
        }

        private static bool TryGetNearestForwardIntersection(
            Point2D origin,
            Vector2D direction,
            Polyline2D[] rayTargets,
            ILineIntersectionService intersectionService,
            out Point2D intersection,
            out Polyline2D hitBoundary)
        {
            intersection = origin;
            hitBoundary = null;

            if (rayTargets == null || rayTargets.Length == 0 || !direction.TryNormalize(out _))
                return false;

            double nearestDist = double.MaxValue;
            bool found = false;

            foreach (var target in rayTargets)
            {
                if (target == null)
                    continue;

                if (intersectionService.TryGetNearestForwardIntersection(origin, direction, target, out Point2D hit))
                {
                    double dist = origin.DistanceTo(hit);
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        intersection = hit;
                        hitBoundary = target;
                        found = true;
                    }
                }
            }

            return found;
        }

        #endregion

        #region 弯钩计算（对应旧代码 Addhook + AddAnchorToReinforcement + AddAnchorToReinforcementIsReverse）

        /// <summary>
        /// 对所有锚固钢筋添加弯钩
        /// 对应旧代码 Addhook
        /// </summary>
        public static Polyline2D[] AddHooks(
            Polyline2D[] reinforcementsWithAnchors,
            List<Dictionary<int, bool>> bendingFlags,
            Polyline2D boundary,
            double hookLength,
            ILineIntersectionService intersectionService,
            Polyline2D[] rayTargets = null,
            ReinRegion region = null)
        {
            var targets = rayTargets ?? new[] { boundary };
            var result = new List<Polyline2D>();

            for (int i = 0; i < reinforcementsWithAnchors.Length; i++)
            {
                var poly = reinforcementsWithAnchors[i].Clone();
                var flags = bendingFlags[i];

                var endSeg = poly.GetSegmentAt(poly.VertexCount - 2);
                var startSeg = poly.GetSegmentAt(0);
                var startSegReversed = new Line2D(startSeg.EndPoint, startSeg.StartPoint);

                Point2D hookStart = flags[1]
                    ? CalculateHookPointWithReverse(startSegReversed, true, hookLength, targets, intersectionService, region)
                    : CalculateHookPoint(startSegReversed, true, hookLength);
                poly.AddVertexAt(0, hookStart);

                Point2D hookEnd = flags[2]
                    ? CalculateHookPointWithReverse(endSeg, false, hookLength, targets, intersectionService, region)
                    : CalculateHookPoint(endSeg, false, hookLength);
                poly.AddVertex(hookEnd);

                result.Add(poly);
            }

            return result.ToArray();
        }

        /// <summary>
        /// 计算弯钩点（标准方向）
        /// 对应旧代码 AddAnchorToReinforcement
        /// </summary>
        public static Point2D CalculateHookPoint(Line2D seg, bool isStartPoint, double hookLength)
        {
            Vector2D segDir = seg.StartPoint.VectorTo(seg.EndPoint);
            if (!segDir.TryNormalize(out Vector2D segDirNorm))
                return seg.EndPoint; // 零长度线段，不添加弯钩
            double angle = isStartPoint ? Math.PI * 5.0 / 4.0 : Math.PI * 3.0 / 4.0;
            Vector2D hookDir = segDirNorm.Rotate(angle);
            return seg.EndPoint.Add(hookDir * hookLength);
        }

        /// <summary>
        /// 计算弯钩点（带反转检测：弯钩应指向混凝土内侧）
        /// 对应旧代码 AddAnchorToReinforcementIsReverse
        /// </summary>
        public static Point2D CalculateHookPointWithReverse(
            Line2D seg,
            bool isStartPoint,
            double hookLength,
            Polyline2D[] rayTargets,
            ILineIntersectionService intersectionService,
            ReinRegion region = null)
        {
            Vector2D segDir = seg.StartPoint.VectorTo(seg.EndPoint);
            if (!segDir.TryNormalize(out Vector2D segDirNorm))
                return seg.EndPoint;
            Vector2D hookDir = segDirNorm.Rotate(Math.PI * 3.0 / 4.0);
            Vector2D hookDirReverse = segDirNorm.Rotate(Math.PI * 5.0 / 4.0);

            Point2D basePoint = isStartPoint ? seg.StartPoint : seg.EndPoint;
            Vector2D chosenDir = ChooseHookDirection(
                basePoint, hookDir, hookDirReverse, hookLength, rayTargets, intersectionService, region);

            return seg.EndPoint.Add(chosenDir * hookLength);
        }

        private static Vector2D ChooseHookDirection(
            Point2D basePoint,
            Vector2D hookDir,
            Vector2D hookDirReverse,
            double hookLength,
            Polyline2D[] rayTargets,
            ILineIntersectionService intersectionService,
            ReinRegion region)
        {
            if (region != null)
            {
                Point2D testA = basePoint.Add(hookDir * hookLength);
                Point2D testB = basePoint.Add(hookDirReverse * hookLength);
                bool inA = region.IsValidRebarPoint(testA);
                bool inB = region.IsValidRebarPoint(testB);

                if (inA && !inB)
                    return hookDir;
                if (inB && !inA)
                    return hookDirReverse;
            }

            double distA = double.MaxValue;
            double distB = double.MaxValue;

            if (TryGetNearestForwardIntersection(basePoint, hookDir, rayTargets, intersectionService, out Point2D ptA, out _))
                distA = basePoint.DistanceTo(ptA);
            if (TryGetNearestForwardIntersection(basePoint, hookDirReverse, rayTargets, intersectionService, out Point2D ptB, out _))
                distB = basePoint.DistanceTo(ptB);

            return distA > distB ? hookDir : hookDirReverse;
        }

        #endregion

        #region 点钢筋布置（对应旧代码 AddDotRein + AddReduceDotRein + GetLineSeparatPoint 系列）

        /// <summary>
        /// 在偏移多段线的每条边上按间距布置点钢筋
        /// 对应旧代码 AddDotRein
        /// </summary>
        public static Point2D[] GenerateDotPositions(
            Polyline2D offsetPoly, double separation, double startDistance)
        {
            var list = new List<Point2D>();
            var segments = offsetPoly.GetSegments();
            foreach (var seg in segments)
            {
                var points = GetSeparationPoints(seg, separation, startDistance);
                // 使用 Union 去重（与旧代码一致）
                foreach (var pt in points)
                {
                    if (!list.Any(existing => existing.IsEqualTo(pt)))
                        list.Add(pt);
                }
            }
            return list.ToArray();
        }

        /// <summary>
        /// 在偏移多段线的每条边上生成减少数量的点位置
        /// 对应旧代码 AddReduceDotRein
        /// </summary>
        public static Point2D[] GenerateReducedDotPositions(
            Polyline2D offsetPoly, double separation)
        {
            var list = new List<Point2D>();
            var segments = offsetPoly.GetSegments();
            foreach (var seg in segments)
            {
                var points = GetReducePoints(seg, separation);
                foreach (var pt in points)
                {
                    if (!list.Any(existing => existing.IsEqualTo(pt)))
                        list.Add(pt);
                }
            }
            return list.ToArray();
        }

        /// <summary>
        /// 在线段上按间距和起始距离均匀分布点
        /// 对应旧代码 GetLineSeparatPoint
        /// </summary>
        public static Point2D[] GetSeparationPoints(Line2D seg, double separation, double startDistance)
        {
            double length = seg.Length;
            var ratios = GetRangeNumbersWithStartEnd(length, separation, startDistance);
            return ratios.Select(r => seg.GetPointAtParameter(r)).ToArray();
        }

        /// <summary>
        /// 在线段上生成减少的点（起点/终点/中点组合）
        /// 对应旧代码 GetLineReducePoints
        /// </summary>
        public static Point2D[] GetReducePoints(Line2D seg, double separation)
        {
            var result = new List<Point2D>();

            if (seg.Length <= separation)
            {
                result.Add(seg.StartPoint);
                result.Add(seg.EndPoint);
            }
            else if (seg.Length <= separation * 2)
            {
                result.Add(seg.StartPoint);
                result.Add(seg.MidPoint);
                result.Add(seg.EndPoint);
            }
            else
            {
                if (seg.Direction.TryNormalize(out Vector2D dir))
                {
                    Point2D mid = seg.MidPoint;
                    result.Add(mid.Subtract(dir * (separation / 2)));
                    result.Add(mid.Add(dir * (separation / 2)));
                }
                else
                {
                    result.Add(seg.StartPoint);
                    result.Add(seg.EndPoint);
                }
            }

            return result.ToArray();
        }

        /// <summary>
        /// 计算归一化分割参数（含起止点偏移）
        /// 对应旧代码 GetRangeNumbersUseSEPoint
        /// </summary>
        private static double[] GetRangeNumbersWithStartEnd(double length, double separation, double startDistance)
        {
            double innerLength = length - startDistance * 2;
            if (innerLength < separation)
                return new[] { 0.0, 1.0 };

            double scale = innerLength / length;
            double offset = startDistance / length;
            var innerRatios = GetEvenlySpacedRatios(innerLength, separation);

            var result = new List<double> { 0.0 };
            foreach (var r in innerRatios)
                result.Add(r * scale + offset);
            result.Add(1.0);

            return result.ToArray();
        }

        /// <summary>
        /// 计算在给定长度上按间距均匀分布的归一化参数
        /// 对应旧代码 GetRangeNumbersByLengthSeparatin
        /// </summary>
        private static double[] GetEvenlySpacedRatios(double length, double separation)
        {
            double normalizedSep = separation / length;
            int divCount = (int)(1.0 / normalizedSep);
            double remainder = 1.0 - normalizedSep * divCount;
            var result = new List<double>();

            if (remainder < 1e-6)
            {
                result.Add(0);
                for (int i = 1; i < divCount; i++)
                    result.Add(i * normalizedSep);
                result.Add(1.0);
            }
            else
            {
                double halfGap = (normalizedSep - remainder) / 2;
                result.Add(0);
                double current = normalizedSep - halfGap;
                result.Add(current);
                for (int i = 2; i < divCount; i++)
                {
                    current += normalizedSep;
                    result.Add(current);
                }
                result.Add(1.0 - normalizedSep + halfGap);
                result.Add(1.0);
            }

            return result.ToArray();
        }

        #endregion

        #region 标注位置计算（对应旧代码 AddMleaders + GetMleaderByPoints）

        /// <summary>
        /// 计算所有标注的数据
        /// 对应旧代码 AddMleaders；相邻过近的标注合并，避免重叠
        /// </summary>
        public static MLeaderData[] CalculateLabelData(
            Polyline2D dotReinCenterPoly, double separation, double leaderDistance, string content)
        {
            var result = new List<MLeaderData>();
            var segments = dotReinCenterPoly.GetSegments();
            double minGap = separation * 0.5; // 标注中点距离小于此值则视为重叠，跳过

            foreach (var seg in segments)
            {
                var points = GetReducePoints(seg, separation);
                if (points.Length < 2) continue;

                Point2D mid = seg.MidPoint;
                if (result.Any(r => MidpointOf(r).DistanceTo(mid) < minGap))
                    continue;

                result.Add(new MLeaderData
                {
                    AnchorPoints = points,
                    LeaderDistance = leaderDistance,
                    Content = content
                });
            }

            return result.ToArray();
        }

        private static Point2D MidpointOf(MLeaderData data)
        {
            if (data.AnchorPoints == null || data.AnchorPoints.Length == 0)
                return Point2D.Origin;
            double x = 0, y = 0;
            foreach (var p in data.AnchorPoints) { x += p.X; y += p.Y; }
            int n = data.AnchorPoints.Length;
            return new Point2D(x / n, y / n);
        }

        #endregion

        #region 完整配筋生成（对应旧代码 SetProperties 编排逻辑）

        /// <summary>
        /// 执行完整配筋生成流程，返回平台无关的结果
        /// 对应旧代码 SetProperties
        /// </summary>
        public static ReinforcementResult GenerateAll(
            Polyline2D boundary,
            ReinParameters parameters,
            IPolygonOffsetService offsetService,
            ILineIntersectionService intersectionService,
            ReinRegion region = null)
        {
            var result = new ReinforcementResult();
            result.Boundary = boundary;

            var rayTargets = region?.AllRings ?? new[] { boundary };
            double scale = parameters.Scale;
            double protectionThickness = parameters.ProtectionThickness * scale;
            double dotReinOffset = parameters.DotReinOffset * scale;
            double hookLength = parameters.HookLength * scale;
            double dotSeparation = parameters.DotSeparation;
            double dotStartDistance = parameters.DotStartDistance;
            double leaderDistance = parameters.MleaderDistance * scale;

            var offsetBoundary = offsetService.Offset(boundary, -protectionThickness);
            if (offsetBoundary == null)
            {
                result.SubReinforcements = new Polyline2D[0];
                return result;
            }
            offsetBoundary.RemoveShortSegments(1.0);
            if (offsetBoundary.VertexCount < 3)
            {
                result.SubReinforcements = new Polyline2D[0];
                return result;
            }
            result.SubReinforcements = offsetBoundary.SplitByAngleThreshold();

            result.SubReinforcements = result.SubReinforcements
                .Where(s => s.VertexCount >= 2 && s.GetTotalLength() > 1.0)
                .ToArray();
            if (result.SubReinforcements.Length == 0)
                return result;

            result.SubReinforcements = Polyline2D.ConnectByCondition(
                result.SubReinforcements, parameters.AnchorageJoinLength);

            var (extended, bendingFlags) = ExtendAllToAnchorage(
                result.SubReinforcements, boundary, parameters, intersectionService, rayTargets);
            result.SubReinforcementWithAnchors = extended;
            result.BendingFlags = bendingFlags;

            result.FinalReinforcements = AddHooks(
                extended, bendingFlags, boundary, hookLength, intersectionService, rayTargets, region);

            if (region != null)
            {
                result.FinalReinforcements = ClampReinforcementsToRegion(
                    result.FinalReinforcements, region, protectionThickness, intersectionService, rayTargets);
            }

            var dotCenterPoly = offsetService.Offset(boundary, -dotReinOffset);
            if (dotCenterPoly != null)
                dotCenterPoly.RemoveShortSegments(1.0);
            result.DotReinCenterPoly = dotCenterPoly;
            if (dotCenterPoly != null && dotCenterPoly.VertexCount >= 3)
            {
                result.DotReinPoints = GenerateDotPositions(dotCenterPoly, dotSeparation, dotStartDistance);
                result.ReduceDotReinPoints = GenerateReducedDotPositions(dotCenterPoly, dotSeparation);
            }

            string labelContent = $"\\U+E532{parameters.RebarDiameter}@{parameters.RebarSpacing}";
            result.MLeaders = dotCenterPoly != null && dotCenterPoly.VertexCount >= 3
                ? CalculateLabelData(dotCenterPoly, dotSeparation, leaderDistance, labelContent)
                : new MLeaderData[0];

            return result;
        }

        /// <summary>
        /// 将钢筋端部顶点裁剪到有效配筋区域内，防止延伸/弯钩出界。
        /// </summary>
        public static Polyline2D[] ClampReinforcementsToRegion(
            Polyline2D[] reinforcements,
            ReinRegion region,
            double protectionThickness,
            ILineIntersectionService intersectionService,
            Polyline2D[] rayTargets)
        {
            if (region == null || reinforcements == null)
                return reinforcements;

            var clamped = new Polyline2D[reinforcements.Length];
            for (int i = 0; i < reinforcements.Length; i++)
                clamped[i] = ClampSingleReinforcementToRegion(
                    reinforcements[i], region, protectionThickness, intersectionService, rayTargets);
            return clamped;
        }

        private static Polyline2D ClampSingleReinforcementToRegion(
            Polyline2D poly,
            ReinRegion region,
            double protectionThickness,
            ILineIntersectionService intersectionService,
            Polyline2D[] rayTargets)
        {
            if (poly == null || poly.VertexCount < 2)
                return poly;

            var vertices = new List<Point2D>();
            for (int i = 0; i < poly.VertexCount; i++)
                vertices.Add(poly.GetPointAt(i));

            vertices = ClampEndVertices(vertices, fromStart: true, region, protectionThickness, intersectionService, rayTargets);
            vertices = ClampEndVertices(vertices, fromStart: false, region, protectionThickness, intersectionService, rayTargets);

            if (vertices.Count < 2)
                return poly;

            return new Polyline2D(vertices, poly.IsClosed);
        }

        private static List<Point2D> ClampEndVertices(
            List<Point2D> vertices,
            bool fromStart,
            ReinRegion region,
            double protectionThickness,
            ILineIntersectionService intersectionService,
            Polyline2D[] rayTargets)
        {
            while (vertices.Count >= 2)
            {
                int endIdx = fromStart ? 0 : vertices.Count - 1;
                int innerIdx = fromStart ? 1 : vertices.Count - 2;
                Point2D endPoint = vertices[endIdx];
                Point2D innerPoint = vertices[innerIdx];

                if (region.IsValidRebarPoint(endPoint))
                    break;

                if (TryClampPointToRegion(
                        innerPoint, endPoint, region, protectionThickness, intersectionService, rayTargets,
                        out Point2D clamped))
                {
                    vertices[endIdx] = clamped;
                    break;
                }

                vertices.RemoveAt(endIdx);
            }

            return vertices;
        }

        private static bool TryClampPointToRegion(
            Point2D from,
            Point2D to,
            ReinRegion region,
            double protectionThickness,
            ILineIntersectionService intersectionService,
            Polyline2D[] rayTargets,
            out Point2D clamped)
        {
            clamped = to;
            if (!from.VectorTo(to).TryNormalize(out Vector2D dir))
                return false;

            if (TryGetNearestForwardIntersection(from, dir, rayTargets, intersectionService, out Point2D hit, out _))
            {
                double fullLen = from.DistanceTo(hit);
                double adjustedLen = Math.Max(0, fullLen - 2 * protectionThickness);
                Point2D candidate = from.Add(dir * adjustedLen);
                if (region.IsValidRebarPoint(candidate))
                {
                    clamped = candidate;
                    return true;
                }
            }

            const int steps = 16;
            for (int i = steps - 1; i >= 1; i--)
            {
                double t = (double)i / steps;
                Point2D candidate = new Point2D(
                    from.X + (to.X - from.X) * t,
                    from.Y + (to.Y - from.Y) * t);
                if (region.IsValidRebarPoint(candidate))
                {
                    clamped = candidate;
                    return true;
                }
            }

            return false;
        }

        #endregion
    }
}
