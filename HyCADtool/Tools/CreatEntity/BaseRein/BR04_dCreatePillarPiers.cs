using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using Exception = Autodesk.AutoCAD.Runtime.Exception;
[assembly: CommandClass(typeof(HyCADTool.BaseRein))]
namespace HyCADTool
{
    public static partial class BaseRein
    {
        public static void CreatePillarPiersExtend()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                try
                {
                    // 选择有限元网格，并绘制包络线
                    var sourceTextAndFinitePoly = SelectFiniteElementGrid();
                    var sourceTextAndEnvelopePoly = DrawBoundingPolyline(sourceTextAndFinitePoly); // 4a;
                                                                                                   // 创建柱墩放大轮廓
                    var polyExtendPillarPiers = new List<Polyline>();
                    // 处理PillarPiers，放大轮廓但不添加到数据库中
                    PillarPiers.ProcessEntities<Polyline>((polyline, trans) =>
                    {
                        var expandedPolyline = polyline.ExpandQuadrilateral(PillarPiersDistance, trans);
                        // 不添加 expandedPolyline 到数据库，只保存在内存中
                        polyExtendPillarPiers.Add(expandedPolyline);
                    });
                    // 调用 GroupByContainingPolyline 方法，处理最终的分组和添加逻辑
                    var resultDic = GroupByContainingPolyline(sourceTextAndEnvelopePoly, polyExtendPillarPiers, tr);
                    Tools.ZTools.SetCurrentLayer("00_hy_调整配筋轮廓");
                    var polylines = resultDic.Keys.ToList();
                    polylines.ToSpace();
                    // 通过 groupedData 添加需要保留的多边形到数据库中
                    // 你可以在这里进一步处理 resultDic 结果
                    // 提交事务
                    tr.Commit();
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n错误: {ex.Message}");
                    tr.Abort();
                }
            }
        }
        public static Dictionary<Polyline, Dictionary<DBText, ObjectId>> GroupByContainingPolyline(
     Dictionary<DBText, ObjectId> sourceTextAndEnvelopePoly,
     List<Polyline> polylines,
     Transaction transaction)
        {
            var groupedData = new Dictionary<Polyline, Dictionary<DBText, ObjectId>>();
            // 遍历每个大多边形 (polylines 中的 ObjectId)
            foreach (var bigPolyline in polylines)
            {
                // 获取大多边形对象               
                if (bigPolyline == null) continue;
                // 初始化保存符合条件的小多边形和DBText的字典
                var containedTexts = new Dictionary<DBText, ObjectId>();
                // 遍历字典中的每个小多边形
                foreach (var kvp in sourceTextAndEnvelopePoly)
                {
                    var dbText = kvp.Key;
                    var smallPolylineId = kvp.Value;
                    // 获取小多边形对象
                    var smallPolyline = transaction.GetObject(smallPolylineId, OpenMode.ForRead) as Polyline;
                    if (smallPolyline == null) continue;
                    // 检查小多边形是否在大多边形内部
                    if (IsPolylineInside(bigPolyline, smallPolyline))
                    {
                        // 如果在内部，将DBText和小多边形的ObjectId添加到字典中
                        containedTexts.Add(dbText, smallPolylineId);
                    }
                }
                if (containedTexts.Any())
                {
                    // 将大多边形调整为包含所有小多边形的最小包络矩形
                    var minimizedBigPolyline = MinimizeEnvelope(bigPolyline, containedTexts.Values, transaction);
                    groupedData.Add(minimizedBigPolyline, containedTexts);
                }
            }
            return groupedData;
        }
        private static Polyline MinimizeEnvelope(Polyline bigPolyline, ICollection<ObjectId> smallPolylineIds, Transaction transaction)
        {
            List<Point2d> allPoints = new List<Point2d>();
            // 首先收集大四边形的顶点
            // 获取大长方形的四个顶点
            var pbs0 = bigPolyline.GetPoint2dAt(0) + new Vector2d(PillarPiersDistance, PillarPiersDistance); // 左下
            var pbs1 = bigPolyline.GetPoint2dAt(1) + new Vector2d(-PillarPiersDistance, PillarPiersDistance); // 左上
            var pbs2 = bigPolyline.GetPoint2dAt(2) + new Vector2d(-PillarPiersDistance, -PillarPiersDistance);// 右上
            var pbs3 = bigPolyline.GetPoint2dAt(3) + new Vector2d(PillarPiersDistance, -PillarPiersDistance); // 右下
            allPoints.Add(pbs0);
            allPoints.Add(pbs1);
            allPoints.Add(pbs2);
            allPoints.Add(pbs3);
            // 收集所有小多边形的顶点
            foreach (var smallPolylineId in smallPolylineIds)
            {
                var smallPolyline = transaction.GetObject(smallPolylineId, OpenMode.ForRead) as Polyline;
                if (smallPolyline == null) continue;
                for (int i = 0; i < smallPolyline.NumberOfVertices; i++)
                {
                    allPoints.Add(smallPolyline.GetPoint2dAt(i));
                }
            }
            if (!allPoints.Any()) return bigPolyline; // 如果没有顶点则直接返回
                                                      // 找到顶点的最小和最大X、Y值
            var minX = allPoints.Min(point => point.X);
            var maxX = allPoints.Max(point => point.X);
            var minY = allPoints.Min(point => point.Y);
            var maxY = allPoints.Max(point => point.Y);
            // 根据计算的极值点，设置大多边形的顶点
            bigPolyline.SetPointAt(0, new Point2d(minX, minY)); // 左下
            bigPolyline.SetPointAt(1, new Point2d(maxX, minY)); // 下右
            bigPolyline.SetPointAt(2, new Point2d(maxX, maxY)); // 上右
            bigPolyline.SetPointAt(3, new Point2d(minX, maxY)); // 上左
            return bigPolyline;
        }
        // 辅助方法，检查所有小长方形是否在大长方形内部
        private static bool AreAllSmallPolylinesInside(Polyline bigPolyline, ICollection<ObjectId> smallPolylineIds, Transaction transaction)
        {
            foreach (var smallPolylineId in smallPolylineIds)
            {
                var smallPolyline = transaction.GetObject(smallPolylineId, OpenMode.ForRead) as Polyline;
                if (smallPolyline == null || !IsPolylineInside(bigPolyline, smallPolyline))
                {
                    return false;
                }
            }
            return true;
        }
        // 检查小长方形的所有顶点是否在大长方形内部
        private static bool IsPolylineInside(Polyline bigPolyline, Polyline smallPolyline)
        {
            // 获取大长方形的四个顶点
            var pb0 = bigPolyline.GetPoint2dAt(0); // 左下
            var pb1 = bigPolyline.GetPoint2dAt(1); // 左上
            var pb2 = bigPolyline.GetPoint2dAt(2); // 右上
            var pb3 = bigPolyline.GetPoint2dAt(3); // 右下
                                                   // 计算大长方形的边界范围
            double minX = Math.Min(pb0.X, pb1.X);
            double maxX = Math.Max(pb0.X, pb1.X);
            double minY = Math.Min(pb1.Y, pb2.Y);
            double maxY = Math.Max(pb1.Y, pb2.Y);
            // 遍历小长方形的四个顶点
            for (int i = 0; i < smallPolyline.NumberOfVertices; i++)
            {
                var ps = smallPolyline.GetPoint2dAt(i);
                // 检查小长方形的顶点是否在大长方形的边界内
                if (ps.X < minX || ps.X > maxX || ps.Y < minY || ps.Y > maxY)
                {
                    // 只要有一个顶点不在范围内，则小长方形不完全位于大长方形内
                    return false;
                }
            }
            // 如果所有顶点都在范围内，则小长方形完全位于大长方形内
            return true;
        }
    }
}
