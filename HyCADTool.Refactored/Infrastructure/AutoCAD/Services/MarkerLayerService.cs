using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services
{
    /// <summary>
    /// 标记图层服务 - 统一管理紫色标记图层
    /// 用于在图纸上绘制临时标记（打断点、重复线、问题区域等）
    /// </summary>
    public class MarkerLayerService
    {
        private const short MARKER_COLOR = 200; // 紫色 (ACI 200)

        // 标记图层名称常量
        public const string LAYER_BREAK_POINTS = "00_HY_标记_打断点";
        public const string LAYER_DUPLICATE_LINES = "00_HY_标记_重复线";
        public const string LAYER_OUTER_POLYGONS = "00_HY_标记_外轮廓";
        public const string LAYER_INNER_HOLES = "00_HY_标记_内孔洞";
        public const string LAYER_ELEVATION_CHECK = "00_HY_标记_标高检查";
        public const string LAYER_INDEPENDENT_ENDPOINTS = "00_HY_标记_独立端点";

        /// <summary>
        /// 确保标记图层存在
        /// </summary>
        /// <param name="db">数据库</param>
        /// <param name="trans">事务</param>
        /// <param name="layerName">图层名称</param>
        public void EnsureMarkerLayer(Database db, Transaction trans, string layerName)
        {
            LayerTable lt = trans.GetObject(db.LayerTableId, OpenMode.ForRead) as LayerTable;

            if (!lt.Has(layerName))
            {
                lt.UpgradeOpen();
                LayerTableRecord ltr = new LayerTableRecord();
                ltr.Name = layerName;
                ltr.Color = Autodesk.AutoCAD.Colors.Color.FromColorIndex(
                    Autodesk.AutoCAD.Colors.ColorMethod.ByAci, MARKER_COLOR);
                
                // 设置为不可打印
                ltr.IsPlottable = false;
                
                lt.Add(ltr);
                trans.AddNewlyCreatedDBObject(ltr, true);
                lt.DowngradeOpen();
            }
        }

        /// <summary>
        /// 在标记图层绘制圆圈标记
        /// </summary>
        /// <param name="db">数据库</param>
        /// <param name="trans">事务</param>
        /// <param name="center">圆心</param>
        /// <param name="radius">半径</param>
        /// <param name="layerName">图层名称</param>
        public void DrawCircleMarker(Database db, Transaction trans, Point3d center, double radius, string layerName)
        {
            BlockTable bt = trans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            BlockTableRecord btr = trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

            Circle circle = new Circle();
            circle.Center = center;
            circle.Radius = radius;
            circle.Layer = layerName;

            btr.AppendEntity(circle);
            trans.AddNewlyCreatedDBObject(circle, true);
        }

        /// <summary>
        /// 在标记图层绘制多段线标记
        /// </summary>
        /// <param name="db">数据库</param>
        /// <param name="trans">事务</param>
        /// <param name="points">点集合</param>
        /// <param name="layerName">图层名称</param>
        /// <param name="closed">是否闭合</param>
        public void DrawPolylineMarker(Database db, Transaction trans, IEnumerable<Point3d> points, string layerName, bool closed = false)
        {
            BlockTable bt = trans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            BlockTableRecord btr = trans.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;

            Polyline pline = new Polyline();
            int index = 0;
            foreach (var point in points)
            {
                pline.AddVertexAt(index++, new Point2d(point.X, point.Y), 0, 0, 0);
            }
            pline.Closed = closed;
            pline.Layer = layerName;

            btr.AppendEntity(pline);
            trans.AddNewlyCreatedDBObject(pline, true);
        }
    }
}

