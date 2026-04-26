using HyCADTool.Features.Pile.Domain.Entities;
using HyCADTool.Shared.Geometry;
using System;
using System.Collections.Generic;

namespace HyCADTool.Features.Pile.Domain.Services
{
    /// <summary>
    /// 桩布置算法服务（纯 Domain，平台无关）
    /// 移植自旧 GeometryUtils + StandardArea 逻辑
    /// 负责：内缩矩形计算、网格尺寸计算、桩点生成
    /// </summary>
    public class PileLayoutService
    {
        #region 数据结构

        /// <summary>
        /// 轴对齐矩形（Axis-Aligned Rectangle）
        /// </summary>
        public struct Rect
        {
            public double X0, Y0, Width, Height;

            public Rect(double x0, double y0, double width, double height)
            {
                X0 = x0; Y0 = y0; Width = width; Height = height;
            }

            public double Area => Width * Height;
            public double X1 => X0 + Width;
            public double Y1 => Y0 + Height;
        }

        /// <summary>
        /// 桩布置结果
        /// </summary>
        public class PileLayoutResult
        {
            /// <summary>桩中心点列表</summary>
            public List<Point2D> PilePoints { get; set; } = new List<Point2D>();
            /// <summary>小矩形顶点列表（每个矩形4个顶点）</summary>
            public List<Rect> SmallRects { get; set; } = new List<Rect>();
            /// <summary>内缩矩形</summary>
            public Rect InsetRect { get; set; }
            /// <summary>轮廓矩形</summary>
            public Rect ContourRect { get; set; }
            /// <summary>X 方向网格数</summary>
            public int NX { get; set; }
            /// <summary>Y 方向网格数</summary>
            public int NY { get; set; }
            /// <summary>X 间距</summary>
            public double CellWidth { get; set; }
            /// <summary>Y 间距</summary>
            public double CellHeight { get; set; }
            /// <summary>桩截面类型</summary>
            public PileSectionType SectionType { get; set; }
            /// <summary>桩直径/边长</summary>
            public double DiameterOrEdge { get; set; }
            /// <summary>输入置换率</summary>
            public double InputDisplacementRate { get; set; }
            /// <summary>实际置换率</summary>
            public double ActualDisplacementRate { get; set; }
            /// <summary>布置类型</summary>
            public PileArrangementType ArrangementType { get; set; }
            /// <summary>比例</summary>
            public double Scale { get; set; }
            /// <summary>错误信息（null 表示成功）</summary>
            public string Error { get; set; }
        }

        #endregion

        #region 公开 API

        /// <summary>
        /// 计算桩布置方案（完整流程）
        /// </summary>
        /// <param name="contourX0">轮廓左下角 X</param>
        /// <param name="contourY0">轮廓左下角 Y</param>
        /// <param name="contourWidth">轮廓宽度</param>
        /// <param name="contourHeight">轮廓高度</param>
        /// <param name="sectionType">桩截面类型</param>
        /// <param name="diameterOrEdge">桩直径/边长</param>
        /// <param name="displacementRate">置换率</param>
        /// <param name="arrangeRate">布置比例（取整阈值）</param>
        /// <param name="margin">边距 (上, 下, 左, 右)</param>
        /// <param name="arrangementType">布置类型（矩形/梅花形）</param>
        /// <param name="scale">比例</param>
        /// <param name="manualNX">手动 NX（0 表示自动）</param>
        /// <param name="manualNY">手动 NY（0 表示自动）</param>
        public PileLayoutResult Calculate(
            double contourX0, double contourY0, double contourWidth, double contourHeight,
            PileSectionType sectionType, double diameterOrEdge,
            double displacementRate, double arrangeRate,
            (double up, double down, double left, double right) margin,
            PileArrangementType arrangementType, double scale,
            int manualNX = 0, int manualNY = 0)
        {
            var result = new PileLayoutResult
            {
                ContourRect = new Rect(contourX0, contourY0, contourWidth, contourHeight),
                SectionType = sectionType,
                DiameterOrEdge = diameterOrEdge,
                InputDisplacementRate = displacementRate,
                ArrangementType = arrangementType,
                Scale = scale
            };

            // 1. 验证
            if (contourWidth <= 0 || contourHeight <= 0)
            {
                result.Error = "矩形方向异常，宽/高 <= 0";
                return result;
            }

            // 2. 内缩矩形
            var inset = CreateInsetRect(contourX0, contourY0, contourWidth, contourHeight, margin);
            if (inset == null)
            {
                result.Error = $"margin 过大（宽={contourWidth}, 高={contourHeight}，左右={margin.left + margin.right}, 上下={margin.up + margin.down}）";
                return result;
            }
            result.InsetRect = inset.Value;

            // 3. 桩面积
            double pileArea = CalculatePileArea(sectionType, diameterOrEdge);
            double contourArea = contourWidth * contourHeight;

            // 4. 网格尺寸
            int nX, nY;
            if (manualNX > 0 && manualNY > 0)
            {
                nX = manualNX;
                nY = manualNY;
            }
            else if (arrangementType == PileArrangementType.Rectangle)
            {
                CalculateGridSizeRect(contourArea, contourWidth, contourHeight,
                    pileArea, displacementRate, arrangeRate, out nX, out nY);
            }
            else
            {
                CalculateGridSizeCircular(contourArea, contourWidth, contourHeight,
                    pileArea, displacementRate, arrangeRate, out nX, out nY);
            }

            if (nX <= 0 || nY <= 0)
            {
                result.Error = $"网格计算异常: nX={nX}, nY={nY}";
                return result;
            }

            // 5. 生成桩点和小矩形
            double cellWidth, cellHeight;
            List<Point2D> pilePoints;
            
            if (arrangementType == PileArrangementType.Circular)
            {
                // 梅花形：网格点 + 正方形中间桩
                pilePoints = GenerateCircularGridPoints(inset.Value, nX, nY, out cellWidth, out cellHeight, out var smallRects);
                result.SmallRects = smallRects;
            }
            else
            {
                // 矩形：仅网格点
                pilePoints = GenerateGridPoints(inset.Value, nX, nY, out cellWidth, out cellHeight, out var smallRects);
                result.SmallRects = smallRects;
            }

            result.NX = nX;
            result.NY = nY;
            result.CellWidth = cellWidth;
            result.CellHeight = cellHeight;
            result.PilePoints = pilePoints;
            result.ActualDisplacementRate = pilePoints.Count > 0
                ? (pilePoints.Count * pileArea) / contourArea
                : 0;

            return result;
        }

        #endregion

        #region 内缩矩形

        /// <summary>
        /// 创建内缩矩形
        /// </summary>
        public Rect? CreateInsetRect(double x0, double y0, double width, double height,
            (double up, double down, double left, double right) margin)
        {
            if (margin.left + margin.right >= width || margin.up + margin.down >= height)
                return null;

            return new Rect(
                x0 + margin.left,
                y0 + margin.down,
                width - margin.left - margin.right,
                height - margin.up - margin.down
            );
        }

        #endregion

        #region 网格尺寸计算

        /// <summary>
        /// 矩形网格尺寸计算（移植自 GeometryUtils.CalculateGridSizeRect）
        /// </summary>
        public void CalculateGridSizeRect(double contourArea, double lX, double lY,
            double pileArea, double displacementRate, double arrangeRate,
            out int nX, out int nY)
        {
            double totalPiles = Math.Ceiling(contourArea * displacementRate / pileArea);

            if (lX <= 0 || lY <= 0) { nX = 0; nY = 0; return; }

            if (lX > lY)
            {
                var d = Math.Pow(totalPiles * lY / lX, 0.5);
                nY = (int)((d % 1 <= arrangeRate) ? Math.Floor(d) : Math.Ceiling(d));
                nY = Math.Max(1, nY);
                nX = Math.Max(1, (int)Math.Ceiling(totalPiles / nY));
            }
            else
            {
                var d = Math.Pow(totalPiles * lX / lY, 0.5);
                nX = (int)((d % 1 <= arrangeRate) ? Math.Floor(d) : Math.Ceiling(d));
                nX = Math.Max(1, nX);
                nY = Math.Max(1, (int)Math.Ceiling(totalPiles / nX));
            }
        }

        /// <summary>
        /// 梅花形网格尺寸计算（移植自 GeometryUtils.CalculateGridSizeCircular）
        /// </summary>
        public void CalculateGridSizeCircular(double contourArea, double lX, double lY,
            double pileArea, double displacementRate, double arrangeRate,
            out int nX, out int nY)
        {
            if (contourArea <= 0 || lX <= 0 || lY <= 0) { nX = 0; nY = 0; return; }

            double smallRectArea = pileArea / displacementRate;
            double ss = Math.Sqrt(smallRectArea);
            double totalPiles = Math.Ceiling(contourArea / smallRectArea);

            int bestNX = 0, bestNY = 0;
            int minPiles = int.MaxValue;
            int maxX = (int)(lX / ss);
            int maxY = (int)(lY / ss);

            if (lX > lY)
            {
                int baseNY = (int)((lY / ss) % 1 <= arrangeRate ? Math.Floor(lY / ss) : Math.Ceiling(lY / ss));
                baseNY = Math.Max(1, baseNY);

                for (int ny = Math.Max(1, baseNY - 1); ny <= maxY; ny++)
                {
                    double numerator = totalPiles - (ny + 1);
                    double denominator = 2 * ny + 1;
                    int nxBase = (int)Math.Max(1, Math.Ceiling(numerator / denominator));

                    for (int nx = nxBase; nx <= maxX; nx++)
                    {
                        int actualPiles = (nx + 1) * (ny + 1) + nx * ny;
                        if (actualPiles > totalPiles && actualPiles < minPiles)
                        {
                            minPiles = actualPiles;
                            bestNX = nx;
                            bestNY = ny;
                        }
                        if (actualPiles >= minPiles && nx > nxBase) break;
                    }
                }
            }
            else
            {
                int baseNX = (int)((lX / ss) % 1 <= arrangeRate ? Math.Floor(lX / ss) : Math.Ceiling(lX / ss));
                baseNX = Math.Max(1, baseNX);

                for (int nx = Math.Max(1, baseNX - 1); nx <= maxX; nx++)
                {
                    double numerator = totalPiles - nx - 1;
                    double denominator = 2 * nx + 1;
                    int nyBase = (int)Math.Max(1, Math.Ceiling(numerator / denominator));

                    for (int ny = nyBase; ny <= maxY; ny++)
                    {
                        int actualPiles = (nx + 1) * (ny + 1) + nx * ny;
                        if (actualPiles > totalPiles && actualPiles < minPiles)
                        {
                            minPiles = actualPiles;
                            bestNX = nx;
                            bestNY = ny;
                        }
                        if (actualPiles >= minPiles && ny > nyBase) break;
                    }
                }
            }

            nX = bestNX + 1;
            nY = bestNY + 1;
        }

        #endregion

        #region 桩点生成

        /// <summary>
        /// 生成矩形网格桩点（移植自 GeometryUtils.GenerateRectPolygons 的桩点逻辑）
        /// </summary>
        public List<Point2D> GenerateGridPoints(Rect insetRect, int nX, int nY,
            out double cellWidth, out double cellHeight, out List<Rect> smallRects)
        {
            var points = new List<Point2D>();
            smallRects = new List<Rect>();
            double x0 = insetRect.X0;
            double y0 = insetRect.Y0;
            double lX = insetRect.Width;
            double lY = insetRect.Height;

            cellWidth = lX / Math.Max(nX, 1);
            cellHeight = lY / Math.Max(nY, 1);

            // 特殊情况: 单桩
            if (nX == 1 && nY == 1)
            {
                points.Add(new Point2D(x0 + lX / 2, y0 + lY / 2));
                smallRects.Add(insetRect);
                return points;
            }

            // 单列
            if (nX == 1 && nY >= 1)
            {
                int rows = nY - 1;
                if (rows < 1) rows = 1;
                cellHeight = lY / rows;
                double cx = x0 + lX / 2;
                for (int i = 0; i <= rows; i++)
                    points.Add(new Point2D(cx, y0 + i * cellHeight));
                for (int i = 0; i < rows; i++)
                    smallRects.Add(new Rect(x0, y0 + i * cellHeight, lX, cellHeight));
                return points;
            }

            // 单行
            if (nY == 1 && nX >= 1)
            {
                int cols = nX - 1;
                if (cols < 1) cols = 1;
                cellWidth = lX / cols;
                double cy = y0 + lY / 2;
                for (int j = 0; j <= cols; j++)
                    points.Add(new Point2D(x0 + j * cellWidth, cy));
                for (int j = 0; j < cols; j++)
                    smallRects.Add(new Rect(x0 + j * cellWidth, y0, cellWidth, lY));
                return points;
            }

            // 一般情况: nX >= 2, nY >= 2
            int adjNX = nX - 1;
            int adjNY = nY - 1;
            cellWidth = lX / Math.Max(adjNX, 1);
            cellHeight = lY / Math.Max(adjNY, 1);

            // 生成桩点 (网格交点)
            for (int i = 0; i <= adjNY; i++)
                for (int j = 0; j <= adjNX; j++)
                    points.Add(new Point2D(x0 + j * cellWidth, y0 + i * cellHeight));

            // 生成小矩形
            for (int i = 0; i < adjNY; i++)
                for (int j = 0; j < adjNX; j++)
                    smallRects.Add(new Rect(x0 + j * cellWidth, y0 + i * cellHeight, cellWidth, cellHeight));

            return points;
        }

        /// <summary>
        /// 生成梅花形网格桩点（移植自 CircularStandardArea.ArrangePiles）
        /// 包含：网格交点 + 每个小正方形的中心点（形心）
        /// </summary>
        public List<Point2D> GenerateCircularGridPoints(Rect insetRect, int nX, int nY,
            out double cellWidth, out double cellHeight, out List<Rect> smallRects)
        {
            var gridPoints = new List<Point2D>();
            var centroids = new List<Point2D>();
            smallRects = new List<Rect>();
            double x0 = insetRect.X0;
            double y0 = insetRect.Y0;
            double lX = insetRect.Width;
            double lY = insetRect.Height;

            cellWidth = lX / Math.Max(nX, 1);
            cellHeight = lY / Math.Max(nY, 1);

            // 特殊情况: 单桩
            if (nX == 1 && nY == 1)
            {
                gridPoints.Add(new Point2D(x0 + lX / 2, y0 + lY / 2));
                smallRects.Add(insetRect);
                return gridPoints;
            }

            // 单列
            if (nX == 1 && nY >= 1)
            {
                int rows = nY - 1;
                if (rows < 1) rows = 1;
                cellHeight = lY / rows;
                double cx = x0 + lX / 2;
                for (int i = 0; i <= rows; i++)
                    gridPoints.Add(new Point2D(cx, y0 + i * cellHeight));
                for (int i = 0; i < rows; i++)
                    smallRects.Add(new Rect(x0, y0 + i * cellHeight, lX, cellHeight));
                return gridPoints;
            }

            // 单行
            if (nY == 1 && nX >= 1)
            {
                int cols = nX - 1;
                if (cols < 1) cols = 1;
                cellWidth = lX / cols;
                double cy = y0 + lY / 2;
                for (int j = 0; j <= cols; j++)
                    gridPoints.Add(new Point2D(x0 + j * cellWidth, cy));
                for (int j = 0; j < cols; j++)
                    smallRects.Add(new Rect(x0 + j * cellWidth, y0, cellWidth, lY));
                return gridPoints;
            }

            // 一般情况: nX >= 2, nY >= 2
            int adjNX = nX - 1;
            int adjNY = nY - 1;
            cellWidth = lX / Math.Max(adjNX, 1);
            cellHeight = lY / Math.Max(adjNY, 1);

            // Step 1: 生成网格交点（(adjNX+1) * (adjNY+1) 个点）
            for (int i = 0; i <= adjNY; i++)
                for (int j = 0; j <= adjNX; j++)
                    gridPoints.Add(new Point2D(x0 + j * cellWidth, y0 + i * cellHeight));

            // Step 2: 生成小矩形和形心（中间桩）
            for (int i = 0; i < adjNY; i++)
            {
                for (int j = 0; j < adjNX; j++)
                {
                    // 小矩形四个顶点索引
                    int indexBL = i * (adjNX + 1) + j;           // 左下
                    int indexBR = indexBL + 1;                    // 右下
                    int indexTL = indexBL + (adjNX + 1);          // 左上
                    int indexTR = indexBL + (adjNX + 1) + 1;      // 右上

                    // 计算形心（正方形中心 = 四个顶点的平均值）
                    double centroidX = (gridPoints[indexBL].X + gridPoints[indexBR].X + 
                                       gridPoints[indexTL].X + gridPoints[indexTR].X) / 4.0;
                    double centroidY = (gridPoints[indexBL].Y + gridPoints[indexBR].Y + 
                                       gridPoints[indexTL].Y + gridPoints[indexTR].Y) / 4.0;
                    centroids.Add(new Point2D(centroidX, centroidY));

                    // 小矩形
                    smallRects.Add(new Rect(x0 + j * cellWidth, y0 + i * cellHeight, cellWidth, cellHeight));
                }
            }

            // Step 3: 合并网格点和中间桩
            var allPoints = new List<Point2D>(gridPoints.Count + centroids.Count);
            allPoints.AddRange(gridPoints);
            allPoints.AddRange(centroids);

            return allPoints;
        }

        #endregion

        #region 工具方法

        private double CalculatePileArea(PileSectionType section, double diameterOrEdge)
        {
            if (section == PileSectionType.Circle)
                return Math.PI * Math.Pow(diameterOrEdge / 2, 2);
            return diameterOrEdge * diameterOrEdge;
        }

        #endregion
    }
}
