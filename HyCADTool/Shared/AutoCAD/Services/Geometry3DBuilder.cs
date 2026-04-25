using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.ApplicationServices;
using HyCADTool.Domain.Entities;
using HyCADTool.Shared.Geometry;
using HyCADTool.Shared.AutoCAD.Interfaces;

namespace HyCADTool.Shared.AutoCAD.Services
{
    /// <summary>
    /// AutoCAD 3D几何体构建器实现
    /// 将平台无关的Domain实体转换为AutoCAD Solid3d对象
    /// </summary>
    public class Geometry3DBuilder : IGeometry3DBuilder
    {
        private readonly double _scale;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="scale">比例（默认1.0，如果需要mm转m则传0.001）</param>
        public Geometry3DBuilder(double scale = 1.0)
        {
            _scale = scale;
        }
        
        /// <summary>
        /// 从墙体实体创建AutoCAD 3D Solid
        /// </summary>
        public Solid3d CreateWallSolid(Wall3D wall)
        {
            if (wall == null)
                throw new ArgumentNullException(nameof(wall));
            
            return ExtrudePolygon(
                wall.BufferRegion,
                wall.BottomElevation.Value,
                wall.TopElevation.Value);
        }
        
        /// <summary>
        /// 在事务上下文中创建墙体 Solid
        /// 注意：CreateExtrudedSolid 要求 Polyline 必须在数据库中（Database-Resident）
        /// </summary>
        public Solid3d CreateWallSolidInTransaction(
            Wall3D wall,
            Transaction tr,
            BlockTableRecord modelSpace)
        {
            if (wall == null)
                throw new ArgumentNullException(nameof(wall));
            if (tr == null)
                throw new ArgumentNullException(nameof(tr));
            if (modelSpace == null)
                throw new ArgumentNullException(nameof(modelSpace));
            
            // 计算拉伸高度
            double height = (wall.TopElevation.Value - wall.BottomElevation.Value) * _scale;
            
            if (height <= 0)
                throw new ArgumentException($"拉伸高度必须为正值，当前：{height}");
            
            // 1. 创建底面多段线
            Polyline polyline = CreatePolyline(wall.BufferRegion);
            
            // 验证多段线有效性
            if (!polyline.Closed)
            {
                polyline.Dispose();
                throw new InvalidOperationException("多边形未闭合，无法拉伸");
            }
            
            if (polyline.NumberOfVertices < 3)
            {
                polyline.Dispose();
                throw new InvalidOperationException($"多边形顶点数不足（{polyline.NumberOfVertices}），至少需要3个顶点");
            }
            
            // 设置多段线的标高（Z坐标）
            polyline.Elevation = wall.BottomElevation.Value * _scale;
            
            // 2. 将 Polyline 添加到数据库（临时）
            modelSpace.AppendEntity(polyline);
            tr.AddNewlyCreatedDBObject(polyline, true);
            
            // 3. 创建3D Solid
            Solid3d solid = new Solid3d();
            
            try
            {
                // 使用AutoCAD的CreateExtrudedSolid方法
                solid.CreateExtrudedSolid(polyline, new Vector3d(0, 0, height), new SweepOptions());
                
                // 4. 将 Solid 添加到数据库
                modelSpace.AppendEntity(solid);
                tr.AddNewlyCreatedDBObject(solid, true);
                
                // 5. 删除临时 Polyline
                polyline.Erase();
                
                return solid;
            }
            catch (Exception ex)
            {
                solid.Dispose();
                polyline.Erase(); // 清理临时 Polyline
                throw new InvalidOperationException($"拉伸多边形失败：{ex.Message}", ex);
            }
        }
        
        /// <summary>
        /// 从筏板实体创建AutoCAD 3D Solid
        /// </summary>
        public Solid3d CreateSlabSolid(Slab3D slab)
        {
            if (slab == null)
                throw new ArgumentNullException(nameof(slab));
            
            return ExtrudePolygon(
                slab.Region,
                slab.BottomElevation.Value,
                slab.TopElevation.Value);
        }
        
        /// <summary>
        /// 批量创建墙体Solid
        /// </summary>
        public List<Solid3d> CreateWallSolids(IEnumerable<Wall3D> walls)
        {
            return walls.Select(w => CreateWallSolid(w)).ToList();
        }
        
        /// <summary>
        /// 批量创建筏板Solid
        /// </summary>
        public List<Solid3d> CreateSlabSolids(IEnumerable<Slab3D> slabs)
        {
            return slabs.Select(s => CreateSlabSolid(s)).ToList();
        }
        
        /// <summary>
        /// 从平台无关的Polygon2D创建AutoCAD Polyline
        /// </summary>
        public Polyline CreatePolyline(Polygon2D polygon)
        {
            if (polygon == null)
                throw new ArgumentNullException(nameof(polygon));
            
            var polyline = new Polyline();
            
            for (int i = 0; i < polygon.VertexCount; i++)
            {
                var vertex = polygon.Vertices[i];
                polyline.AddVertexAt(
                    i,
                    new Point2d(vertex.X * _scale, vertex.Y * _scale),
                    0, 0, 0);
            }
            
            // 如果是闭合多边形，设置闭合标志
            if (polygon.IsClosed)
            {
                polyline.Closed = true;
            }
            
            return polyline;
        }
        
        /// <summary>
        /// 拉伸多边形创建3D Solid
        /// 核心算法：与原 ElevationModelGenerator.ExtrudeWallRegion 保持一致
        /// 
        /// 注意：CreateExtrudedSolid 需要 Polyline 在数据库中（Database-Resident）
        /// 参考：https://adndevblog.typepad.com/autocad/2012/06/createextrudedsolid-throws-ekeynotfound.html
        /// </summary>
        public Solid3d ExtrudePolygon(
            Polygon2D polygon,
            double bottomElevation,
            double topElevation)
        {
            if (polygon == null)
                throw new ArgumentNullException(nameof(polygon));
            
            // 计算拉伸高度（单位：米）
            double height = (topElevation - bottomElevation) * _scale;
            
            if (height <= 0)
                throw new ArgumentException($"拉伸高度必须为正值，当前：{height}");
            
            // 1. 创建底面多段线（2D → 3D，设置Z坐标为底部标高）
            Polyline polyline = CreatePolyline(polygon);
            
            // 验证多段线有效性
            if (!polyline.Closed)
            {
                polyline.Dispose();
                throw new InvalidOperationException("多边形未闭合，无法拉伸");
            }
            
            if (polyline.NumberOfVertices < 3)
            {
                polyline.Dispose();
                throw new InvalidOperationException($"多边形顶点数不足（{polyline.NumberOfVertices}），至少需要3个顶点");
            }
            
            // 设置多段线的标高（Z坐标）
            polyline.Elevation = bottomElevation * _scale;
            
            // 2. 创建3D Solid
            // 注意：CreateExtrudedSolid 要求 Polyline 必须在数据库中（Database-Resident）
            // 但我们不想污染数据库，所以使用 SetDatabaseDefaults 来设置数据库上下文
            Solid3d solid = new Solid3d();
            
            try
            {
                // 设置 Polyline 的数据库默认值（使其成为 Database-Resident）
                var db = HostApplicationServices.WorkingDatabase;
                polyline.SetDatabaseDefaults(db);
                
                // 使用AutoCAD的CreateExtrudedSolid方法
                solid.CreateExtrudedSolid(polyline, new Vector3d(0, 0, height), new SweepOptions());
                
                return solid;
            }
            catch (Exception ex)
            {
                solid.Dispose();
                throw new InvalidOperationException($"拉伸多边形失败：{ex.Message}", ex);
            }
            finally
            {
                // 清理临时多段线
                polyline.Dispose();
            }
        }
        
        /// <summary>
        /// 验证Solid3d是否有效
        /// </summary>
        public bool ValidateSolid(Solid3d solid, out string errorMessage)
        {
            if (solid == null)
            {
                errorMessage = "Solid3d对象为空";
                return false;
            }
            
            try
            {
                // 检查体积
                double volume = solid.MassProperties.Volume;
                if (volume <= 0)
                {
                    errorMessage = $"Solid3d体积无效：{volume}";
                    return false;
                }
                
                errorMessage = null;
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = $"验证Solid3d时出错：{ex.Message}";
                return false;
            }
        }
    }
}

