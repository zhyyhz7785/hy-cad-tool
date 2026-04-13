using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services
{
    /// <summary>
    /// 人行横道绘制服务
    /// 斑马线在 Line2/Line3 与弧线围成的区域内绘制，弧线处自动裁切。
    /// </summary>
    public class CrosswalkService
    {
        private const double PointTolerance = 1.0;

        public class RoadArm
        {
            public Point3d LeftInner { get; set; }
            public Point3d RightInner { get; set; }
            public Point3d LeftOuter { get; set; }
            public Point3d RightOuter { get; set; }
            public Point3d LeftCorner { get; set; }
            public Point3d RightCorner { get; set; }
            public Vector3d OutwardDirection { get; set; }
            public Arc LeftArc { get; set; }
            public Arc RightArc { get; set; }
        }

        public class AnalysisResult
        {
            public List<RoadArm> Arms { get; set; } = new List<RoadArm>();
            public int TotalLines { get; set; }
            public int TotalArcs { get; set; }
            public int ConnectedLineCount { get; set; }
            public int IgnoredLineCount { get; set; }
            public int ValidArcCount { get; set; }
            public int SkippedArcCount { get; set; }
        }

        public AnalysisResult AnalyzeIntersection(List<Line> lines, List<Arc> arcs)
        {
            var result = new AnalysisResult
            {
                TotalLines = lines.Count,
                TotalArcs = arcs.Count
            };

            if (arcs.Count == 0)
                throw new Exception("未选择任何圆弧，无法识别交叉口角点。");

            var lineInfos = BuildConnectivityTolerant(lines, arcs);
            result.ConnectedLineCount = lineInfos.Count;
            result.IgnoredLineCount = lines.Count - lineInfos.Count;

            if (lineInfos.Count < 2)
                throw new Exception(
                    $"至少需要 2 条与圆弧相连的直线，当前仅 {lineInfos.Count} 条。");

            var arcCorners = ComputeArcCornersTolerant(lineInfos,
                out int validArcCount, out int skippedArcCount);
            result.ValidArcCount = validArcCount;
            result.SkippedArcCount = skippedArcCount;

            if (arcCorners.Count < 2)
                throw new Exception(
                    $"至少需要 2 个有效角点，当前仅 {arcCorners.Count} 个。");

            result.Arms = GroupIntoArmsTolerant(lineInfos, arcCorners);

            if (result.Arms.Count == 0)
                throw new Exception("无法配对出任何道路方向。");

            return result;
        }

        public void DrawAuxiliaryLines(Transaction tr, BlockTableRecord ms,
            List<RoadArm> arms, string layerName)
        {
            var center = ComputeCenter(arms);

            foreach (var arm in arms)
            {
                AddLine(tr, ms, arm.LeftCorner, arm.RightCorner, layerName);

                var leftDir = (arm.LeftOuter - arm.LeftInner).GetNormal();
                var leftExtLen = arm.LeftInner.DistanceTo(center) * 2.5;
                AddLine(tr, ms, arm.LeftInner, arm.LeftInner - leftDir * leftExtLen, layerName);

                var rightDir = (arm.RightOuter - arm.RightInner).GetNormal();
                var rightExtLen = arm.RightInner.DistanceTo(center) * 2.5;
                AddLine(tr, ms, arm.RightInner, arm.RightInner - rightDir * rightExtLen, layerName);
            }
        }

        /// <summary>
        /// 为一个道路方向绘制人行横道。
        /// 算法：1) Line2/Line3 两端延伸到弧线得 4 交点；
        ///       2) 从 roadDir 最小到最大全覆盖绘制条纹；
        ///       3) 每条条纹双端裁切（内端+外端均可能被弧线截断）。
        /// </summary>
        public void DrawCrosswalkForArm(Transaction tr, BlockTableRecord ms, RoadArm arm,
            double gapWidth, double crosswalkWidth, double stopLineDistance, double stripeSpacing,
            string auxLayerName, string crosswalkLayerName, string stopLineLayerName)
        {
            var outward = arm.OutwardDirection;
            var inward = new Vector3d(-outward.X, -outward.Y, -outward.Z);
            var baseLeft = arm.LeftCorner;
            var baseRight = arm.RightCorner;

            var vecGap = outward * gapWidth;
            var vecCrosswalk = outward * (gapWidth + crosswalkWidth);
            var vecStop = outward * (gapWidth + crosswalkWidth + stopLineDistance);

            var l2Left = baseLeft + vecGap;
            var l2Right = baseRight + vecGap;
            var l3Left = baseLeft + vecCrosswalk;
            var l3Right = baseRight + vecCrosswalk;

            var roadWidthVec = l2Right - l2Left;
            var roadWidth = roadWidthVec.Length;
            if (roadWidth < 1e-6)
                throw new Exception($"路宽≈0（{roadWidth:G4}），角点可能重合。");
            var roadDir = roadWidthVec.GetNormal();
            var negRoadDir = new Vector3d(-roadDir.X, -roadDir.Y, -roadDir.Z);

            // ---- Step 1: 延伸 Line2/Line3 到弧线，得 4 个交点 ----
            var hitL2L = RayHitArc(l2Left, negRoadDir, arm.LeftArc);
            var hitL2R = RayHitArc(l2Right, roadDir, arm.RightArc);
            var hitL3L = RayHitArc(l3Left, negRoadDir, arm.LeftArc);
            var hitL3R = RayHitArc(l3Right, roadDir, arm.RightArc);

            AddLine(tr, ms, hitL2L ?? l2Left, hitL2R ?? l2Right, auxLayerName);
            AddLine(tr, ms, hitL3L ?? l3Left, hitL3R ?? l3Right, auxLayerName);

            // 停止线（不延伸）
            var l4Left = baseLeft + vecStop;
            var l4Right = baseRight + vecStop;
            AddLine(tr, ms, l4Left, l4Right, stopLineLayerName);

            // ---- Step 2: 从 roadDir 最小到最大全覆盖 ----
            // 把 4 个交点投影到 roadDir 上（以 l2Left 为原点），求 tMin / tMax
            double tMin = 0, tMax = roadWidth;
            foreach (var pt in new Point3d?[] { hitL2L, hitL2R, hitL3L, hitL3R })
            {
                if (!pt.HasValue) continue;
                var diff = pt.Value - l2Left;
                double proj = diff.X * roadDir.X + diff.Y * roadDir.Y;
                if (proj < tMin) tMin = proj;
                if (proj > tMax) tMax = proj;
            }

            int startIdx = (int)Math.Ceiling(tMin / stripeSpacing);
            int endIdx = (int)Math.Floor(tMax / stripeSpacing);

            // ---- Step 3: 逐条绘制 + 双端裁切 ----
            for (int i = startIdx; i <= endIdx; i++)
            {
                double t = i * stripeSpacing;
                var innerPt = l2Left + roadDir * t;
                var outerPt = l3Left + roadDir * t;

                // 裁切外端：从 innerPt 沿 outward 射线，取最近弧线交点
                var clippedOuter = outerPt;
                double outerBestDist = crosswalkWidth;
                foreach (var arc in new[] { arm.LeftArc, arm.RightArc })
                {
                    var hit = RayHitArc(innerPt, outward, arc);
                    if (hit == null) continue;
                    var hv = hit.Value - innerPt;
                    double d = hv.X * outward.X + hv.Y * outward.Y;
                    if (d > 1e-4 && d < outerBestDist)
                    {
                        clippedOuter = hit.Value;
                        outerBestDist = d;
                    }
                }

                // 裁切内端：从 clippedOuter 沿 inward 射线，取最近弧线交点
                var clippedInner = innerPt;
                double innerMaxDist = clippedOuter.DistanceTo(innerPt);
                foreach (var arc in new[] { arm.LeftArc, arm.RightArc })
                {
                    var hit = RayHitArc(clippedOuter, inward, arc);
                    if (hit == null) continue;
                    double d = clippedOuter.DistanceTo(hit.Value);
                    if (d > 1e-4 && d < innerMaxDist)
                    {
                        clippedInner = hit.Value;
                        innerMaxDist = d;
                    }
                }

                double len = clippedInner.DistanceTo(clippedOuter);
                if (len > stripeSpacing * 0.05)
                    AddLine(tr, ms, clippedInner, clippedOuter, crosswalkLayerName);
            }
        }

        #region Private — 分析

        private class LineInfo
        {
            public Point3d InnerPoint;
            public Point3d OuterPoint;
            public Arc ConnectedArc;
            public Vector3d Direction;
        }

        private List<LineInfo> BuildConnectivityTolerant(List<Line> lines, List<Arc> arcs)
        {
            var result = new List<LineInfo>();
            foreach (var line in lines)
            {
                LineInfo info = null;
                foreach (var arc in arcs)
                {
                    if (IsNear(line.StartPoint, arc.StartPoint) || IsNear(line.StartPoint, arc.EndPoint))
                    {
                        info = new LineInfo
                        {
                            InnerPoint = line.StartPoint,
                            OuterPoint = line.EndPoint,
                            ConnectedArc = arc
                        };
                        break;
                    }
                    if (IsNear(line.EndPoint, arc.StartPoint) || IsNear(line.EndPoint, arc.EndPoint))
                    {
                        info = new LineInfo
                        {
                            InnerPoint = line.EndPoint,
                            OuterPoint = line.StartPoint,
                            ConnectedArc = arc
                        };
                        break;
                    }
                }
                if (info == null) continue;
                info.Direction = (info.OuterPoint - info.InnerPoint).GetNormal();
                result.Add(info);
            }
            return result;
        }

        private Dictionary<Arc, Point3d> ComputeArcCornersTolerant(
            List<LineInfo> lineInfos, out int validCount, out int skippedCount)
        {
            var arcToLines = new Dictionary<Arc, List<LineInfo>>();
            foreach (var info in lineInfos)
            {
                if (!arcToLines.ContainsKey(info.ConnectedArc))
                    arcToLines[info.ConnectedArc] = new List<LineInfo>();
                arcToLines[info.ConnectedArc].Add(info);
            }

            var corners = new Dictionary<Arc, Point3d>();
            int skipped = 0;
            foreach (var kvp in arcToLines)
            {
                var pair = kvp.Value;
                if (pair.Count != 2) { skipped++; continue; }

                var a = pair[0];
                var b = pair[1];
                var dA = (a.InnerPoint - a.OuterPoint).GetNormal();
                var dB = (b.InnerPoint - b.OuterPoint).GetNormal();

                var corner = LineLineIntersection2D(a.OuterPoint, dA, b.OuterPoint, dB);
                if (corner == null) { skipped++; continue; }

                var arcMid = new Point3d(
                    (a.InnerPoint.X + b.InnerPoint.X) / 2,
                    (a.InnerPoint.Y + b.InnerPoint.Y) / 2,
                    (a.InnerPoint.Z + b.InnerPoint.Z) / 2);
                if (a.InnerPoint.DistanceTo(b.InnerPoint) > 1e-6 &&
                    corner.Value.DistanceTo(arcMid) > a.InnerPoint.DistanceTo(b.InnerPoint) * 20)
                {
                    skipped++; continue;
                }

                corners[kvp.Key] = corner.Value;
            }
            validCount = corners.Count;
            skippedCount = skipped;
            return corners;
        }

        private List<RoadArm> GroupIntoArmsTolerant(
            List<LineInfo> lineInfos, Dictionary<Arc, Point3d> arcCorners)
        {
            var validLines = new List<LineInfo>();
            foreach (var li in lineInfos)
                if (arcCorners.ContainsKey(li.ConnectedArc))
                    validLines.Add(li);

            var arms = new List<RoadArm>();
            var used = new HashSet<int>();

            for (int i = 0; i < validLines.Count; i++)
            {
                if (used.Contains(i)) continue;
                double bestDot = -2;
                int bestJ = -1;

                for (int j = i + 1; j < validLines.Count; j++)
                {
                    if (used.Contains(j)) continue;
                    if (ReferenceEquals(validLines[i].ConnectedArc, validLines[j].ConnectedArc))
                        continue;
                    double dot = validLines[i].Direction.DotProduct(validLines[j].Direction);
                    if (dot > bestDot) { bestDot = dot; bestJ = j; }
                }

                if (bestJ < 0 || bestDot < 0.5) continue;
                used.Add(i);
                used.Add(bestJ);

                var li = validLines[i];
                var lj = validLines[bestJ];
                var avgDir = (li.Direction + lj.Direction).GetNormal();
                var lateral = lj.InnerPoint - li.InnerPoint;
                var cross = avgDir.CrossProduct(lateral);

                RoadArm arm;
                if (cross.Z >= 0)
                    arm = new RoadArm
                    {
                        LeftInner = li.InnerPoint, LeftOuter = li.OuterPoint,
                        RightInner = lj.InnerPoint, RightOuter = lj.OuterPoint,
                        LeftCorner = arcCorners[li.ConnectedArc],
                        RightCorner = arcCorners[lj.ConnectedArc],
                        LeftArc = li.ConnectedArc, RightArc = lj.ConnectedArc,
                    };
                else
                    arm = new RoadArm
                    {
                        LeftInner = lj.InnerPoint, LeftOuter = lj.OuterPoint,
                        RightInner = li.InnerPoint, RightOuter = li.OuterPoint,
                        LeftCorner = arcCorners[lj.ConnectedArc],
                        RightCorner = arcCorners[li.ConnectedArc],
                        LeftArc = lj.ConnectedArc, RightArc = li.ConnectedArc,
                    };

                arm.OutwardDirection = avgDir;
                arms.Add(arm);
            }
            return arms;
        }

        #endregion

        #region Private — 几何

        /// <summary>
        /// 射线与弧线求交，返回最近的有效交点（最小正 t 且在弧线角度范围内）。
        /// </summary>
        private Point3d? RayHitArc(Point3d origin, Vector3d dir, Arc arc)
        {
            if (arc == null) return null;

            double cx = arc.Center.X, cy = arc.Center.Y;
            double r = arc.Radius;
            double ox = origin.X - cx, oy = origin.Y - cy;
            double dx = dir.X, dy = dir.Y;

            double a = dx * dx + dy * dy;
            if (a < 1e-20) return null;

            double b = 2.0 * (ox * dx + oy * dy);
            double c = ox * ox + oy * oy - r * r;
            double disc = b * b - 4.0 * a * c;
            if (disc < 0) return null;

            double sqrtDisc = Math.Sqrt(disc);
            double t1 = (-b - sqrtDisc) / (2.0 * a);
            double t2 = (-b + sqrtDisc) / (2.0 * a);

            Point3d? result = null;
            double bestT = double.MaxValue;

            foreach (double t in new[] { t1, t2 })
            {
                if (t < -1e-6) continue;
                double px = origin.X + dx * t;
                double py = origin.Y + dy * t;
                double angle = Math.Atan2(py - cy, px - cx);

                if (IsAngleOnArc(angle, arc.StartAngle, arc.EndAngle) && t < bestT)
                {
                    bestT = t;
                    result = new Point3d(px, py, origin.Z);
                }
            }
            return result;
        }

        private bool IsAngleOnArc(double testAngle, double arcStart, double arcEnd)
        {
            testAngle = NormAngle(testAngle);
            arcStart = NormAngle(arcStart);
            arcEnd = NormAngle(arcEnd);
            const double eps = 0.002;

            if (arcEnd >= arcStart)
                return testAngle >= arcStart - eps && testAngle <= arcEnd + eps;
            else
                return testAngle >= arcStart - eps || testAngle <= arcEnd + eps;
        }

        private double NormAngle(double a)
        {
            const double TwoPi = Math.PI * 2.0;
            a = a % TwoPi;
            if (a < 0) a += TwoPi;
            return a;
        }

        private Point3d? LineLineIntersection2D(Point3d p1, Vector3d d1, Point3d p2, Vector3d d2)
        {
            double cross = d1.X * d2.Y - d1.Y * d2.X;
            if (Math.Abs(cross) < 1e-10) return null;
            var dp = p2 - p1;
            double t = (dp.X * d2.Y - dp.Y * d2.X) / cross;
            return p1 + d1 * t;
        }

        private Point3d ComputeCenter(List<RoadArm> arms)
        {
            double x = 0, y = 0, z = 0;
            int count = 0;
            foreach (var arm in arms)
            {
                x += arm.LeftCorner.X + arm.RightCorner.X;
                y += arm.LeftCorner.Y + arm.RightCorner.Y;
                z += arm.LeftCorner.Z + arm.RightCorner.Z;
                count += 2;
            }
            return new Point3d(x / count, y / count, z / count);
        }

        private void AddLine(Transaction tr, BlockTableRecord ms, Point3d start, Point3d end, string layerName)
        {
            var line = new Line(start, end);
            line.Layer = layerName;
            ms.AppendEntity(line);
            tr.AddNewlyCreatedDBObject(line, true);
        }

        private bool IsNear(Point3d a, Point3d b) => a.DistanceTo(b) < PointTolerance;

        #endregion
    }
}
