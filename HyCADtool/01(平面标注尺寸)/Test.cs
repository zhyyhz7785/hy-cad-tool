using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.DatabaseServices.Filters;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Config;
using HyCADTool.HelpClass;
using HyCADTool.Models.Cluster;
using HyCADTool.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using static HyCADTool.Tools.Tools;
namespace HyCADTool
{
    public static class HyTest
    {
        // ✨ 测试时指定方向：ForDown (X向水平标注) / ForLeft (Y向垂直标注)
        [CommandMethod("TEST_PROCESS_CLUSTER")]
        public static void TestProcessClusterSingle()
        {
            DimensionFor dimDirection = DimensionFor.ForDown;
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            var layerId = Tools.Tools.CreateLayer("00_hy_测试_标注", 91, db);
            // 选择点
            var points = SelectPoints();
            if (points.Count < 2)
            {
                ed.WriteMessage("\n点数量不足，测试终止。");
                return;
            }
            using (doc.LockDocument())
            using (Transaction tr = db.TransactionManager.StartTransaction())
            {
                // 创建测试聚类数据（模拟一个ClusterResult）
                var cluster = new ClusterResult
                {
                    Points = SortPoints(points, dimDirection) // ✨ 提前排序
                };
                // 调用ProcessClusterSingle测试
                // X方向标注
                var dimsX = ProcessClusterSingleX(cluster, layerId, DimensionFor.ForDown, true);
                // Y方向标注
                var dimsY = ProcessClusterSingleY(cluster, layerId, DimensionFor.ForLeft, true);
                // 将标注插入到模型空间
                dimsX.ToSpace(db);
                dimsY.ToSpace(db);
                tr.Commit();
            }
        }
        /// <summary>
        /// 从AutoCAD选择点
        /// </summary>
        private static List<Point3d> SelectPoints()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;
            var points = new List<Point3d>();
            var filter = new SelectionFilter(new[] { new TypedValue(0, "POINT") });
            var res = ed.GetSelection(filter);
            if (res.Status != PromptStatus.OK) return points;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (SelectedObject sel in res.Value)
                {
                    var dbPoint = tr.GetObject(sel.ObjectId, OpenMode.ForRead) as DBPoint;
                    if (dbPoint != null)
                    {
                        points.Add(dbPoint.Position);
                    }
                }
                tr.Commit();
            }
            return points;
        }
        /// <summary>
        /// 处理单个聚类的X方向（水平）标注，统一Y坐标，去重，并根据X间距调整标注偏移。
        /// </summary>
        private static List<RotatedDimension> ProcessClusterSingleX(ClusterResult cluster, ObjectId layerId, DimensionFor dimDirection, bool enableDoubleOffsetIfTooClose = true)
        {
            var dims = new List<RotatedDimension>();
            List<Point3d> points = cluster.Points ?? new List<Point3d>();
            if (points.Count < 2) return dims;
            // ✨ 拉平到统一Y
            double baseY = points.Min(p => p.Y);
            points = points.Select(p => new Point3d(p.X, baseY, 0)).ToList();
            // ✨ 去重
            points = points
                .Distinct(new Point3dEqualityComparer(0.0001))
                .OrderBy(p => p.X) // X向排序
                .ToList();
            if (points.Count < 2) return dims;
            double defaultOffset = 5 * BaseConfig.Scale;
            double distanceThreshold = 3 * BaseConfig.Scale;
            for (int i = 0; i < points.Count - 1; i++)
            {
                var p1 = points[i];
                var p2 = points[i + 1];
                double dx = Math.Abs(p2.X - p1.X);
                double effectiveOffset = defaultOffset;
                if (enableDoubleOffsetIfTooClose && dx < distanceThreshold)
                {
                    effectiveOffset = 2 * defaultOffset;
                }
                var dim = Tools.Tools.GetDimByTwoPoints(p1, p2, effectiveOffset, dimDirection, true);
                dim.LayerId = layerId;
                dim.DimensionStyle = Application.DocumentManager.MdiActiveDocument.Database.Dimstyle;
                dims.Add(dim);
            }
            return dims;
        }
        /// <summary>
        /// 处理单个聚类的Y方向（竖直）标注，统一X坐标，去重，并根据Y间距调整标注偏移。
        /// </summary>
        private static List<RotatedDimension> ProcessClusterSingleY(ClusterResult cluster, ObjectId layerId, DimensionFor dimDirection, bool enableDoubleOffsetIfTooClose = true)
        {
            var dims = new List<RotatedDimension>();
            List<Point3d> points = cluster.Points ?? new List<Point3d>();
            if (points.Count < 2) return dims;
            // ✨ 拉平到统一X
            double baseX = points.Min(p => p.X);
            points = points.Select(p => new Point3d(baseX, p.Y, 0)).ToList();
            // ✨ 去重
            points = points
                .Distinct(new Point3dEqualityComparer(0.0001))
                .OrderBy(p => p.Y) // Y向排序
                .ToList();
            if (points.Count < 2) return dims;
            double defaultOffset = 5 * BaseConfig.Scale;
            double distanceThreshold = 3 * BaseConfig.Scale;
            for (int i = 0; i < points.Count - 1; i++)
            {
                var p1 = points[i];
                var p2 = points[i + 1];
                double dy = Math.Abs(p2.Y - p1.Y);
                double effectiveOffset = defaultOffset;
                if (enableDoubleOffsetIfTooClose && dy < distanceThreshold)
                {
                    effectiveOffset = 2 * defaultOffset;
                }
                var dim = Tools.Tools.GetDimByTwoPoints(p1, p2, effectiveOffset, dimDirection, true);
                dim.LayerId = layerId;
                dim.DimensionStyle = Application.DocumentManager.MdiActiveDocument.Database.Dimstyle;
                dims.Add(dim);
            }
            return dims;
        }
        /// <summary>
        /// 根据标注方向对点集合排序
        /// </summary>
        private static List<Point3d> SortPoints(List<Point3d> points, DimensionFor dimDirection)
        {
            if (points == null || points.Count == 0) return new List<Point3d>();
            if (dimDirection == DimensionFor.ForDown || dimDirection == DimensionFor.ForUp)
            {
                // X向标注，按X升序，再按Y次序
                return points.OrderBy(p => p.X).ThenBy(p => p.Y).ToList();
            }
            else
            {
                // Y向标注，按Y升序，再按X次序
                return points.OrderBy(p => p.Y).ThenBy(p => p.X).ToList();
            }
        }
    }
}
