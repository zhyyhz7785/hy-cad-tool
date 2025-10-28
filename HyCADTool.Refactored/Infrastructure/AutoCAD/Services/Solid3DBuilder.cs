using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Interfaces;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services
{
    /// <summary>
    /// 3D 实体构建器实现
    /// 负责创建 AutoCAD 3D 实体（墙体、筏板等）
    /// </summary>
    public class Solid3DBuilder : ISolid3DBuilder
    {
        /// <summary>
        /// 创建墙体实体（矩形拉伸）
        /// </summary>
        public Solid3d CreateWallSolid(
            Point2D v1,
            Point2D v2,
            double bottomElevation,
            double wallHeight,
            Vector2D outwardNormal,
            double wallThickness,
            int offsetDirection = 1)
        {
            var offsetNormal = new Vector2D(
                outwardNormal.X * offsetDirection,
                outwardNormal.Y * offsetDirection);
            
            var outerV1 = new Point2D(
                v1.X + offsetNormal.X * wallThickness,
                v1.Y + offsetNormal.Y * wallThickness);
            
            var outerV2 = new Point2D(
                v2.X + offsetNormal.X * wallThickness,
                v2.Y + offsetNormal.Y * wallThickness);
            
            using (var wallPolyline = new Polyline())
            {
                wallPolyline.AddVertexAt(0, new Point2d(v1.X, v1.Y), 0, 0, 0);
                wallPolyline.AddVertexAt(1, new Point2d(v2.X, v2.Y), 0, 0, 0);
                wallPolyline.AddVertexAt(2, new Point2d(outerV2.X, outerV2.Y), 0, 0, 0);
                wallPolyline.AddVertexAt(3, new Point2d(outerV1.X, outerV1.Y), 0, 0, 0);
                wallPolyline.Closed = true;
                wallPolyline.Elevation = bottomElevation;
                
                var wallSolid = new Solid3d();
                wallSolid.CreateExtrudedSolid(
                    wallPolyline,
                    new Vector3d(0, 0, wallHeight),
                    new SweepOptions());
                
                return wallSolid;
            }
        }
        
        /// <summary>
        /// 创建筏板实体（多边形拉伸）
        /// </summary>
        public Solid3d CreateSlabSolid(
            Polygon2D polygon,
            double bottomElevation,
            double height)
        {
            using (var polyline = new Polyline())
            {
                for (int i = 0; i < polygon.VertexCount; i++)
                {
                    var v = polygon.Vertices[i];
                    polyline.AddVertexAt(i, new Point2d(v.X, v.Y), 0, 0, 0);
                }
                polyline.Closed = true;
                polyline.Elevation = bottomElevation;
                
                var solid = new Solid3d();
                solid.CreateExtrudedSolid(
                    polyline,
                    new Vector3d(0, 0, height),
                    new SweepOptions());
                
                return solid;
            }
        }
    }
}

