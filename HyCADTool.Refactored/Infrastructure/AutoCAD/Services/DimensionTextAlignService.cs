using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System;
using System.Collections.Generic;
using System.Linq;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services
{
    /// <summary>
    /// 标注文字防重叠服务：检测同一标注链上的文字包围盒重叠，
    /// 通过交错偏移 TextPosition 解决重叠，AutoCAD 会自动绘制引线。
    /// </summary>
    public class DimensionTextAlignService
    {
        private const double RotationTolerance = 0.01;
        private const double DimLineTolerance = 1.0;
        private const double CharWidthRatio = 0.7;

        /// <summary>
        /// 对一组 RotatedDimension 进行文字防重叠处理
        /// </summary>
        public int AlignDimensionTexts(ObjectId[] dimIds)
        {
            if (dimIds == null || dimIds.Length == 0) return 0;

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            int adjustedCount = 0;

            using (doc.LockDocument())
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var dims = new List<RotatedDimension>();
                foreach (var id in dimIds)
                {
                    var dim = tr.GetObject(id, OpenMode.ForWrite) as RotatedDimension;
                    if (dim != null) dims.Add(dim);
                }

                if (dims.Count == 0) { tr.Commit(); return 0; }

                var chains = GroupIntoDimChains(dims);

                foreach (var chain in chains)
                {
                    adjustedCount += ResolveChainOverlaps(chain);
                }

                tr.Commit();
            }

            return adjustedCount;
        }

        #region 分组：按标注线共线关系组成标注链

        private List<List<RotatedDimension>> GroupIntoDimChains(List<RotatedDimension> dims)
        {
            var rotationGroups = dims.GroupBy(d => Math.Round(d.Rotation / (Math.PI / 2)) * (Math.PI / 2),
                new DoubleApproxComparer(RotationTolerance));

            var chains = new List<List<RotatedDimension>>();

            foreach (var rotGroup in rotationGroups)
            {
                var remaining = rotGroup.ToList();
                while (remaining.Count > 0)
                {
                    var chain = new List<RotatedDimension> { remaining[0] };
                    remaining.RemoveAt(0);

                    bool added = true;
                    while (added)
                    {
                        added = false;
                        for (int i = remaining.Count - 1; i >= 0; i--)
                        {
                            if (IsOnSameDimLine(chain[0], remaining[i]))
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

        private bool IsOnSameDimLine(RotatedDimension a, RotatedDimension b)
        {
            if (Math.Abs(a.Rotation - b.Rotation) > RotationTolerance)
                return false;

            var dimDir = Vector3d.XAxis.RotateBy(a.Rotation, Vector3d.ZAxis);
            var perpDir = dimDir.RotateBy(Math.PI / 2, Vector3d.ZAxis);

            var diff = b.DimLinePoint - a.DimLinePoint;
            double perpDist = Math.Abs(diff.DotProduct(perpDir));

            return perpDist < DimLineTolerance;
        }

        private void SortChain(List<RotatedDimension> chain)
        {
            var dimDir = Vector3d.XAxis.RotateBy(chain[0].Rotation, Vector3d.ZAxis);

            chain.Sort((a, b) =>
            {
                double projA = MidPoint(a).GetAsVector().DotProduct(dimDir);
                double projB = MidPoint(b).GetAsVector().DotProduct(dimDir);
                return projA.CompareTo(projB);
            });
        }

        private Point3d MidPoint(RotatedDimension dim)
        {
            return new Point3d(
                (dim.XLine1Point.X + dim.XLine2Point.X) / 2,
                (dim.XLine1Point.Y + dim.XLine2Point.Y) / 2,
                0);
        }

        #endregion

        #region 重叠检测与解决

        private int ResolveChainOverlaps(List<RotatedDimension> chain)
        {
            if (chain.Count < 2) return 0;

            var boxes = chain.Select(d => GetTextBoundingBox(d)).ToList();

            var overlapIndices = new HashSet<int>();
            for (int i = 0; i < boxes.Count - 1; i++)
            {
                if (BoxesOverlap(boxes[i], boxes[i + 1]))
                {
                    overlapIndices.Add(i);
                    overlapIndices.Add(i + 1);
                }
            }

            if (overlapIndices.Count == 0) return 0;

            var overlapGroups = FindContiguousOverlapGroups(boxes);
            int adjustedCount = 0;

            foreach (var group in overlapGroups)
            {
                adjustedCount += StaggerGroup(chain, group);
            }

            return adjustedCount;
        }

        private List<List<int>> FindContiguousOverlapGroups(List<TextBox> boxes)
        {
            var groups = new List<List<int>>();
            int i = 0;

            while (i < boxes.Count - 1)
            {
                if (BoxesOverlap(boxes[i], boxes[i + 1]))
                {
                    var group = new List<int> { i };
                    while (i < boxes.Count - 1 && BoxesOverlap(boxes[i], boxes[i + 1]))
                    {
                        group.Add(i + 1);
                        i++;
                    }
                    groups.Add(group);
                }
                else
                {
                    i++;
                }
            }

            return groups;
        }

        /// <summary>
        /// 对一组连续重叠的标注进行交错偏移。
        /// 保留中间位置的标注不动，其余向两侧交替偏移。
        /// </summary>
        private int StaggerGroup(List<RotatedDimension> chain, List<int> groupIndices)
        {
            if (groupIndices.Count < 2) return 0;

            var dim0 = chain[groupIndices[0]];
            var perpDir = Vector3d.XAxis.RotateBy(dim0.Rotation + Math.PI / 2, Vector3d.ZAxis);

            double textH = GetTextHeight(dim0);
            double offsetStep = textH * 2.5;

            int count = groupIndices.Count;
            int midIndex = count / 2;
            int adjusted = 0;

            for (int i = 0; i < count; i++)
            {
                int dimIdx = groupIndices[i];
                var dim = chain[dimIdx];

                int staggerOrder = i - midIndex;
                if (staggerOrder == 0) continue;

                double offset = staggerOrder * offsetStep;
                var defaultTextPos = GetDefaultTextPosition(dim);
                dim.TextPosition = defaultTextPos + perpDir * offset;
                adjusted++;
            }

            return adjusted;
        }

        #endregion

        #region 文字包围盒计算

        private struct TextBox
        {
            public double MinX, MaxX, MinY, MaxY;
        }

        private TextBox GetTextBoundingBox(RotatedDimension dim)
        {
            var textPos = dim.TextPosition;
            double textH = GetTextHeight(dim);
            string measureText = dim.Measurement.ToString("F0");
            if (!string.IsNullOrWhiteSpace(dim.DimensionText))
                measureText = dim.DimensionText.Replace("<>", dim.Measurement.ToString("F0"));
            double textW = measureText.Length * textH * CharWidthRatio;

            bool isVertical = Math.Abs(Math.Abs(dim.Rotation) - Math.PI / 2) < RotationTolerance;

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

        private double GetTextHeight(RotatedDimension dim)
        {
            double scale = dim.Dimscale;
            if (scale < 1e-6) scale = 1.0;
            double txtSize = dim.Dimtxt;
            if (txtSize < 1e-6) txtSize = 2.5;
            return scale * txtSize;
        }

        private Point3d GetDefaultTextPosition(RotatedDimension dim)
        {
            var dimDir = Vector3d.XAxis.RotateBy(dim.Rotation, Vector3d.ZAxis);
            var mid = MidPoint(dim);
            var perpDir = dimDir.RotateBy(Math.PI / 2, Vector3d.ZAxis);

            var diff = dim.DimLinePoint - mid;
            double perpOffset = diff.DotProduct(perpDir);
            var dimLineCenter = mid + perpDir * perpOffset;

            double textH = GetTextHeight(dim);
            double gap = dim.Dimscale * dim.Dimgap;
            if (gap < 1e-6) gap = textH * 0.5;

            return dimLineCenter + perpDir * (gap + textH / 2);
        }

        private bool BoxesOverlap(TextBox a, TextBox b)
        {
            if (a.MaxX <= b.MinX || b.MaxX <= a.MinX) return false;
            if (a.MaxY <= b.MinY || b.MaxY <= a.MinY) return false;
            return true;
        }

        #endregion

        #region 辅助类

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
