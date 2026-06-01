using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.Colors;
using HyCAD.Geometry;
using HyCADTool.Shared.AutoCAD.Interfaces;

namespace HyCADTool.Features.Elevation.Services
{
    /// <summary>
    /// 墙体构建器实现
    /// 使用 AutoCAD API 创建墙体实体
    /// </summary>
    public class WallBuilder : IWallBuilder
    {
        public Solid3d CreateWall(
            Transaction tr,
            Line2D edge,
            double wallThickness,
            double wallHeight,
            int offsetDirection,
            double baseElevation,
            string layerName)
        {
            try
            {
                var db = HostApplicationServices.WorkingDatabase;
                
                // 1. 创建内侧多段线（原始边）
                var innerPolyline = CreatePolylineFromEdge(edge, baseElevation);
                
                // 2. 偏移创建外侧多段线
                double offsetDistance = wallThickness * offsetDirection;
                var outerPolyline = CreateOffsetPolyline(innerPolyline, offsetDistance);
                
                if (outerPolyline == null)
                {
                    innerPolyline.Dispose();
                    return null;
                }
                
                // 3. 拉伸内外多段线为实体
                var innerSolid = CreateExtrudedSolid(innerPolyline, wallHeight, baseElevation, db);
                var outerSolid = CreateExtrudedSolid(outerPolyline, wallHeight, baseElevation, db);
                
                if (innerSolid == null || outerSolid == null)
                {
                    innerPolyline.Dispose();
                    outerPolyline.Dispose();
                    innerSolid?.Dispose();
                    outerSolid?.Dispose();
                    return null;
                }
                
                // 4. 布尔运算（外-内=墙体）
                outerSolid.BooleanOperation(BooleanOperationType.BoolSubtract, innerSolid);
                
                // 5. 添加到数据库
                var modelSpace = (BlockTableRecord)tr.GetObject(
                    db.CurrentSpaceId,
                    OpenMode.ForWrite);
                
                modelSpace.AppendEntity(outerSolid);
                tr.AddNewlyCreatedDBObject(outerSolid, true);
                
                // 6. 设置图层
                EnsureLayerExists(tr, db, layerName, 3); // 黄色
                outerSolid.Layer = layerName;
                
                // 清理
                innerPolyline.Dispose();
                outerPolyline.Dispose();
                innerSolid.Dispose();
                
                return outerSolid;
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// 从边创建多段线
        /// </summary>
        private Polyline CreatePolylineFromEdge(Line2D edge, double ElevationValue)
        {
            var polyline = new Polyline();
            polyline.AddVertexAt(0, new Point2d(edge.StartPoint.X, edge.StartPoint.Y), 0, 0, 0);
            polyline.AddVertexAt(1, new Point2d(edge.EndPoint.X, edge.EndPoint.Y), 0, 0, 0);
            polyline.Elevation = ElevationValue;
            
            return polyline;
        }
        
        /// <summary>
        /// 偏移多段线
        /// </summary>
        private Polyline CreateOffsetPolyline(Polyline source, double offsetDistance)
        {
            try
            {
                var offsetCurves = source.GetOffsetCurves(offsetDistance);
                
                if (offsetCurves == null || offsetCurves.Count == 0)
                {
                    return null;
                }
                
                var offsetPolyline = offsetCurves[0] as Polyline;
                
                // 清理其他偏移曲线
                for (int i = 1; i < offsetCurves.Count; i++)
                {
                    offsetCurves[i].Dispose();
                }
                
                return offsetPolyline;
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// 拉伸多段线为实体
        /// </summary>
        private Solid3d CreateExtrudedSolid(
            Polyline polyline,
            double height,
            double baseElevation,
            Database db)
        {
            try
            {
                polyline.SetDatabaseDefaults(db);
                
                var region = new DBObjectCollection();
                var curves = new DBObjectCollection { polyline };
                var regions = Region.CreateFromCurves(curves);
                
                if (regions.Count == 0)
                {
                    return null;
                }
                
                var regionObj = regions[0] as Region;
                if (regionObj == null)
                {
                    return null;
                }
                
                var solid = new Solid3d();
                solid.CreateExtrudedSolid(regionObj, new Vector3d(0, 0, height), new SweepOptions());
                
                regionObj.Dispose();
                
                return solid;
            }
            catch
            {
                return null;
            }
        }
        
        /// <summary>
        /// 确保图层存在
        /// </summary>
        private void EnsureLayerExists(
            Transaction tr,
            Database db,
            string layerName,
            short colorIndex)
        {
            var layerTable = (LayerTable)tr.GetObject(db.LayerTableId, OpenMode.ForRead);
            
            if (!layerTable.Has(layerName))
            {
                layerTable.UpgradeOpen();
                
                var layerTableRecord = new LayerTableRecord
                {
                    Name = layerName,
                    Color = Color.FromColorIndex(ColorMethod.ByAci, colorIndex)
                };
                
                layerTable.Add(layerTableRecord);
                tr.AddNewlyCreatedDBObject(layerTableRecord, true);
                
                layerTable.DowngradeOpen();
            }
        }
    }
}

