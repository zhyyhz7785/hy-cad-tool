using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;

namespace HyCADTool.HelpClass
{
    public static class GeometryUtils
    {
        public static Polygon CreateInsetRect(Polygon polygon, (double up, double down, double left, double right) margin)
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;

            if (polygon == null || polygon.ExteriorRing == null || polygon.ExteriorRing.Coordinates.Length != 5)
            {
                ed.WriteMessage("\n警告：Polyline 不闭合");
                return null;
            }

            Coordinate[] coords = polygon.ExteriorRing.Coordinates;
            Coordinate p0 = coords[0];
            Coordinate p1 = coords[1];
            Coordinate p2 = coords[2];
            Coordinate p3 = coords[3];

            double originalWidth = p1.X - p0.X;
            double originalHeight = p3.Y - p0.Y;

            if (margin.left + margin.right >= originalWidth || margin.up + margin.down >= originalHeight)
            {
                ed.WriteMessage("\n错误：margin 过大，无法调整矩形");
                ed.WriteMessage($"\n当前宽度: {originalWidth}, 当前高度: {originalHeight}");
                ed.WriteMessage($"\n当前 margin: 左 {margin.left}, 右 {margin.right}, 上 {margin.up}, 下 {margin.down}");
                ed.WriteMessage("\n请调整 margin 使其小于边长");
                return null;
            }

            Coordinate newP0 = new Coordinate(p0.X + margin.left, p0.Y + margin.down);
            Coordinate newP1 = new Coordinate(p1.X - margin.right, p1.Y + margin.down);
            Coordinate newP2 = new Coordinate(p2.X - margin.right, p2.Y - margin.up);
            Coordinate newP3 = new Coordinate(p3.X + margin.left, p3.Y - margin.up);

            Coordinate[] newCoords = { newP0, newP1, newP2, newP3, newP0 };
            var geometryFactory = new GeometryFactory();
            return geometryFactory.CreatePolygon(newCoords);
        }

        public static void CalculateGridSizeRect(StandardAreaBase standardArea, Pile pile, out int nX, out int nY)
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            var contour = standardArea.Contour;
            var insetRect = standardArea.InsetRect;
            var inputDisplacementRate = standardArea.InputDisplacementRate;
            var pileArea = pile.PileArea;
            var area = contour.Area;
            double calculatedTotalPiles =Math.Ceiling( contour.Area * inputDisplacementRate / pileArea);            

            Coordinate[] vertices = contour.ExteriorRing.Coordinates;
            Coordinate p0 = vertices[0], p1 = vertices[1], p2 = vertices[2], p3 = vertices[3];

            double lX = p1.X - p0.X;
            double lY = p3.Y - p0.Y;

            if (lX <= 0 || lY <= 0)
            {
                ed.WriteMessage("\n警告：矩形方向异常，请重新选择");
                nX = 0;
                nY = 0;
                return;
            }

            if (lX > lY)
            {
                var d = Math.Pow( calculatedTotalPiles * lY / lX,0.5);                
                nY = (int)((d) % 1 <= HyCADTool.PileConfig.Instance.PileArrangeRate ? Math.Floor(d) : Math.Ceiling(d));
                nY = Math.Max(1, nY);
                nX = Math.Max(1, (int)Math.Ceiling(calculatedTotalPiles / nY));                
                nY = nY - 1;
                nX = nX - 1;
            }
            else
            {
                var d = Math.Pow(calculatedTotalPiles * lX / lY, 0.5);
                nX = (int)((d) % 1 <= HyCADTool.PileConfig.Instance.PileArrangeRate ? Math.Floor(d) : Math.Ceiling(d));
                nX= Math.Max(1, nX);
                nY = Math.Max(1, (int)Math.Ceiling(calculatedTotalPiles / nX));
                nY = nY - 1;
                nX = nX - 1;
            }
        }

        public static void CalculateGridSizeCircular(StandardAreaBase standardArea, out int nX, out int nY)
        {
            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            var contour = standardArea.Contour;
            var insetRect = standardArea.InsetRect;

            var area = contour.Area;
            double pileArea = 0;
            if (insetRect == null || area <= 0 || insetRect.Length < 4)
            {
                ed.WriteMessage("\n错误：Polygon 为空");
                nX = 0;
                nY = 0;
                return;
            }

            if (HyCADTool.PileConfig.Instance.Section == SectionType.Circle)
            {
                pileArea = Math.Pow(HyCADTool.PileConfig.Instance.DiameterOrEdge / 2, 2) * Math.PI;
            }
            else
            {
                pileArea = Math.Pow(HyCADTool.PileConfig.Instance.DiameterOrEdge, 2);
            }

            double smallRectArea = pileArea / HyCADTool.PileConfig.Instance.InputDisplacementRate;
            double ss = Math.Sqrt(smallRectArea);
            double totalPiles = Math.Ceiling(area / smallRectArea);

            Coordinate[] vertices = contour.ExteriorRing.Coordinates;
            Coordinate p0 = vertices[0], p1 = vertices[1], p2 = vertices[2], p3 = vertices[3];

            double lX = p1.X - p0.X;
            double lY = p3.Y - p0.Y;

            if (lX <= 0 || lY <= 0)
            {
                ed.WriteMessage("\n警告：矩形方向异常，请重新选择");
                nX = 0;
                nY = 0;
                return;
            }

            int bestNX = 0, bestNY = 0;
            int minPiles = int.MaxValue;
            int maxX = (int)(lX / ss);
            int maxY = (int)(lY / ss);

            if (lX > lY)
            {
                int baseNY = (int)((lY / ss) % 1 <= HyCADTool.PileConfig.Instance.PileArrangeRate ? Math.Floor(lY / ss) : Math.Ceiling(lY / ss));
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
                        if (actualPiles >= minPiles && nx > nxBase)
                            break;
                    }
                }
            }
            else
            {
                int baseNX = (int)((lX / ss) % 1 <= HyCADTool.PileConfig.Instance.PileArrangeRate ? Math.Floor(lX / ss) : Math.Ceiling(lX / ss));
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
                        if (actualPiles >= minPiles && ny > nyBase)
                            break;
                    }
                }
            }

            nX = bestNX;
            nY = bestNY;

            int finalPiles = (nX + 1) * (nY + 1) + nX * nY;
            ed.WriteMessage($"\n第二种情况：nX = {nX}, nY = {nY}, 桩总数 = {finalPiles}, 目标桩数 = {totalPiles}");
        }

        public static List<Polygon> GenerateRectPolygons(
    Polygon polygon, int nX, int nY, out List<Point> points, out List<Point> centroids, out double cellWidth, out double cellHeight)
        {
            List<Polygon> rectangles = new List<Polygon>();
            points = new List<Point>();
            centroids = new List<Point>();

            if (polygon == null || polygon.Area <= 0 || nX <= 0 || nY <= 0)
            {
                throw new ArgumentException("输入的 Polygon 无效或 nX/nY 计算错误");
            }

            Coordinate[] vertices = polygon.ExteriorRing.Coordinates;
            Coordinate p0 = vertices[0];

            double lX = vertices[1].X - p0.X;
            double lY = vertices[3].Y - p0.Y;

            cellWidth = lX / nX;
            cellHeight = lY / nY;

            GeometryFactory gf = new GeometryFactory();

            // Step 1: 生成所有唯一的网格点
            for (int i = 0; i <= nY; i++)
            {
                for (int j = 0; j <= nX; j++)
                {
                    double x = p0.X + j * cellWidth;
                    double y = p0.Y + i * cellHeight;
                    points.Add(new Point(x, y, 0));
                }
            }

            // Step 2: 生成小矩形和形心
            for (int i = 0; i < nY; i++)
            {
                for (int j = 0; j < nX; j++)
                {
                    // 获取当前小矩形的四个顶点索引
                    int indexBL = i * (nX + 1) + j;        // 左下角
                    int indexBR = indexBL + 1;             // 右下角
                    int indexTR = indexBL + (nX + 1) + 1;  // 右上角
                    int indexTL = indexBL + (nX + 1);      // 左上角

                    Coordinate bottomLeft = new Coordinate(points[indexBL].X, points[indexBL].Y);
                    Coordinate bottomRight = new Coordinate(points[indexBR].X, points[indexBR].Y);
                    Coordinate topRight = new Coordinate(points[indexTR].X, points[indexTR].Y);
                    Coordinate topLeft = new Coordinate(points[indexTL].X, points[indexTL].Y);

                    Coordinate[] rectCoords = new Coordinate[]
                    {
                bottomLeft,
                bottomRight,
                topRight,
                topLeft,
                bottomLeft
                    };

                    // 计算形心
                    double centroidX = (bottomLeft.X + bottomRight.X + topRight.X + topLeft.X) / 4;
                    double centroidY = (bottomLeft.Y + bottomRight.Y + topRight.Y + topLeft.Y) / 4;
                    centroids.Add(new Point(centroidX, centroidY, 0));

                    rectangles.Add(gf.CreatePolygon(rectCoords));
                }
            }

            return rectangles;
        }
    }
}