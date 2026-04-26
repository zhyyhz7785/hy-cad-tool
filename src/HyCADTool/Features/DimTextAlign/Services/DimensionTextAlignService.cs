using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Features.DimTextAlign.Services
{
    /// <summary>
    /// 标注文字防重叠服务：支持所有 Dimension 子类。
    /// 迭代式贪心：每轮移动碰撞对中 Measurement 最小的标注，推最小距离，重新检测。
    /// </summary>
    public class DimensionTextAlignService
    {
        private const double RotationTolerance = 0.05;
        private const double CharWidthRatio = 0.7;
        private const int MaxIterations = 50;
        private const double MinGapRatio = 0.3;

        public int AlignDimensionTexts(ObjectId[] dimIds, out string diagnostics)
        {
            diagnostics = "";
            if (dimIds == null || dimIds.Length == 0) return 0;

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            int adjustedCount = 0;
            var diag = new System.Text.StringBuilder();

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var dims = new List<Dimension>();
                foreach (var id in dimIds)
                {
                    var ent = tr.GetObject(id, OpenMode.ForWrite);
                    if (ent is Dimension dim)
                        dims.Add(dim);
                }

                diag.AppendLine($"Dimension 数量: {dims.Count} ({string.Join("/", dims.Select(d => d.GetType().Name).Distinct())})");
                if (dims.Count == 0) { tr.Commit(); diagnostics = diag.ToString(); return 0; }

                foreach (var d in dims)
                {
                    double rot = GetDimRotation(d);
                    var dlp = GetDimLinePoint(d);
                    diag.AppendLine($"  [{d.GetType().Name}] M={d.Measurement:F0} Rot={rot:F4} " +
                        $"DLP=({dlp.X:F0},{dlp.Y:F0}) " +
                        $"TP=({d.TextPosition.X:F0},{d.TextPosition.Y:F0}) " +
                        $"sc={d.Dimscale} txt={d.Dimtxt}");
                }

                var chains = GroupIntoDimChains(dims);
                diag.AppendLine($"标注链: {chains.Count} 条");
                for (int c = 0; c < chains.Count; c++)
                {
                    diag.Append($"  [{c}] ");
                    diag.AppendLine(string.Join(", ", chains[c].Select(d => d.Measurement.ToString("F0"))));
                }

                foreach (var chain in chains)
                {
                    adjustedCount += ResolveChainOverlaps(chain, diag);
                }

                tr.Commit();
            }

            diagnostics = diag.ToString();
            return adjustedCount;
        }

        #region Dimension 属性提取（适配所有子类）

        private static Point3d GetXLine1Point(Dimension dim)
        {
            if (dim is RotatedDimension rd) return rd.XLine1Point;
            if (dim is AlignedDimension ad) return ad.XLine1Point;
            if (dim is Point3AngularDimension p3) return p3.XLine1Point;
            if (dim is LineAngularDimension2 la) return la.XLine1Start;
            return dim.TextPosition;
        }

        private static Point3d GetXLine2Point(Dimension dim)
        {
            if (dim is RotatedDimension rd) return rd.XLine2Point;
            if (dim is AlignedDimension ad) return ad.XLine2Point;
            if (dim is Point3AngularDimension p3) return p3.XLine2Point;
            if (dim is LineAngularDimension2 la) return la.XLine1End;
            return dim.TextPosition;
        }

        private static Point3d GetDimLinePoint(Dimension dim)
        {
            if (dim is RotatedDimension rd) return rd.DimLinePoint;
            if (dim is AlignedDimension ad) return ad.DimLinePoint;
            if (dim is Point3AngularDimension p3) return p3.ArcPoint;
            return dim.TextPosition;
        }

        private double GetDimRotation(Dimension dim)
        {
            if (dim is RotatedDimension rd)
                return rd.Rotation;

            var p1 = GetXLine1Point(dim);
            var p2 = GetXLine2Point(dim);
            var v = p2 - p1;
            if (v.Length < 1e-6) return 0;
            bool vertical = Math.Abs(v.Y) > Math.Abs(v.X);
            return vertical ? Math.PI / 2 : 0;
        }

        #endregion

        #region 分组

        private List<List<Dimension>> GroupIntoDimChains(List<Dimension> dims)
        {
            var rotationGroups = dims.GroupBy(
                d => Math.Round(GetDimRotation(d) / (Math.PI / 2)) * (Math.PI / 2),
                new DoubleApproxComparer(RotationTolerance));

            var chains = new List<List<Dimension>>();

            foreach (var rotGroup in rotationGroups)
            {
                var remaining = rotGroup.ToList();
                while (remaining.Count > 0)
                {
                    var chain = new List<Dimension> { remaining[0] };
                    remaining.RemoveAt(0);

                    bool added = true;
                    while (added)
                    {
                        added = false;
                        for (int i = remaining.Count - 1; i >= 0; i--)
                        {
                            if (IsOnSameDimLine(chain, remaining[i]))
                            {
                                chain.Add(remaining[i]);
                                remaining.RemoveAt(i);
                                added = true;
                            }
                        }
                    }

                    if (chain.Count > 1)
                    {
                        SortChain(chain);
                        chains.Add(chain);
                    }
                }
            }

            return chains;
        }

        private bool IsOnSameDimLine(List<Dimension> chain, Dimension candidate)
        {
            double candRot = GetDimRotation(candidate);
            foreach (var member in chain)
            {
                double memRot = GetDimRotation(member);
                if (Math.Abs(memRot - candRot) > RotationTolerance)
                    continue;

                var dimDir = Vector3d.XAxis.RotateBy(memRot, Vector3d.ZAxis);
                var perpDir = dimDir.RotateBy(Math.PI / 2, Vector3d.ZAxis);

                var diff = GetDimLinePoint(candidate) - GetDimLinePoint(member);
                double perpDist = Math.Abs(diff.DotProduct(perpDir));

                double scale = Math.Max(member.Dimscale, 1.0);
                double tolerance = scale * 10;

                if (perpDist < tolerance)
                    return true;
            }
            return false;
        }

        private void SortChain(List<Dimension> chain)
        {
            double rot = GetDimRotation(chain[0]);
            var dimDir = Vector3d.XAxis.RotateBy(rot, Vector3d.ZAxis);

            chain.Sort((a, b) =>
            {
                double projA = MidPoint(a).GetAsVector().DotProduct(dimDir);
                double projB = MidPoint(b).GetAsVector().DotProduct(dimDir);
                return projA.CompareTo(projB);
            });
        }

        private Point3d MidPoint(Dimension dim)
        {
            var p1 = GetXLine1Point(dim);
            var p2 = GetXLine2Point(dim);
            return new Point3d((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2, 0);
        }

        #endregion

        #region 迭代式贪心

        private int ResolveChainOverlaps(List<Dimension> chain, System.Text.StringBuilder diag)
        {
            if (chain.Count < 2) return 0;

            double rot = GetDimRotation(chain[0]);
            bool isVertical = Math.Abs(Math.Abs(rot) - Math.PI / 2) < RotationTolerance;
            var dimDir = Vector3d.XAxis.RotateBy(rot, Vector3d.ZAxis);
            var perpDir = dimDir.RotateBy(Math.PI / 2, Vector3d.ZAxis);

            var boxes = chain.Select(d => GetTextBoundingBox(d)).ToList();
            var adjustedSet = new HashSet<int>();

            for (int round = 0; round < MaxIterations; round++)
            {
                var pair = FindSmallestCollidingTarget(chain, boxes);
                if (pair == null) break;

                int targetIdx = pair.Item1;
                int otherIdx = pair.Item2;

                double overlap = OverlapDepth(boxes[targetIdx], boxes[otherIdx], isVertical);
                double textH = GetTextHeight(chain[targetIdx]);
                double moveAmount = overlap + textH * MinGapRatio;

                diag.AppendLine($"  R{round}: M={chain[targetIdx].Measurement:F0}(#{targetIdx})" +
                    $" <- M={chain[otherIdx].Measurement:F0}(#{otherIdx})" +
                    $" olap={overlap:F0} mv={moveAmount:F0}");

                var flipDir = GetFlipDirection(chain[targetIdx], perpDir);
                FlipTextToOtherSide(chain[targetIdx], flipDir, textH);
                boxes[targetIdx] = GetTextBoundingBox(chain[targetIdx]);

                if (CheckNewCollision(targetIdx, boxes))
                {
                    diag.AppendLine($"    -> 对侧仍碰撞，沿链方向外推");
                    var chainDir = ChooseMoveDirection(targetIdx, otherIdx, chain, dimDir);
                    ShiftTextPosition(chain[targetIdx], chainDir, moveAmount);
                    boxes[targetIdx] = GetTextBoundingBox(chain[targetIdx]);
                }

                adjustedSet.Add(targetIdx);
            }

            return adjustedSet.Count;
        }

        private Tuple<int, int> FindSmallestCollidingTarget(List<Dimension> chain, List<TextBox> boxes)
        {
            int bestTarget = -1, bestOther = -1;
            double bestMeasurement = double.MaxValue;

            for (int i = 0; i < boxes.Count; i++)
            {
                for (int j = i + 1; j < boxes.Count; j++)
                {
                    if (!BoxesOverlap(boxes[i], boxes[j])) continue;

                    double mI = chain[i].Measurement;
                    double mJ = chain[j].Measurement;

                    if (mI <= mJ && mI < bestMeasurement)
                    {
                        bestMeasurement = mI;
                        bestTarget = i;
                        bestOther = j;
                    }
                    else if (mJ < mI && mJ < bestMeasurement)
                    {
                        bestMeasurement = mJ;
                        bestTarget = j;
                        bestOther = i;
                    }
                }
            }

            return bestTarget >= 0 ? Tuple.Create(bestTarget, bestOther) : null;
        }

        private double OverlapDepth(TextBox a, TextBox b, bool isVertical)
        {
            double depth = isVertical
                ? Math.Min(a.MaxY, b.MaxY) - Math.Max(a.MinY, b.MinY)
                : Math.Min(a.MaxX, b.MaxX) - Math.Max(a.MinX, b.MinX);
            return Math.Max(depth, 0);
        }

        /// <summary>
        /// 判断文字当前在标注线哪一侧，返回指向对侧的方向。
        /// </summary>
        private Vector3d GetFlipDirection(Dimension dim, Vector3d perpDir)
        {
            var textPos = dim.TextPosition;
            var dlp = GetDimLinePoint(dim);
            double side = (textPos - dlp).DotProduct(perpDir);
            return side >= 0 ? perpDir.Negate() : perpDir;
        }

        /// <summary>
        /// 将文字翻转到标注线对侧：以 DimLinePoint 为镜像基准，
        /// 把文字放到对称位置（再加 gap+textH/2 确保不贴线）。
        /// </summary>
        private void FlipTextToOtherSide(Dimension dim, Vector3d flipDir, double textH)
        {
            var mid = MidPoint(dim);
            var dlp = GetDimLinePoint(dim);
            var perpDir = flipDir.Length > 0.5 ? flipDir.GetNormal() : flipDir;

            double gap = dim.Dimscale * dim.Dimgap;
            if (gap < 1e-6) gap = textH * 0.5;

            var dimLineCenter = mid + (dlp - mid);
            dim.TextPosition = dimLineCenter + perpDir * (gap + textH / 2);
        }

        private Vector3d ChooseMoveDirection(int targetIdx, int otherIdx,
            List<Dimension> chain, Vector3d dimDir)
        {
            var targetMid = MidPoint(chain[targetIdx]);
            var otherMid = MidPoint(chain[otherIdx]);
            double proj = (targetMid - otherMid).DotProduct(dimDir);
            return proj >= 0 ? dimDir : dimDir.Negate();
        }

        private void ShiftTextPosition(Dimension dim, Vector3d direction, double amount)
        {
            dim.TextPosition = dim.TextPosition + direction * amount;
        }

        private bool CheckNewCollision(int targetIdx, List<TextBox> boxes)
        {
            for (int i = 0; i < boxes.Count; i++)
            {
                if (i == targetIdx) continue;
                if (BoxesOverlap(boxes[targetIdx], boxes[i]))
                    return true;
            }
            return false;
        }

        #endregion

        #region 包围盒

        private struct TextBox
        {
            public double MinX, MaxX, MinY, MaxY;
        }

        private TextBox GetTextBoundingBox(Dimension dim)
        {
            var textPos = dim.TextPosition;
            double textH = GetTextHeight(dim);
            string measureText = dim.Measurement.ToString("F0");
            if (!string.IsNullOrWhiteSpace(dim.DimensionText))
                measureText = dim.DimensionText.Replace("<>", dim.Measurement.ToString("F0"));
            double textW = Math.Max(measureText.Length, 1) * textH * CharWidthRatio;

            double rot = GetDimRotation(dim);
            bool isVertical = Math.Abs(Math.Abs(rot) - Math.PI / 2) < RotationTolerance;

            double halfW, halfH;
            if (isVertical)
            {
                halfW = textH / 2;
                halfH = textW / 2;
            }
            else
            {
                halfW = textW / 2;
                halfH = textH / 2;
            }

            return new TextBox
            {
                MinX = textPos.X - halfW,
                MaxX = textPos.X + halfW,
                MinY = textPos.Y - halfH,
                MaxY = textPos.Y + halfH
            };
        }

        private double GetTextHeight(Dimension dim)
        {
            double scale = dim.Dimscale;
            if (scale < 1e-6) scale = 1.0;
            double txtSize = dim.Dimtxt;
            if (txtSize < 1e-6) txtSize = 2.5;
            return scale * txtSize;
        }

        private bool BoxesOverlap(TextBox a, TextBox b)
        {
            if (a.MaxX <= b.MinX || b.MaxX <= a.MinX) return false;
            if (a.MaxY <= b.MinY || b.MaxY <= a.MinY) return false;
            return true;
        }

        #endregion

        #region 辅助

        private class DoubleApproxComparer : IEqualityComparer<double>
        {
            private readonly double _tol;
            public DoubleApproxComparer(double tolerance) { _tol = tolerance; }

            public bool Equals(double x, double y) => Math.Abs(x - y) < _tol;
            public int GetHashCode(double obj) => Math.Round(obj / _tol).GetHashCode();
        }

        #endregion
    }
}
