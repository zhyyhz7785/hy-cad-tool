using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Runtime;
using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using Exception = Autodesk.AutoCAD.Runtime.Exception;
namespace HyCADTool.Tools
{
    public static partial class Tools
    {
        /// <summary>
        /// 命令测试示例：执行桩布局迭代
        /// </summary>
        [CommandMethod("TEST_PILE_LAYOUT")]
        public static void TestPileLayout()
        {
            Document doc = Application.DocumentManager.MdiActiveDocument;
            Editor ed = doc.Editor;
            try
            {
                // 1. 假设我们已经有一个闭合多边形对象(从用户选择或已有对象)
                //    这里为了演示，复用: PromptEntityOptions + ConvertPolylineToNTSPolygon
                Polygon ntsPolygon = PromptAndGetPolygon();
                if (ntsPolygon == null)
                {
                    ed.WriteMessage("\n获取多边形失败，命令结束。");
                    return;
                }
                // 2. 用户输入桩直径D、面积置换率R
                double diameter = 0.4; // 默认1m
                double ratio = 0.05;   // 默认5%
                                       // 也可用 AutoCAD 提示获取
                PromptDoubleOptions pdoDiameter = new PromptDoubleOptions("\n请输入桩直径(米):");
                pdoDiameter.DefaultValue = 1.0;
                PromptDoubleResult pdrDiameter = ed.GetDouble(pdoDiameter);
                if (pdrDiameter.Status != PromptStatus.OK) return;
                diameter = pdrDiameter.Value;
                PromptDoubleOptions pdoRatio = new PromptDoubleOptions("\n请输入目标面积置换率(如 0.05 表示5%):");
                pdoRatio.DefaultValue = 0.05;
                PromptDoubleResult pdrRatio = ed.GetDouble(pdoRatio);
                if (pdrRatio.Status != PromptStatus.OK) return;
                ratio = pdrRatio.Value;
                // 3. 调用迭代方法
                //    这里选用 minCellSize 作为主要调整参数(也可用 maxDepth)
                //    需要一个初始范围。例如(0.2, 20) 实际看多边形尺寸调整
                double minCellSizeLower = 0.1;
                double minCellSizeUpper = 50.0;
                double targetTolerancePercent = 0.05; // 相对误差5%内算收敛
                int maxIterations = 20;
                double finalMinCellSize = IterateGridByAreaRatio(
                     ntsPolygon,
                     diameter,
                     ratio,
                     minCellSizeLower,
                     minCellSizeUpper,
                     maxIterations,
                     targetTolerancePercent
                 );
                // 打印结果
                ed.WriteMessage($"\n迭代完成, 最终 minCellSize={finalMinCellSize:F2}\n");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n出现错误: {ex.Message}");
            }
        }
        /// <summary>
        /// 核心迭代方法：通过不断调整 minCellSize，使网格内桩总面积逼近目标置换率
        /// </summary>
        /// <param name="polygon">原始闭合多边形(NTS Polygon)</param>
        /// <param name="pileDiameter">桩直径</param>
        /// <param name="ratio">目标置换率(如0.05表示5%)</param>
        /// <param name="lowerBound">minCellSize下限</param>
        /// <param name="upperBound">minCellSize上限</param>
        /// <param name="maxIter">最大迭代次数</param>
        /// <param name="tolerancePercent">相对误差容忍度(如0.05表示5%)</param>
        /// <returns>逼近后得到的 minCellSize</returns>
        private static double IterateGridByAreaRatio(
            Polygon polygon,
            double pileDiameter,
            double ratio,
            double lowerBound,
            double upperBound,
            int maxIter,
            double tolerancePercent
        )
        {
            double A_poly = polygon.Area; // 多边形总面积
            double A_target = ratio * A_poly; // 目标总桩面积
            double A_onePile = Math.PI * (pileDiameter * pileDiameter) / 4.0; // 单桩截面
                                                                              // 二分法 or 简单迭代
            double low = lowerBound;
            double high = upperBound;
            for (int i = 0; i < maxIter; i++)
            {
                // 1) 取当前尝试值 mid
                double mid = (low + high) * 0.5;
                // 2) 基于 mid 生成网格单元列表
                List<QuadtreeNode> leaves = GenerateGridCells(polygon, mid);
                // 3) 统计有效单元数 = #cellsInside (or inside+partial)
                int nCells = 0;
                foreach (var leaf in leaves)
                {
                    if (leaf.Intersection == IntersectionType.Outside) continue;
                    // 这里简单地把 Partial 和 Inside 都计为1个有效格子
                    // 如果要更精细，也可判断“Partial”的面积比，然后做加权
                    nCells++;
                }
                // 4) 计算当前总桩面积
                double A_current = nCells * A_onePile;
                // 5) 判断与目标的偏差
                double diff = A_current - A_target;
                double relErr = Math.Abs(diff) / A_target;
                // 打印调试
                // Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
                // ed.WriteMessage($"\nIter={i}, mid={mid:F2}, nCells={nCells}, A_current={A_current:F2}, diff={diff:F2}, relErr={relErr:P2}");
                if (relErr < tolerancePercent)
                {
                    // 收敛
                    return mid;
                }
                // 6) 根据 diff 符号来调整区间
                if (diff < 0)
                {
                    // A_current < A_target => 桩总面积不足 => 细化网格 => 减小 minCellSize
                    high = mid;
                }
                else
                {
                    // A_current > A_target => 桩总面积过多 => 粗化网格 => 增大 minCellSize
                    low = mid;
                }
            }
            // 超过最大迭代次数，返回区间中点
            return (low + high) * 0.5;
        }
        /// <summary>
        /// 生成网格单元的示例方法(结构化+自适应),
        /// 依赖您已实现的 Quadtree + 裁剪逻辑。
        /// 这里简化演示，以 minCellSize 作为分辨率进行自适应划分。
        /// </summary>
        /// <param name="polygon">原始闭合多边形</param>
        /// <param name="minCellSize">当前迭代中给定的网格尺寸</param>
        /// <returns>四叉树叶子节点列表</returns>
        private static List<QuadtreeNode> GenerateGridCells(Polygon polygon, double minCellSize)
        {
            // 1) 获取 bounding box
            Envelope env = polygon.EnvelopeInternal;
            double xMin = env.MinX;
            double xMax = env.MaxX;
            double yMin = env.MinY;
            double yMax = env.MaxY;
            // 2) 设定一个 maxDepth(示例)，视需求可给个稍大的值
            int maxDepth = 10;
            // 3) 构造简易四叉树(复用您已有的 SimpleQuadtree 或类似方法)
            var quadtree = new SimpleQuadtree(
                xMin, xMax, yMin, yMax,
                maxDepth,
                minCellSize,
                node => CheckIntersection(polygon, node)
            );
            // 4) 获取叶子节点
            List<QuadtreeNode> leaves = quadtree.GetLeafNodes();
            return leaves;
        }
        /// <summary>
        /// 判断四叉树节点与多边形的关系(与您之前的CheckIntersection类似)
        /// </summary>
        //private static IntersectionType CheckIntersection(Polygon poly, QuadtreeNode node)
        //{
        //    var env = new Envelope(node.XMin, node.XMax, node.YMin, node.YMax);
        //    var gf = new GeometryFactory();
        //    var rectPoly = gf.ToGeometry(env) as NetTopologySuite.Geometries.Polygon;
        //    if (!poly.EnvelopeInternal.Intersects(env))
        //        return IntersectionType.Outside;
        //    if (poly.Contains(rectPoly))
        //        return IntersectionType.Inside;
        //    else if (!poly.Intersects(rectPoly))
        //        return IntersectionType.Outside;
        //    else
        //        return IntersectionType.Partial;
        //}
    }
}
