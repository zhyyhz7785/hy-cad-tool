using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.DataStructures.DCEL;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Interfaces;
using System;
using System.Linq;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services
{
    /// <summary>
    /// DCEL 渲染器实现（DCEL Renderer Implementation）
    /// 将 DCEL 图渲染到 AutoCAD
    /// </summary>
    public class DCELRenderer : IDCELRenderer
    {
        /// <summary>
        /// 渲染 DCEL 图
        /// 根据 Face.IsOuter 属性分别绘制到不同图层
        /// </summary>
        public void Render(DCELGraph graph, string outerLayer = "dcelOuter", string innerLayer = "dcelInner")
        {
            if (graph == null)
                throw new ArgumentNullException(nameof(graph));

            var doc = Application.DocumentManager.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("无法获取当前活动文档");

            var db = doc.Database;

            using (var tr = db.TransactionManager.StartTransaction())
            {
                // 确保图层存在
                EnsureLayerExists(tr, db, outerLayer, 1); // 红色
                EnsureLayerExists(tr, db, innerLayer, 2);  // 黄色

                var bt = (BlockTable)tr.GetObject(db.BlockTableId, OpenMode.ForRead);
                var btr = (BlockTableRecord)tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite);

                // 统一遍历所有面，根据 IsOuter 属性分别绘制
                foreach (var face in graph.Faces)
                {
                    try
                    {
                        // 获取面的所有顶点
                        var vertices = face.Components
                            .Select(he => he.StartVertex.Position)
                            .Select(p => new Point2d(p.X, p.Y))
                            .ToList();

                        if (vertices.Count < 3)
                            continue; // 跳过无效的面

                        // 创建多段线
                        var polyline = new Polyline(vertices.Count);
                        polyline.Layer = face.IsOuter ? outerLayer : innerLayer;

                        for (int i = 0; i < vertices.Count; i++)
                        {
                            polyline.AddVertexAt(i, vertices[i], 0, 0, 0);
                        }

                        polyline.Closed = true;

                        // 添加到图形数据库
                        btr.AppendEntity(polyline);
                        tr.AddNewlyCreatedDBObject(polyline, true);
                    }
                    catch (Exception)
                    {
                        // 忽略单个面的绘制错误，继续处理其他面
                        continue;
                    }
                }

                tr.Commit();
            }
        }

        /// <summary>
        /// 确保图层存在（如果不存在则创建）
        /// </summary>
        private void EnsureLayerExists(Transaction tr, Database db, string layerName, short colorIndex)
        {
            var lt = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);

            if (!lt.Has(layerName))
            {
                lt.UpgradeOpen();
                var newLayer = new LayerTableRecord
                {
                    Name = layerName,
                    Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                        Autodesk.AutoCAD.Colors.ColorMethod.ByAci,
                        colorIndex)
                };
                lt.Add(newLayer);
                tr.AddNewlyCreatedDBObject(newLayer, true);
                lt.DowngradeOpen();
            }
        }
    }
}






