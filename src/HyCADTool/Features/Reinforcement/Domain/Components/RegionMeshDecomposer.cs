using HyCAD.Geometry;
using HyCADTool.Features.Reinforcement.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace HyCADTool.Features.Reinforcement.Domain.Components
{
    /// <summary>
    /// 混凝土区域网格化：竖直条带梯形 → 轴对齐矩形 + 斜边三角形，矩形横/竖向合并最大化。
    /// </summary>
    public static class RegionMeshDecomposer
    {
        private const double YToleranceMm = 0.01;
        private const double MergeToleranceMm = 1.0;
        private const double AspectRatioThreshold = 1.5;

        private sealed class TrapezoidPiece
        {
            public double X0, X1, TopL, TopR, BotL, BotR;
            public Line2D TopSeg, BotSeg;
        }

        private sealed class RectPiece
        {
            public double X0, X1, YBot, YTop;
        }

        /// <summary>分解 ReinRegion 为矩形 + 三角形网格单元（纯几何，不按 cutY 切分）。</summary>
        public static List<MeshCell> Decompose(ReinRegion region)
        {
            if (region?.Outer == null || region.Outer.VertexCount < 3)
                return new List<MeshCell>();

            var trapezoids = CollectTrapezoidPieces(region);
            var rectPieces = new List<RectPiece>();
            var triangles = new List<MeshCell>();

            foreach (var trap in trapezoids)
                SplitTrapezoidIntoParts(trap, rectPieces, triangles);

            var mergedRects = MergeRectanglesHorizontally(rectPieces);
            mergedRects = MergeRectanglesVertically(mergedRects);

            var result = new List<MeshCell>(mergedRects.Count + triangles.Count);
            result.AddRange(mergedRects.Select(r => CreateRectangleCell(r.X0, r.X1, r.YBot, r.YTop)));
            result.AddRange(triangles);
            return result;
        }

        private static List<TrapezoidPiece> CollectTrapezoidPieces(ReinRegion region)
        {
            var pieces = new List<TrapezoidPiece>();
            var segments = StripGeometry.CollectSegments(region);
            var breakpoints = StripGeometry.CollectBreakpoints(region);
            if (breakpoints.Count < 2)
                return pieces;

            var active = new List<TrapezoidPiece>();

            for (int i = 0; i < breakpoints.Count - 1; i++)
            {
                double x0 = breakpoints[i];
                double x1 = breakpoints[i + 1];
                if (x1 - x0 < StripGeometry.MinStripWidthMm)
                    continue;

                double xm = (x0 + x1) / 2.0;
                var intervals = StripGeometry.GetInsideIntervalsAt(region, segments, xm);
                var usedActive = new HashSet<TrapezoidPiece>();
                var nextActive = new List<TrapezoidPiece>();

                foreach (var iv in intervals)
                {
                    if (iv == null)
                        continue;

                    TrapezoidPiece match = FindExtendableActive(active, usedActive, iv, x0);
                    if (match != null)
                    {
                        usedActive.Add(match);
                        match.X1 = x1;
                        match.TopR = StripGeometry.EvaluateYOnSegment(iv.TopSeg, x1);
                        match.BotR = StripGeometry.EvaluateYOnSegment(iv.BotSeg, x1);
                        nextActive.Add(match);
                    }
                    else
                    {
                        nextActive.Add(new TrapezoidPiece
                        {
                            X0 = x0,
                            X1 = x1,
                            TopSeg = iv.TopSeg,
                            BotSeg = iv.BotSeg,
                            TopL = StripGeometry.EvaluateYOnSegment(iv.TopSeg, x0),
                            TopR = StripGeometry.EvaluateYOnSegment(iv.TopSeg, x1),
                            BotL = StripGeometry.EvaluateYOnSegment(iv.BotSeg, x0),
                            BotR = StripGeometry.EvaluateYOnSegment(iv.BotSeg, x1)
                        });
                    }
                }

                foreach (var openPiece in active)
                {
                    if (!usedActive.Contains(openPiece))
                        pieces.Add(openPiece);
                }

                active = nextActive;
            }

            pieces.AddRange(active);
            return pieces;
        }

        private static TrapezoidPiece FindExtendableActive(
            IReadOnlyList<TrapezoidPiece> active,
            HashSet<TrapezoidPiece> usedActive,
            StripInsideInterval iv,
            double x0)
        {
            foreach (var candidate in active)
            {
                if (usedActive.Contains(candidate))
                    continue;
                if (!ReferenceEquals(candidate.TopSeg, iv.TopSeg))
                    continue;
                if (!ReferenceEquals(candidate.BotSeg, iv.BotSeg))
                    continue;
                if (Math.Abs(candidate.X1 - x0) > MergeToleranceMm)
                    continue;

                return candidate;
            }

            return null;
        }

        private static void SplitTrapezoidIntoParts(
            TrapezoidPiece t,
            List<RectPiece> rectPieces,
            List<MeshCell> triangles)
        {
            double yt = Math.Min(t.TopL, t.TopR);
            double yb = Math.Max(t.BotL, t.BotR);

            if (yt - yb >= StripGeometry.MinIntervalHeightMm)
            {
                rectPieces.Add(new RectPiece
                {
                    X0 = t.X0,
                    X1 = t.X1,
                    YBot = yb,
                    YTop = yt
                });
            }
            else if (Math.Abs(t.TopL - t.TopR) < YToleranceMm
                     && Math.Abs(t.BotL - t.BotR) < YToleranceMm)
            {
                return;
            }

            AddTopTriangles(t, yt, triangles);
            AddBottomTriangles(t, yb, triangles);

            if (yt - yb < StripGeometry.MinIntervalHeightMm
                && (Math.Abs(t.TopL - t.TopR) >= YToleranceMm || Math.Abs(t.BotL - t.BotR) >= YToleranceMm))
            {
                AddTrapezoidDiagonalTriangles(t, triangles);
            }
        }

        private static void AddTopTriangles(TrapezoidPiece t, double yt, List<MeshCell> triangles)
        {
            if (Math.Abs(t.TopL - t.TopR) < YToleranceMm)
                return;

            if (t.TopL > t.TopR)
                AddTriangle(triangles, t.X0, t.TopL, t.X0, yt, t.X1, yt);
            else
                AddTriangle(triangles, t.X1, t.TopR, t.X1, yt, t.X0, yt);
        }

        private static void AddBottomTriangles(TrapezoidPiece t, double yb, List<MeshCell> triangles)
        {
            if (Math.Abs(t.BotL - t.BotR) < YToleranceMm)
                return;

            if (t.BotL > t.BotR)
                AddTriangle(triangles, t.X1, t.BotR, t.X1, yb, t.X0, yb);
            else
                AddTriangle(triangles, t.X0, t.BotL, t.X0, yb, t.X1, yb);
        }

        private static void AddTrapezoidDiagonalTriangles(TrapezoidPiece t, List<MeshCell> triangles)
        {
            AddTriangle(triangles, t.X0, t.TopL, t.X1, t.TopR, t.X1, t.BotR);
            AddTriangle(triangles, t.X0, t.TopL, t.X1, t.BotR, t.X0, t.BotL);
        }

        private static List<RectPiece> MergeRectanglesHorizontally(List<RectPiece> rectPieces)
        {
            if (rectPieces.Count == 0)
                return new List<RectPiece>();

            var groups = rectPieces
                .GroupBy(r => (
                    YBot: Math.Round(r.YBot / YToleranceMm) * YToleranceMm,
                    YTop: Math.Round(r.YTop / YToleranceMm) * YToleranceMm))
                .ToList();

            var merged = new List<RectPiece>();
            foreach (var group in groups)
            {
                var sorted = group.OrderBy(r => r.X0).ToList();
                double runX0 = sorted[0].X0;
                double runX1 = sorted[0].X1;
                double yBot = sorted[0].YBot;
                double yTop = sorted[0].YTop;

                for (int i = 1; i < sorted.Count; i++)
                {
                    var cur = sorted[i];
                    if (cur.X0 - runX1 <= MergeToleranceMm
                        && Math.Abs(cur.YBot - yBot) < YToleranceMm
                        && Math.Abs(cur.YTop - yTop) < YToleranceMm)
                    {
                        runX1 = Math.Max(runX1, cur.X1);
                    }
                    else
                    {
                        merged.Add(new RectPiece { X0 = runX0, X1 = runX1, YBot = yBot, YTop = yTop });
                        runX0 = cur.X0;
                        runX1 = cur.X1;
                        yBot = cur.YBot;
                        yTop = cur.YTop;
                    }
                }

                merged.Add(new RectPiece { X0 = runX0, X1 = runX1, YBot = yBot, YTop = yTop });
            }

            return merged;
        }

        private static List<RectPiece> MergeRectanglesVertically(List<RectPiece> rectPieces)
        {
            if (rectPieces.Count == 0)
                return new List<RectPiece>();

            var groups = rectPieces
                .GroupBy(r => (
                    X0: Math.Round(r.X0 / YToleranceMm) * YToleranceMm,
                    X1: Math.Round(r.X1 / YToleranceMm) * YToleranceMm))
                .ToList();

            var merged = new List<RectPiece>();
            foreach (var group in groups)
            {
                var sorted = group.OrderBy(r => r.YBot).ToList();
                double runX0 = sorted[0].X0;
                double runX1 = sorted[0].X1;
                double yBot = sorted[0].YBot;
                double yTop = sorted[0].YTop;

                for (int i = 1; i < sorted.Count; i++)
                {
                    var cur = sorted[i];
                    if (cur.YBot - yTop <= MergeToleranceMm
                        && Math.Abs(cur.X0 - runX0) < YToleranceMm
                        && Math.Abs(cur.X1 - runX1) < YToleranceMm)
                    {
                        yTop = Math.Max(yTop, cur.YTop);
                    }
                    else
                    {
                        merged.Add(new RectPiece { X0 = runX0, X1 = runX1, YBot = yBot, YTop = yTop });
                        runX0 = cur.X0;
                        runX1 = cur.X1;
                        yBot = cur.YBot;
                        yTop = cur.YTop;
                    }
                }

                merged.Add(new RectPiece { X0 = runX0, X1 = runX1, YBot = yBot, YTop = yTop });
            }

            return merged;
        }

        private static MeshCell CreateRectangleCell(double x0, double x1, double yBot, double yTop)
        {
            var poly = new Polygon2D(new[]
            {
                new Point2D(x0, yBot),
                new Point2D(x1, yBot),
                new Point2D(x1, yTop),
                new Point2D(x0, yTop)
            }, isClosed: true);

            return new MeshCell
            {
                Kind = MeshCellKind.Rectangle,
                Orientation = ClassifyRectangleOrientation(x1 - x0, yTop - yBot),
                Polygon = poly,
                AreaMm2 = poly.GetArea()
            };
        }

        private static MeshCellOrientation ClassifyRectangleOrientation(double widthMm, double heightMm)
        {
            double shortSide = Math.Max(Math.Min(widthMm, heightMm), 1e-6);
            double ratio = Math.Max(widthMm, heightMm) / shortSide;

            if (ratio < AspectRatioThreshold)
                return MeshCellOrientation.Square;

            return heightMm > widthMm
                ? MeshCellOrientation.Vertical
                : MeshCellOrientation.Horizontal;
        }

        private static void AddTriangle(
            List<MeshCell> triangles,
            double x1, double y1,
            double x2, double y2,
            double x3, double y3)
        {
            if (Math.Abs((x2 - x1) * (y3 - y1) - (x3 - x1) * (y2 - y1)) < YToleranceMm * YToleranceMm)
                return;

            var poly = new Polygon2D(new[]
            {
                new Point2D(x1, y1),
                new Point2D(x2, y2),
                new Point2D(x3, y3)
            }, isClosed: true);

            if (poly.GetArea() < StripGeometry.MinIntervalHeightMm)
                return;

            triangles.Add(new MeshCell
            {
                Kind = MeshCellKind.Triangle,
                Polygon = poly,
                AreaMm2 = poly.GetArea()
            });
        }
    }
}
