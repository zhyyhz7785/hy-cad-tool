using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCAD.Geometry;
using System;
using System.Collections.Generic;

namespace HyCADTool.Shell.Commands
{
    /// <summary>
    /// 测试多边形偏移命令（C16）
    /// </summary>
    public class TestOffsetCommand
    {
        /// <summary>
        /// 执行测试偏移命令
        /// </summary>
        public void Execute()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var db = doc.Database;
            var ed = doc.Editor;

            ed.WriteMessage("\n=== 多边形偏移测试（C16） ===");
            ed.WriteMessage("\n请选择一个封闭的多段线进行偏移测试");

            try
            {
                // 选择多段线
                var polyline = SelectPolyline(ed, db);
                if (polyline == null)
                {
                    ed.WriteMessage("\n未选择有效的多段线");
                    return;
                }

                // 获取偏移距离
                var distanceResult = ed.GetDistance("\n输入偏移距离（mm）<100>: ");
                double offsetDistance = 100.0;
                if (distanceResult.Status == PromptStatus.OK)
                {
                    offsetDistance = distanceResult.Value;
                }

                ed.WriteMessage($"\n偏移距离：{offsetDistance:F2} mm");

                // 转换为 Polygon2D
                var polygon2D = ConvertToPolygon2D(polyline);
                ed.WriteMessage($"\n原始多边形：顶点数={polygon2D.VertexCount}");

                // 计算面积和方向
                double signedArea = polygon2D.GetSignedArea();
                bool isCCW = signedArea > 0;
                ed.WriteMessage($"\n多边形方向：{(isCCW ? "逆时针" : "顺时针")}");
                ed.WriteMessage($"\n有向面积：{signedArea:F2}");

                // 测试外偏移
                ed.WriteMessage("\n\n--- 测试外偏移 ---");
                try
                {
                    var outerPolygon = polygon2D.Offset(offsetDistance, isOutward: true);
                    ed.WriteMessage($"\n外偏移成功：顶点数={outerPolygon.VertexCount}");

                    // 绘制外偏移结果
                    DrawPolygon2D(outerPolygon, "00_hy_测试_外偏移", 3, db, 0); // 绿色
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n✗ 外偏移失败：{ex.Message}");
                }

                // 测试内偏移
                ed.WriteMessage("\n\n--- 测试内偏移 ---");
                try
                {
                    var innerPolygon = polygon2D.Offset(offsetDistance, isOutward: false);
                    ed.WriteMessage($"\n内偏移成功：顶点数={innerPolygon.VertexCount}");

                    // 绘制内偏移结果
                    DrawPolygon2D(innerPolygon, "00_hy_测试_内偏移", 5, db, 0); // 蓝色
                }
                catch (Exception ex)
                {
                    ed.WriteMessage($"\n✗ 内偏移失败：{ex.Message}");
                }

                // 高亮原始多边形
                using (var tr = db.TransactionManager.StartTransaction())
                {
                    polyline.UpgradeOpen();
                    polyline.ColorIndex = 1; // 红色
                    polyline.DowngradeOpen();
                    tr.Commit();
                }

                ed.WriteMessage("\n\n测试完成！");
                ed.WriteMessage("\n图层说明：");
                ed.WriteMessage("\n  - 原始多边形：红色（当前图层）");
                ed.WriteMessage("\n  - 外偏移：绿色（00_hy_测试_外偏移）");
                ed.WriteMessage("\n  - 内偏移：蓝色（00_hy_测试_内偏移）");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n错误：{ex.Message}");
                ed.WriteMessage($"\n堆栈：{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 选择多段线
        /// </summary>
        private Polyline SelectPolyline(Editor ed, Database db)
        {
            var options = new PromptEntityOptions("\n选择一个封闭的多段线：");
            options.SetRejectMessage("\n必须是多段线");
            options.AddAllowedClass(typeof(Polyline), true);

            var result = ed.GetEntity(options);
            if (result.Status != PromptStatus.OK)
                return null;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                var polyline = tr.GetObject(result.ObjectId, OpenMode.ForRead) as Polyline;

                if (polyline == null || !polyline.Closed)
                {
                    ed.WriteMessage("\n多段线必须是封闭的");
                    tr.Commit();
                    return null;
                }

                tr.Commit();
                return polyline;
            }
        }

        /// <summary>
        /// 转换 Polyline 为 Polygon2D
        /// </summary>
        private Polygon2D ConvertToPolygon2D(Polyline polyline)
        {
            var points = new List<Point2D>();

            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                var pt = polyline.GetPoint2dAt(i);
                points.Add(new Point2D(pt.X, pt.Y));
            }

            return new Polygon2D(points, true);
        }

        /// <summary>
        /// 绘制 Polygon2D
        /// </summary>
        private void DrawPolygon2D(Polygon2D polygon, string layerName, int colorIndex, Database db, double elevation)
        {
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var modelSpace = (BlockTableRecord)tr.GetObject(
                    db.CurrentSpaceId,
                    OpenMode.ForWrite);

                // 确保图层存在
                EnsureLayerExists(db, tr, layerName, colorIndex);

                // 创建多段线
                var polyline = new Polyline();
                for (int i = 0; i < polygon.VertexCount; i++)
                {
                    var vertex = polygon.Vertices[i];
                    polyline.AddVertexAt(i, new Point2d(vertex.X, vertex.Y), 0, 0, 0);
                }
                polyline.Closed = true;
                polyline.Elevation = elevation;
                polyline.Layer = layerName;

                modelSpace.AppendEntity(polyline);
                tr.AddNewlyCreatedDBObject(polyline, true);

                tr.Commit();
            }
        }

        /// <summary>
        /// 确保图层存在
        /// </summary>
        private void EnsureLayerExists(Database db, Transaction tr, string layerName, int colorIndex)
        {
            var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

            if (!layerTable.Has(layerName))
            {
                layerTable.UpgradeOpen();
                var layerTableRecord = new LayerTableRecord
                {
                    Name = layerName,
                    Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                        Autodesk.AutoCAD.Colors.ColorMethod.ByAci,
                        (short)colorIndex)
                };
                layerTable.Add(layerTableRecord);
                tr.AddNewlyCreatedDBObject(layerTableRecord, true);
                layerTable.DowngradeOpen();
            }
        }
    }
}












