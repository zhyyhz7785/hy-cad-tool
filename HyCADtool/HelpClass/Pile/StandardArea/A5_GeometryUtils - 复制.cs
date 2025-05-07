//using HyCADTool.Interfaces;
//using NetTopologySuite.Geometries;
//using System;
//using System.Collections.Generic;

//namespace HyCADTool.HelpClass
//{
//    /// <summary>
//    /// 提供几何计算工具方法，用于处理多边形、网格生成和桩布置计算。
//    /// 依赖 NetTopologySuite 库进行几何操作，结合 AutoCAD 的 ICadService 接口输出提示信息。
//    /// </summary>
//    public static class GeometryUtils
//    {
//        /// <summary>
//        /// 创建内缩矩形，基于给定的多边形和四个方向的边距。
//        /// 内缩矩形通过在原始矩形的每个边界向内偏移指定边距生成。
//        /// </summary>
//        /// <param name="polygon">输入的多边形，必须是一个闭合的四边形（矩形），包含5个坐标点（首尾重复）。</param>
//        /// <param name="margin">四个方向的边距，格式为 (上, 下, 左, 右)，单位与多边形坐标一致。</param>
//        /// <param name="cadService">AutoCAD 服务接口，用于输出警告或错误信息到命令行。</param>
//        /// <returns>内缩后的新多边形（矩形），如果输入无效或边距过大则返回 null。</returns>
//        public static Polygon CreateInsetRect(Polygon polygon, (double up, double down, double left, double right) margin, ICadService cadService)
//        {
//            // 验证输入多边形是否有效：不为空、具有闭合外环、且为矩形（5个坐标点，首尾相同）
//            if (polygon == null || polygon.ExteriorRing == null || polygon.ExteriorRing.Coordinates.Length != 5)
//            {
//                cadService.WriteMessage("\n警告：Polyline 不闭合");
//                return null;
//            }

//            // 获取多边形外环的坐标点
//            Coordinate[] coords = polygon.ExteriorRing.Coordinates;
//            Coordinate p0 = coords[0]; // 左下角
//            Coordinate p1 = coords[1]; // 右下角
//            Coordinate p2 = coords[2]; // 右上角
//            Coordinate p3 = coords[3]; // 左上角

//            // 计算原始矩形的宽度（X方向）和高度（Y方向）
//            double originalWidth = p1.X - p0.X;
//            double originalHeight = p3.Y - p0.Y;

//            // 检查边距是否过大（左右边距之和不能超过宽度，上下边距之和不能超过高度）
//            if (margin.left + margin.right >= originalWidth || margin.up + margin.down >= originalHeight)
//            {
//                cadService.WriteMessage("\n错误：margin 过大，无法调整矩形");
//                cadService.WriteMessage($"\n当前宽度: {originalWidth}, 当前高度: {originalHeight}");
//                cadService.WriteMessage($"\n当前 margin: 左 {margin.left}, 右 {margin.right}, 上 {margin.up}, 下 {margin.down}");
//                cadService.WriteMessage("\n请调整 margin 使其小于边长");
//                return null;
//            }

//            // 计算内缩后矩形的四个顶点坐标
//            Coordinate newP0 = new Coordinate(p0.X + margin.left, p0.Y + margin.down); // 新左下角
//            Coordinate newP1 = new Coordinate(p1.X - margin.right, p1.Y + margin.down); // 新右下角
//            Coordinate newP2 = new Coordinate(p2.X - margin.right, p2.Y - margin.up); // 新右上角
//            Coordinate newP3 = new Coordinate(p3.X + margin.left, p3.Y - margin.up); // 新左上角

//            // 创建新坐标数组，闭合多边形（首尾坐标相同）
//            Coordinate[] newCoords = { newP0, newP1, newP2, newP3, newP0 };

//            // 使用 GeometryFactory 创建新的内缩矩形多边形
//            var geometryFactory = new GeometryFactory();
//            return geometryFactory.CreatePolygon(newCoords);
//        }

//        /// <summary>
//        /// 计算矩形网格布置的尺寸，用于桩的矩形排列。
//        /// 根据多边形面积、桩面积和布置率，确定网格的行数和列数。
//        /// </summary>
//        /// <param name="standardArea">标准区域对象，包含多边形轮廓和输入位移率。</param>
//        /// <param name="pile">桩对象，包含桩的面积信息。</param>
//        /// <param name="pileArrangeRate">桩布置率阈值，用于决定是否向上取整网格尺寸（0到1之间）。</param>
//        /// <param name="cadService">AutoCAD 服务接口，用于输出警告信息。</param>
//        /// <param name="nX">输出参数：X方向（水平）的网格单元数（列数减1）。</param>
//        /// <param name="nY">输出参数：Y方向（垂直）的网格单元数（行数减1）。</param>
//        public static void CalculateGridSizeRect(StandardAreaBase standardArea, Pile pile, double pileArrangeRate, ICadService cadService, out int nX, out int nY)
//        {
//            // 获取多边形轮廓和输入位移率
//            var contour = standardArea.Contour;
//            var inputDisplacementRate = standardArea.InputDisplacementRate;
//            var pileArea = pile.PileArea;

//            // 计算理论需要的总桩数：多边形面积 * 位移率 / 桩面积，向上取整
//            double calculatedTotalPiles = Math.Ceiling(contour.Area * inputDisplacementRate / pileArea);

//            // 获取多边形外环顶点
//            Coordinate[] vertices = contour.ExteriorRing.Coordinates;
//            Coordinate p0 = vertices[0]; // 左下角
//            Coordinate p1 = vertices[1]; // 右下角
//            Coordinate p3 = vertices[3]; // 左上角

//            // 计算矩形的宽度（X方向）和高度（Y方向）
//            double lX = p1.X - p0.X;
//            double lY = p3.Y - p0.Y;

//            // 检查矩形尺寸是否有效
//            if (lX <= 0 || lY <= 0)
//            {
//                cadService.WriteMessage("\n警告：矩形方向异常，请重新选择");
//                nX = 0;
//                nY = 0;
//                return;
//            }

//            // 根据矩形的宽高比例，确定网格的行数和列数
//            if (lX > lY)
//            {
//                // 宽大于高时，优先按 Y 方向优化
//                var d = Math.Pow(calculatedTotalPiles * lY / lX, 0.5); // 计算 Y 方向的初步网格数
//                nY = (int)((d % 1 <= pileArrangeRate) ? Math.Floor(d) : Math.Ceiling(d)); // 根据布置率决定取整
//                nY = Math.Max(1, nY); // 确保至少为1
//                nX = Math.Max(1, (int)Math.Ceiling(calculatedTotalPiles / nY)); // 根据总桩数计算 X 方向网格数
                
//            }
//            else
//            {
//                // 高大于宽时，优先按 X 方向优化
//                var d = Math.Pow(calculatedTotalPiles * lX / lY, 0.5); // 计算 X 方向的初步网格数
//                nX = (int)((d % 1 <= pileArrangeRate) ? Math.Floor(d) : Math.Ceiling(d)); // 根据布置率决定取整
//                nX = Math.Max(1, nX); // 确保至少为1
//                nY = Math.Max(1, (int)Math.Ceiling(calculatedTotalPiles / nX)); // 根据总桩数计算 Y 方向网格数
                
//            }
//        }

//        /// <summary>
//        /// 计算梅花形（交错）网格布置的尺寸，用于桩的梅花形排列。
//        /// 根据多边形面积、桩面积和布置率，确定网格的行数和列数，优化桩总数。
//        /// </summary>
//        /// <param name="standardArea">标准区域对象，包含多边形轮廓和输入位移率。</param>
//        /// <param name="pile">桩对象，包含桩的面积信息。</param>
//        /// <param name="pileArrangeRate">桩布置率阈值，用于决定是否向上取整网格尺寸（0到1之间）。</param>
//        /// <param name="cadService">AutoCAD 服务接口，用于输出警告和调试信息。</param>
//        /// <param name="nX">输出参数：X方向（水平）的网格单元数。</param>
//        /// <param name="nY">输出参数：Y方向（垂直）的网格单元数。</param>
//        public static void CalculateGridSizeCircular(StandardAreaBase standardArea, Pile pile, 
//            double pileArrangeRate, ICadService cadService, out int nX, out int nY)
//        {
//            // 获取多边形轮廓和输入位移率
//            var contour = standardArea.Contour;
//            var inputDisplacementRate = standardArea.InputDisplacementRate;

//            // 验证多边形是否有效
//            if (contour == null || contour.Area <= 0)
//            {
//                cadService.WriteMessage("\n错误：Polygon 为空");
//                nX = 0;
//                nY = 0;
//                return;
//            }

//            double pileArea = pile.PileArea; // 获取桩面积
//            double smallRectArea = pileArea / inputDisplacementRate; // 计算每个桩占用的小矩形面积
//            double ss = Math.Sqrt(smallRectArea); // 小矩形的边长
//            double totalPiles = Math.Ceiling(contour.Area / smallRectArea); // 理论需要的总桩数

//            // 获取多边形外环顶点
//            Coordinate[] vertices = contour.ExteriorRing.Coordinates;
//            Coordinate p0 = vertices[0]; // 左下角
//            Coordinate p1 = vertices[1]; // 右下角
//            Coordinate p3 = vertices[3]; // 左上角

//            // 计算矩形的宽度（X方向）和高度（Y方向）
//            double lX = p1.X - p0.X;
//            double lY = p3.Y - p0.Y;

//            // 检查矩形尺寸是否有效
//            if (lX <= 0 || lY <= 0)
//            {
//                cadService.WriteMessage("\n警告：矩形方向异常，请重新选择");
//                nX = 0;
//                nY = 0;
//                return;
//            }

//            int bestNX = 0, bestNY = 0;
//            int minPiles = int.MaxValue;
//            int maxX = (int)(lX / ss); // X方向最大网格数
//            int maxY = (int)(lY / ss); // Y方向最大网格数

//            // 根据宽高比例，优化梅花形网格
//            if (lX > lY)
//            {
//                // 宽大于高时，优先按 Y 方向优化
//                int baseNY = (int)((lY / ss) % 1 <= pileArrangeRate ? Math.Floor(lY / ss) : Math.Ceiling(lY / ss));
//                baseNY = Math.Max(1, baseNY);
//                for (int ny = Math.Max(1, baseNY - 1); ny <= maxY; ny++)
//                {
//                    // 计算 X 方向的最小网格数
//                    double numerator = totalPiles - (ny + 1);
//                    double denominator = 2 * ny + 1;
//                    int nxBase = (int)Math.Max(1, Math.Ceiling(numerator / denominator));
//                    for (int nx = nxBase; nx <= maxX; nx++)
//                    {
//                        // 计算梅花形布置的总桩数：(nx+1)*(ny+1) 为矩形网格点，nx*ny 为交错点
//                        int actualPiles = (nx + 1) * (ny + 1) + nx * ny;
//                        // 选择大于目标桩数且最接近的网格配置
//                        if (actualPiles > totalPiles && actualPiles < minPiles)
//                        {
//                            minPiles = actualPiles;
//                            bestNX = nx;
//                            bestNY = ny;
//                        }
//                        // 提前退出循环，避免不必要的计算
//                        if (actualPiles >= minPiles && nx > nxBase)
//                            break;
//                    }
//                }
//            }
//            else
//            {
//                // 高大于宽时，优先按 X 方向优化
//                int baseNX = (int)((lX / ss) % 1 <= pileArrangeRate ? Math.Floor(lX / ss) : Math.Ceiling(lX / ss));
//                baseNX = Math.Max(1, baseNX);
//                for (int nx = Math.Max(1, baseNX - 1); nx <= maxX; nx++)
//                {
//                    // 计算 Y 方向的最小网格数
//                    double numerator = totalPiles - nx - 1;
//                    double denominator = 2 * nx + 1;
//                    int nyBase = (int)Math.Max(1, Math.Ceiling(numerator / denominator));
//                    for (int ny = nyBase; ny <= maxY; ny++)
//                    {
//                        int actualPiles = (nx + 1) * (ny + 1) + nx * ny;
//                        if (actualPiles > totalPiles && actualPiles < minPiles)
//                        {
//                            minPiles = actualPiles;
//                            bestNX = nx;
//                            bestNY = ny;
//                        }
//                        if (actualPiles >= minPiles && ny > nyBase)
//                            break;
//                    }
//                }
//            }

//            // 输出最终网格尺寸和桩总数
//            nX = bestNX;
//            nY = bestNY;
//            int finalPiles = (nX + 1) * (nY + 1) + nX * nY;
//            cadService.WriteMessage($"\n第二种情况：nX = {nX}, nY = {nY}, 桩总数 = {finalPiles}, 目标桩数 = {totalPiles}");
//        }

//        /// <summary>
//        /// 生成矩形网格的多边形，用于划分输入多边形为小矩形网格。
//        /// 同时返回网格点、形心和单元尺寸。
//        /// </summary>
//        /// <param name="polygon">输入的多边形，必须是一个有效的矩形（四边形）。</param>
//        /// <param name="nX">X方向的网格单元数（列数）。</param>
//        /// <param name="nY">Y方向的网格单元数（行数）。</param>
//        /// <param name="points">输出参数：所有网格点的列表（包括边界点或桩点）。</param>
//        /// <param name="centroids">输出参数：每个网格单元形心的列表（或桩点）。</param>
//        /// <param name="cellWidth">输出参数：每个网格单元的宽度（X方向）。</param>
//        /// <param name="cellHeight">输出参数：每个网格单元的高度（Y方向）。</param>
//        /// <returns>包含所有小矩形多边形的列表，每个多边形表示一个网格单元。</returns>
//        public static List<Polygon> GenerateRectPolygons(Polygon polygon, int nX, int nY, out List<Point> points, out List<Point> centroids, out double cellWidth, out double cellHeight)
//        {
//            // 初始化输出参数
//            List<Polygon> rectangles = new List<Polygon>();
//            points = new List<Point>();
//            centroids = new List<Point>();

//            // 验证输入参数
//            if (polygon == null || polygon.Area <= 0 || nX <= 0 || nY <= 0)
//            {
//                throw new ArgumentException("输入的 Polygon 无效或 nX/nY 计算错误");
//            }

//            // 获取多边形外环顶点
//            Coordinate[] vertices = polygon.ExteriorRing.Coordinates;
//            Coordinate p0 = vertices[0]; // 左下角
//            double lX = vertices[1].X - p0.X; // 矩形宽度
//            double lY = vertices[3].Y - p0.Y; // 矩形高度

//            GeometryFactory gf = new GeometryFactory();

//            // 计算每个网格单元的宽度和高度（初始值）
//            cellWidth = lX / Math.Max(nX, 1);
//            cellHeight = lY / Math.Max(nY, 1);

//            // 情况 1: nX = 1 且 nY = 1（单个桩，位于多边形中心）
//            if (nX == 1 && nY == 1)
//            {
//                // 计算多边形中心点
//                double centerX = p0.X + lX / 2;
//                double centerY = p0.Y + lY / 2;
//                Point centerPoint = new Point(centerX, centerY, 0);

//                // 添加中心点到 points 和 centroids
//                points.Add(centerPoint);
//                centroids.Add(centerPoint);

//                // 添加整个多边形作为唯一的小矩形
//                rectangles.Add(polygon);

//                return rectangles;
//            }

//            // 情况 2: nX = 1（单列网格）
//            if (nX == 1 && nY >= 1)
//            {
                
//                nY = nY - 1;
//                // 重新计算网格单元的宽度和高度
//                cellWidth = lX / Math.Max(nX, 1);
//                cellHeight = lY / Math.Max(nY, 1);
//                // 生成单列网格点，沿 Y 方向分布
//                for (int i = 0; i <= nY; i++)
//                {
//                    double x = p0.X + lX / 2; // X 坐标固定在宽度中点
//                    double y = p0.Y + i * cellHeight; // Y 坐标每次增加 cellHeight
//                    points.Add(new Point(x, y, 0));
//                }

//                // 生成小矩形多边形和形心
//                for (int i = 0; i < nY; i++)
//                {
//                    // 矩形的四个顶点
//                    Coordinate bottomLeft = new Coordinate(p0.X, p0.Y + i * cellHeight);
//                    Coordinate bottomRight = new Coordinate(p0.X + lX, p0.Y + i * cellHeight);
//                    Coordinate topRight = new Coordinate(p0.X + lX, p0.Y + (i + 1) * cellHeight);
//                    Coordinate topLeft = new Coordinate(p0.X, p0.Y + (i + 1) * cellHeight);

//                    // 创建小矩形的坐标数组（闭合）
//                    Coordinate[] rectCoords = { bottomLeft, bottomRight, topRight, topLeft, bottomLeft };
//                    rectangles.Add(gf.CreatePolygon(rectCoords));

//                    // 形心为当前网格点（桩中心）
//                    centroids.Add(points[i]);
//                }

//                return rectangles;
//            }

//            // 情况 3: nY = 1（单行网格）
//            if (nY == 1 && nX >= 1)
//            {
//                nX = nX - 1;
//                // 重新计算网格单元的宽度和高度
//                cellWidth = lX / Math.Max(nX, 1);
//                cellHeight = lY / Math.Max(nY, 1);
//                // 生成单行网格点，沿 X 方向分布
//                for (int j = 0; j <= nX; j++)
//                {
//                    double x = p0.X + j * cellWidth; // X 坐标每次增加 cellWidth
//                    double y = p0.Y + lY / 2; // Y 坐标固定在高度中点
//                    points.Add(new Point(x, y, 0));
//                }

//                // 生成小矩形多边形和形心
//                for (int j = 0; j < nX; j++)
//                {
//                    // 矩形的四个顶点
//                    Coordinate bottomLeft = new Coordinate(p0.X + j * cellWidth, p0.Y);
//                    Coordinate bottomRight = new Coordinate(p0.X + (j + 1) * cellWidth, p0.Y);
//                    Coordinate topRight = new Coordinate(p0.X + (j + 1) * cellWidth, p0.Y + lY);
//                    Coordinate topLeft = new Coordinate(p0.X + j * cellWidth, p0.Y + lY);

//                    // 创建小矩形的坐标数组（闭合）
//                    Coordinate[] rectCoords = { bottomLeft, bottomRight, topRight, topLeft, bottomLeft };
//                    rectangles.Add(gf.CreatePolygon(rectCoords));

//                    // 形心为当前网格点（桩中心）
//                    centroids.Add(points[j]);
//                }

//                return rectangles;
//            }
//            if (nY >= 1 && nX >= 1)
//            {
//                // 情况 4: nX >= 1 且 nY >= 1（调整 nX 和 nY）
//                // 调整 nX 和 nY
//                nX = nX - 1;
//                nY = nY - 1;
//                // 重新计算网格单元的宽度和高度
//                cellWidth = lX / Math.Max(nX, 1);
//                cellHeight = lY / Math.Max(nY, 1);
//                // Step 1: 生成所有网格点（包括边界点，nX+1 列，nY+1 行）
//                for (int i = 0; i <= nY; i++)
//                {
//                    for (int j = 0; j <= nX; j++)
//                    {
//                        double x = p0.X + j * cellWidth;
//                        double y = p0.Y + i * cellHeight;
//                        points.Add(new Point(x, y, 0));
//                    }
//                }

//                // Step 2: 生成小矩形多边形和形心
//                for (int i = 0; i < nY; i++)
//                {
//                    for (int j = 0; j < nX; j++)
//                    {
//                        // 计算当前网格单元的四个顶点索引
//                        int indexBL = i * (nX + 1) + j;        // 左下角
//                        int indexBR = indexBL + 1;             // 右下角
//                        int indexTR = indexBL + (nX + 1) + 1;  // 右上角
//                        int indexTL = indexBL + (nX + 1);      // 左上角

//                        // 获取四个顶点的坐标
//                        Coordinate bottomLeft = new Coordinate(points[indexBL].X, points[indexBL].Y);
//                        Coordinate bottomRight = new Coordinate(points[indexBR].X, points[indexBR].Y);
//                        Coordinate topRight = new Coordinate(points[indexTR].X, points[indexTR].Y);
//                        Coordinate topLeft = new Coordinate(points[indexTL].X, points[indexTL].Y);

//                        // 创建小矩形的坐标数组（闭合）
//                        Coordinate[] rectCoords = new Coordinate[] { bottomLeft, bottomRight, topRight, topLeft, bottomLeft };

//                        // 计算形心坐标（四个顶点的平均值）
//                        double centroidX = (bottomLeft.X + bottomRight.X + topRight.X + topLeft.X) / 4;
//                        double centroidY = (bottomLeft.Y + bottomRight.Y + topRight.Y + topLeft.Y) / 4;
//                        centroids.Add(new Point(centroidX, centroidY, 0));

//                        // 创建并添加小矩形多边形
//                        rectangles.Add(gf.CreatePolygon(rectCoords));
//                    }
//                }
//            }               
                       

           

//            return rectangles;
//        }

//    }
//}