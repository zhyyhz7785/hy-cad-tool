using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Shell.Contracts;
using HyCADTool.Shared.Geometry;
using HyCADTool.Shared.AutoCAD.Converters;
using System;
using System.Collections.Generic;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Shared.AutoCAD.Repositories
{
    /// <summary>
    /// 线段仓储实现
    /// 封装 AutoCAD 线段的读取、创建、删除操作
    /// </summary>
    public class LineRepository : ILineRepository
    {
        private readonly IGeometryConverter _converter;
        private readonly ILayerService _layerService;
        
        public LineRepository(
            IGeometryConverter converter,
            ILayerService layerService)
        {
            _converter = converter;
            _layerService = layerService;
        }
        
        /// <summary>
        /// 从选择集获取线段
        /// </summary>
        public List<(Line2D Line, ObjectId OriginalId)> GetSelectedLines(SelectionSet selection)
        {
            var result = new List<(Line2D, ObjectId)>();
            
            if (selection == null || selection.Count == 0)
                return result;
            
            var db = selection[0].ObjectId.Database;
            using (var tr = db.TransactionManager.StartTransaction())
            {
                foreach (SelectedObject selObj in selection)
                {
                    var line = tr.GetObject(selObj.ObjectId, OpenMode.ForRead) as Line;
                    if (line != null)
                    {
                        var line2d = _converter.FromAutoCADLine(line);
                        result.Add((line2d, selObj.ObjectId));
                    }
                }
                tr.Commit();
            }
            
            return result;
        }
        
        /// <summary>
        /// 替换线段（删除旧线段，添加新线段）
        /// </summary>
        public void ReplaceLines(
            Transaction transaction,
            List<ObjectId> oldLineIds,
            List<Line2D> newLines)
        {
            if (oldLineIds.Count == 0) return;
            
            var db = oldLineIds[0].Database;
            var btr = GetModelSpace(transaction, db);
            
            // 删除旧线段
            foreach (var id in oldLineIds)
            {
                if (!id.IsValid || id.IsErased) continue;
                
                var ent = transaction.GetObject(id, OpenMode.ForWrite) as Entity;
                if (ent != null && !ent.IsErased)
                {
                    ent.Erase();
                }
            }
            
            // 添加新线段
            foreach (var line2d in newLines)
            {
                var acadLine = _converter.ToAutoCADLine(line2d);
                btr.AppendEntity(acadLine);
                transaction.AddNewlyCreatedDBObject(acadLine, true);
            }
        }
        
        // TODO: Application层删除后暂时注释掉 WarningMarker 相关功能
        // /// <summary>
        // /// 创建警告标记（矩形）
        // /// </summary>
        // public void CreateWarningMarkers(
        //     Transaction transaction,
        //     List<WarningMarker> markers)
        // {
        //     if (markers.Count == 0) return;
        //     
        //     // 确保警告图层存在
        //     if (!_layerService.LayerExists("00_HY_警告_红色"))
        //     {
        //         _layerService.CreateLayer("00_HY_警告_红色", 1);
        //     }
        //     
        //     var doc = AcApp.DocumentManager.MdiActiveDocument;
        //     var db = doc.Database;
        //     var btr = GetModelSpace(transaction, db);
        //     
        //     foreach (var marker in markers)
        //     {
        //         var rect = CreateWarningRectangle(
        //             marker.Location,
        //             marker.Direction,
        //             marker.Size);
        //         
        //         rect.Layer = "00_HY_警告_红色";
        //         btr.AppendEntity(rect);
        //         transaction.AddNewlyCreatedDBObject(rect, true);
        //     }
        // }
        // 
        // /// <summary>
        // /// 创建警告矩形
        // /// </summary>
        // private Polyline CreateWarningRectangle(
        //     Point2D center, 
        //     Vector2D direction, 
        //     double length)
        // {
        //     double width = length / 2;
        //     
        //     // 计算垂直方向
        //     Vector2D perpendicular = new Vector2D(-direction.Y, direction.X);
        //     
        //     // 计算四个角点
        //     Point2D p1 = center.Add(perpendicular * (width / 2)).Subtract(direction * (length / 2));
        //     Point2D p2 = center.Add(perpendicular * (width / 2)).Add(direction * (length / 2));
        //     Point2D p3 = center.Subtract(perpendicular * (width / 2)).Add(direction * (length / 2));
        //     Point2D p4 = center.Subtract(perpendicular * (width / 2)).Subtract(direction * (length / 2));
        //     
        //     // 创建多段线
        //     Polyline rect = new Polyline();
        //     rect.AddVertexAt(0, new Point2d(p1.X, p1.Y), 0, 0, 0);
        //     rect.AddVertexAt(1, new Point2d(p2.X, p2.Y), 0, 0, 0);
        //     rect.AddVertexAt(2, new Point2d(p3.X, p3.Y), 0, 0, 0);
        //     rect.AddVertexAt(3, new Point2d(p4.X, p4.Y), 0, 0, 0);
        //     rect.Closed = true;
        //     
        //     return rect;
        // }
        
        /// <summary>
        /// 获取模型空间
        /// </summary>
        private BlockTableRecord GetModelSpace(Transaction tr, Database db)
        {
            var bt = tr.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
            return tr.GetObject(bt[BlockTableRecord.ModelSpace], OpenMode.ForWrite) as BlockTableRecord;
        }
    }
}

