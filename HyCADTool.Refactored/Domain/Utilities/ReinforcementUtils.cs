using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Domain.ValueObjects;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Reinforcement;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Refactored.Domain.Utilities
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
            ILineIntersectionService intersectionService)
        {
            var results = new Polyline2D[subReinforcements.Length];
            var bendingFlags = new List<Dictionary<int, bool>>();

            for (int i = 0; i < subReinforcements.Length; i++)
            {
                results[i] = ExtendSingleToAnchorage(
                    subReinforcements[i], boundary, parameters, intersectionService,
                    out Dictionary<int, bool> dic);
                bendingFlags.Add(dic);
            }

            return (results, bendingFlags);
        }

        /// <summary>
        /// 对单根钢筋延伸到锚固长度
        /// 对应旧代码 ExtendSingleReinforcement
        /// </summary>
        public static Polyline2D ExtendSingleToAnchorage(
            Polyline2D subPolyline,
            Polyline2D boundary,
            ReinParameters parameters,
            ILineIntersectionService intersectionService,
            out Dictionary<int, bool> bendingFlags)
        {
            var result = subPolyline.Clone();

            // 起止线段
            var endSeg = subPolyline.GetSegmentAt(subPolyline.VertexCount - 2);
            var startSeg = subPolyline.GetSegmentAt(0);
            // 起点方向反向
            var startSegReversed = new Line2D(startSeg.EndPoint, startSeg.StartPoint);

            // 判断两端点是否在同一条边界线段上，决定弯折方向
            Vector2D? bendDirection = GetBendDirection(subPolyline, boundary, intersectionService);

            // 延伸起点
            var extendStart = ExtendEnding(
                startSegReversed, bendDirection, boundary, parameters, intersectionService,
                out bool isStartBending);

            // 延伸终点（方向取反）
            Vector2D? endDirection = bendDirection.HasValue ? (Vector2D?)(-bendDirection.Value) : null;
            var extendEnd = ExtendEnding(
                endSeg, endDirection, boundary, parameters, intersectionService,
                out bool isEndBending);

            bendingFlags = new Dictionary<int, bool>
            {
                { 1, isStartBending },
                { 2, isEndBending }
            };

            // 在起点前插入延伸点
            for (int i = extendStart.Count - 1; i >= 0; i--)
                result.AddVertexAt(0, extendStart[i]);

            // 在终点后添加延伸点
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
            ReinParameters parameters,
            ILineIntersectionService intersectionService,
            out bool isBending)
        {
            isBending = false;
            var extendPoints = new List<Point2D>();
            double anchorageLength = parameters.AnchorageLength;
            double protectionThickness = parameters.ProtectionThickness * parameters.Scale;
            double bendingMinLength = parameters.BendingLineMinLength;

            // 钢筋方向
            Vector2D direction01 = seg.Direction.Normalize();

            // 直线锚固终点
            Point2D straightAnchorEnd = seg.EndPoint.Add(direction01 * anchorageLength);

            // 沿钢筋方向延伸到轮廓（减2倍保护层厚度）
            var (extendSeg01End, nextDirection) = GetExtendSegment(
                seg.EndPoint, direction01, boundary, protectionThickness, intersectionService);

            double extendLen01 = seg.EndPoint.DistanceTo(extendSeg01End);

            if (extendLen01 < anchorageLength)
            {
                // 需要弯折
                Vector2D bendDir = preferredDirection ?? nextDirection;

                var (extendSeg02End, _) = GetExtendSegment(
                    extendSeg01End, bendDir, boundary, protectionThickness, intersectionService);

                double extendLen02 = extendSeg01End.DistanceTo(extendSeg02End);

                if ((extendLen02 + extendLen01) < anchorageLength)
                {
                    // 两段总长不够，标记但保留
                    // （旧代码在这里调用 MakeMark，Domain层不做，由调用方处理）
                }
                else
                {
                    // 计算弯折段的实际需要长度
                    double neededLen = anchorageLength - extendLen01;
                    Point2D bendEnd = extendSeg01End.Add(bendDir * neededLen);

                    // 检查最小平直段长度
                    if (neededLen < bendingMinLength)
                    {
                        bendEnd = extendSeg01End.Add(bendDir * bendingMinLength);
                    }

                    extendSeg02End = bendEnd;
                }

                extendPoints.Add(extendSeg01End);
                extendPoints.Add(extendSeg02End);
                isBending = true;
            }
            else
            {
                // 直线锚固即可
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
            Polyline2D boundary,
            ILineIntersectionService intersectionService)
        {
            // 起止线段
            var startSeg = subPoly.GetSegmentAt(0);
            var endSeg = subPoly.GetSegmentAt(subPoly.VertexCount - 2);

            // 起点方向反向
            Vector2D startDir = startSeg.EndPoint.VectorTo(startSeg.StartPoint).Normalize();
            Vector2D endDir = endSeg.Direction.Normalize();

            // 求交点
            Point2D boundStartPt = intersectionService.GetNearestForwardIntersection(
                startSeg.StartPoint, startDir, boundary);
            Point2D boundEndPt = intersectionService.GetNearestForwardIntersection(
                endSeg.EndPoint, endDir, boundary);

            // 求交点所在线段
            try
            {
                var (segStart, _) = boundary.GetSegmentAtPoint(boundStartPt);
                var (segEnd, _) = boundary.GetSegmentAtPoint(boundEndPt);

                // 两交点在同一线段上
                if (segStart.StartPoint.IsEqualTo(segEnd.StartPoint) &&
                    segStart.EndPoint.IsEqualTo(segEnd.EndPoint))
                {
                    return boundStartPt.VectorTo(boundEndPt).Normalize();
                }
            }
            catch
            {
                // 点不在多段线上，忽略
            }

            return null;
        }

        /// <summary>
        /// 获取从基点沿方向到轮廓的延伸段（减去2倍保护层厚度）
        /// 对应旧代码 GetExtendSeg
        /// </summary>
        public static (Point2D endPoint, Vector2D nextDirection) GetExtendSegment(
            Point2D basePoint,
            Vector2D direction,
            Polyline2D boundary,
            double protectionThickness,
            ILineIntersectionService intersectionService)
        {
            // 求射线与边界交点
            Point2D boundaryPoint = intersectionService.GetNearestForwardIntersection(
                basePoint, direction, boundary);

            // 修正长度（减去 2 倍保护层厚度）
            double fullLen = basePoint.DistanceTo(boundaryPoint);
            double adjustedLen = Math.Max(0, fullLen - 2 * protectionThickness);
            Point2D endPoint = basePoint.Add(direction * adjustedLen);

            // 获取边界交点处的下一个方向
            Vector2D nextDirection;
            try
            {
                var (_, dir) = boundary.GetSegmentAtPoint(boundaryPoint);
                nextDirection = dir;
            }
            catch
            {
                nextDirection = direction.Perpendicular();
            }

            return (endPoint, nextDirection);
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
            ILineIntersectionService intersectionService)
        {
            var result = new List<Polyline2D>();

            for (int i = 0; i < reinforcementsWithAnchors.Length; i++)
            {
                var poly = reinforcementsWithAnchors[i].Clone();
                var flags = bendingFlags[i];

                // 起止线段
                var endSeg = poly.GetSegmentAt(poly.VertexCount - 2);
                var startSeg = poly.GetSegmentAt(0);
                var startSegReversed = new Line2D(startSeg.EndPoint, startSeg.StartPoint);

                // 起点弯钩
                Point2D hookStart = flags[1]
                    ? CalculateHookPointWithReverse(startSegReversed, true, hookLength, boundary, intersectionService)
                    : CalculateHookPoint(startSegReversed, true, hookLength);
                poly.AddVertexAt(0, hookStart);

                // 终点弯钩
                Point2D hookEnd = flags[2]
                    ? CalculateHookPointWithReverse(endSeg, false, hookLength, boundary, intersectionService)
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
            double angle = isStartPoint ? Math.PI * 5.0 / 4.0 : Math.PI * 3.0 / 4.0;
            Vector2D hookDir = segDir.Rotate(angle).Normalize();
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
            Polyline2D boundary,
            ILineIntersectionService intersectionService)
        {
            Vector2D segDir = seg.StartPoint.VectorTo(seg.EndPoint);
            Vector2D hookDir = segDir.Rotate(Math.PI * 3.0 / 4.0).Normalize();
            Vector2D hookDirReverse = segDir.Rotate(Math.PI * 5.0 / 4.0).Normalize();

            Point2D basePoint = isStartPoint ? seg.StartPoint : seg.EndPoint;

            // 沿两个方向分别求到边界距离，选较远的方向（内侧更远）
            Point2D ptA = intersectionService.GetNearestForwardIntersection(basePoint, hookDir, boundary);
            Point2D ptB = intersectionService.GetNearestForwardIntersection(basePoint, hookDirReverse, boundary);

            double distA = basePoint.DistanceTo(ptA);
            double distB = basePoint.DistanceTo(ptB);

            Vector2D chosenDir = distA > distB ? hookDir : hookDirReverse;
            return seg.EndPoint.Add(chosenDir * hookLength);
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
                Vector2D dir = seg.Direction.Normalize();
                Point2D mid = seg.MidPoint;
                result.Add(mid.Subtract(dir * (separation / 2)));
                result.Add(mid.Add(dir * (separation / 2)));
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
        /// 对应旧代码 AddMleaders
        /// </summary>
        public static MLeaderData[] CalculateLabelData(
            Polyline2D dotReinCenterPoly, double separation, double leaderDistance, string content)
        {
            var result = new List<MLeaderData>();
            var segments = dotReinCenterPoly.GetSegments();

            foreach (var seg in segments)
            {
                var points = GetReducePoints(seg, separation);
                if (points.Length < 2) continue;

                result.Add(new MLeaderData
                {
                    AnchorPoints = points,
                    LeaderDistance = leaderDistance,
                    Content = content
                });
            }

            return result.ToArray();
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
            ILineIntersectionService intersectionService)
        {
            var result = new ReinforcementResult();
            result.Boundary = boundary;

            double scale = parameters.Scale;
            double protectionThickness = parameters.ProtectionThickness * scale;
            double dotReinOffset = parameters.DotReinOffset * scale;
            double hookLength = parameters.HookLength * scale;
            double dotSeparation = parameters.DotSeparation;
            double dotStartDistance = parameters.DotStartDistance;
            double reinforcementDiameter = parameters.ReinforcementDiameter * scale;
            double leaderDistance = parameters.MleaderDistance * scale;

            // 1. 偏移边界 → 分段钢筋
            var offsetBoundary = offsetService.Offset(boundary, -protectionThickness);
            result.SubReinforcements = offsetBoundary.SplitByAngleThreshold();

            // 2. 条件连接
            result.SubReinforcements = Polyline2D.ConnectByCondition(
                result.SubReinforcements, parameters.AnchorageJoinLength);

            // 3. 锚固延伸
            var (extended, bendingFlags) = ExtendAllToAnchorage(
                result.SubReinforcements, boundary, parameters, intersectionService);
            result.SubReinforcementWithAnchors = extended;
            result.BendingFlags = bendingFlags;

            // 4. 添加弯钩 → 最终钢筋
            result.FinalReinforcements = AddHooks(
                extended, bendingFlags, boundary, hookLength, intersectionService);

            // 5. 点钢筋
            var dotCenterPoly = offsetService.Offset(boundary, -dotReinOffset);
            result.DotReinCenterPoly = dotCenterPoly;
            result.DotReinPoints = GenerateDotPositions(dotCenterPoly, dotSeparation, dotStartDistance);
            result.ReduceDotReinPoints = GenerateReducedDotPositions(dotCenterPoly, dotSeparation);

            // 6. 标注
            string labelContent = $"\\U+E532{parameters.RebarDiameter}@{parameters.RebarSpacing}";
            result.MLeaders = CalculateLabelData(dotCenterPoly, dotSeparation, leaderDistance, labelContent);

            return result;
        }

        #endregion
    }
}
