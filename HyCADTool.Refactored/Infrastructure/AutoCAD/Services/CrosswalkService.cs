using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services
{
    /// <summary>
    /// 人行横道绘制服务
    /// 从选中的直线和圆弧中识别交叉口道路方向，
    /// 绘制辅助线、偏移线、斑马线条纹和停止线。
    /// 容错：不要求固定数量，自动过滤无关实体。
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

        /// <summary>
        /// 分析交叉口：容错模式，自动过滤无关实体
        /// </summary>
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
                    $"至少需要 2 条与圆弧相连的直线才能构成 1 个方向，" +
                    $"当前仅 {lineInfos.Count} 条相连（共选 {lines.Count} 条直线）。");

            var arcCorners = ComputeArcCornersTolerant(lineInfos,
                out int validArcCount, out int skippedArcCount);
            result.ValidArcCount = validArcCount;
            result.SkippedArcCount = skippedArcCount;

            if (arcCorners.Count < 2)
                throw new Exception(
                    $"至少需要 2 个有效角点才能构成 1 个方向，" +
                    $"当前仅 {arcCorners.Count} 个（需每个圆弧恰好连接 2 条直线）。");

            result.Arms = GroupIntoArmsTolerant(lineInfos, arcCorners);

            if (result.Arms.Count == 0)
                throw new Exception("无法配对出任何道路方向，请检查选择的实体。");

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
        /// 为一个道路方向绘制人行横道（所有距离均为绘图单位）
        /// </summary>
        public void DrawCrosswalkForArm(Transaction tr, BlockTableRecord ms, RoadArm arm,
            double gapWidth, double crosswalkWidth, double stopLineDistance, double stripeSpacing,
            string auxLayerName, string crosswalkLayerName, string stopLineLayerName)
        {
            var outward = arm.OutwardDirection;
            var baseLeft = arm.LeftCorner;
            var baseRight = arm.RightCorner;

            var vecGap = outward * gapWidth;
            var vecCrosswalk = outward * (gapWidth + crosswalkWidth);
            var vecStop = outward * (gapWidth + crosswalkWidth + stopLineDistance);

            var l2Left = baseLeft + vecGap;
            var l2Right = baseRight + vecGap;
            AddLine(tr, ms, l2Left, l2Right, auxLayerName);

            var l3Left = baseLeft + vecCrosswalk;
            var l3Right = baseRight + vecCrosswalk;
            AddLine(tr, ms, l3Left, l3Right, auxLayerName);

            var l4Left = baseLeft + vecStop;
            var l4Right = baseRight + vecStop;
            AddLine(tr, ms, l4Left, l4Right, stopLineLayerName);

            var roadWidthVec = l2Right - l2Left;
            var roadWidth = roadWidthVec.Length;
            if (roadWidth < 1e-6)
                throw new Exception($"路宽≈0（{roadWidth:G4}），角点可能重合。");

            var roadDir = roadWidthVec.GetNormal();

            int stripeCount = (int)(roadWidth / stripeSpacing);
            for (int i = 0; i <= stripeCount; i++)
            {
                double t = i * stripeSpacing;
                if (t > roadWidth + 0.001) break;

                var offset = roadDir * t;
                AddLine(tr, ms, l2Left + offset, l3Left + offset, crosswalkLayerName);
            }
        }

        #region Private

        private class LineInfo
        {
            public Point3d InnerPoint;
            public Point3d OuterPoint;
            public Arc ConnectedArc;
            public Vector3d Direction;
        }

        /// <summary>
        /// 容错连接：跳过无法匹配圆弧的直线（不抛异常）
        /// </summary>
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

        /// <summary>
        /// 容错角点：跳过连接数 ≠ 2 的圆弧和平行线对
        /// </summary>
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
                double distToMid = corner.Value.DistanceTo(arcMid);
                double span = a.InnerPoint.DistanceTo(b.InnerPoint);
                if (span > 1e-6 && distToMid > span * 20)
                {
                    skipped++;
                    continue;
                }

                corners[kvp.Key] = corner.Value;
            }

            validCount = corners.Count;
            skippedCount = skipped;
            return corners;
        }

        /// <summary>
        /// 容错配对：跳过无法配对的直线（不抛异常）
        /// </summary>
        private List<RoadArm> GroupIntoArmsTolerant(
            List<LineInfo> lineInfos, Dictionary<Arc, Point3d> arcCorners)
        {
            var validLines = new List<LineInfo>();
            foreach (var li in lineInfos)
            {
                if (arcCorners.ContainsKey(li.ConnectedArc))
                    validLines.Add(li);
            }

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
                    if (dot > bestDot)
                    {
                        bestDot = dot;
                        bestJ = j;
                    }
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
                {
                    arm = new RoadArm
                    {
                        LeftInner = li.InnerPoint,
                        LeftOuter = li.OuterPoint,
                        RightInner = lj.InnerPoint,
                        RightOuter = lj.OuterPoint,
                        LeftCorner = arcCorners[li.ConnectedArc],
                        RightCorner = arcCorners[lj.ConnectedArc],
                    };
                }
                else
                {
                    arm = new RoadArm
                    {
                        LeftInner = lj.InnerPoint,
                        LeftOuter = lj.OuterPoint,
                        RightInner = li.InnerPoint,
                        RightOuter = li.OuterPoint,
                        LeftCorner = arcCorners[lj.ConnectedArc],
                        RightCorner = arcCorners[li.ConnectedArc],
                    };
                }

                arm.OutwardDirection = avgDir;
                arms.Add(arm);
            }

            return arms;
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

        private bool IsNear(Point3d a, Point3d b)
        {
            return a.DistanceTo(b) < PointTolerance;
        }

        #endregion
    }
}
