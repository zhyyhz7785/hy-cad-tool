using System;
using System.Collections.Generic;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Features.Elevation.Domain.ValueObjects;
using HyCAD.Geometry;

namespace HyCADTool.Shared.AutoCAD.Services
{
    /// <summary>
    /// 多边形适配器
    /// 负责AutoCAD Polyline与平台无关Polygon2D之间的转换
    /// </summary>
    public class PolygonAdapter
    {
        private readonly double _scale;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="scale">比例（默认1.0，如果需要mm转m则传0.001）</param>
        public PolygonAdapter(double scale = 1.0)
        {
            _scale = scale;
        }
        
        /// <summary>
        /// 将AutoCAD Polyline转换为平台无关的Polygon2D
        /// </summary>
        public Polygon2D ConvertToPolygon2D(Polyline polyline)
        {
            if (polyline == null)
                throw new ArgumentNullException(nameof(polyline));
            
            var vertices = new List<Point2D>();
            
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                Point2d point = polyline.GetPoint2dAt(i);
                vertices.Add(new Point2D(point.X * _scale, point.Y * _scale));
            }
            
            return new Polygon2D(vertices, isClosed: polyline.Closed);
        }
        
        /// <summary>
        /// 将平台无关的Polygon2D转换为AutoCAD Polyline
        /// </summary>
        public Polyline ConvertToPolyline(Polygon2D polygon)
        {
            if (polygon == null)
                throw new ArgumentNullException(nameof(polygon));
            
            var polyline = new Polyline();
            
            for (int i = 0; i < polygon.VertexCount; i++)
            {
                var vertex = polygon.Vertices[i];
                polyline.AddVertexAt(
                    i,
                    new Point2d(vertex.X / _scale, vertex.Y / _scale),
                    0, 0, 0);
            }
            
            polyline.Closed = polygon.IsClosed;
            
            return polyline;
        }
        
        /// <summary>
        /// 从AutoCAD Polyline提取标高信息
        /// </summary>
        public ElevationValue ExtractElevation(Polyline polyline)
        {
            if (polyline == null)
                throw new ArgumentNullException(nameof(polyline));
            
            // AutoCAD Polyline的Elevation属性是2D多段线的Z坐标（单位：mm）
            double elevation = polyline.Elevation * _scale;
            return ElevationValue.FromMillimeters(elevation);
        }
        
        /// <summary>
        /// 将标高应用到AutoCAD Polyline
        /// </summary>
        public void ApplyElevation(Polyline polyline, ElevationValue elevation)
        {
            if (polyline == null)
                throw new ArgumentNullException(nameof(polyline));
            if (elevation == null)
                throw new ArgumentNullException(nameof(elevation));
            
            polyline.Elevation = elevation.Value / _scale;
        }
        
        /// <summary>
        /// 从AutoCAD Line提取Line2D（2D投影）
        /// </summary>
        public Line2D ConvertToLine2D(Line line)
        {
            if (line == null)
                throw new ArgumentNullException(nameof(line));
            
            Point2D start = new Point2D(
                line.StartPoint.X * _scale,
                line.StartPoint.Y * _scale);
            
            Point2D end = new Point2D(
                line.EndPoint.X * _scale,
                line.EndPoint.Y * _scale);
            
            return new Line2D(start, end);
        }
        
        /// <summary>
        /// 将Line2D转换为AutoCAD Line（Z=0）
        /// </summary>
        public Line ConvertToLine(Line2D line2D, double z = 0.0)
        {
            if (line2D == null)
                throw new ArgumentNullException(nameof(line2D));
            
            Point3d start = new Point3d(
                line2D.StartPoint.X / _scale,
                line2D.StartPoint.Y / _scale,
                z);
            
            Point3d end = new Point3d(
                line2D.EndPoint.X / _scale,
                line2D.EndPoint.Y / _scale,
                z);
            
            return new Line(start, end);
        }
        
        /// <summary>
        /// 批量转换Polyline列表为Polygon2D列表
        /// </summary>
        public List<Polygon2D> ConvertToPolygon2DList(IEnumerable<Polyline> polylines)
        {
            var result = new List<Polygon2D>();
            
            foreach (var polyline in polylines)
            {
                result.Add(ConvertToPolygon2D(polyline));
            }
            
            return result;
        }
        
        /// <summary>
        /// 批量转换Polygon2D列表为Polyline列表
        /// </summary>
        public List<Polyline> ConvertToPolylineList(IEnumerable<Polygon2D> polygons)
        {
            var result = new List<Polyline>();
            
            foreach (var polygon in polygons)
            {
                result.Add(ConvertToPolyline(polygon));
            }
            
            return result;
        }
        
        /// <summary>
        /// 从WallData提取AutoCAD Line（墙体边线）
        /// </summary>
        public Line ExtractWallLine(WallData wallData, double z = 0.0)
        {
            if (wallData == null)
                throw new ArgumentNullException(nameof(wallData));
            
            return ConvertToLine(wallData.Edge, z);
        }
        
        /// <summary>
        /// 验证转换后的多边形是否有效
        /// </summary>
        public bool ValidateConversion(
            Polyline original,
            Polygon2D converted,
            out string errorMessage)
        {
            if (original == null || converted == null)
            {
                errorMessage = "输入为空";
                return false;
            }
            
            // 检查顶点数是否一致
            if (original.NumberOfVertices != converted.VertexCount)
            {
                errorMessage = $"顶点数不一致：原{original.NumberOfVertices}，转换后{converted.VertexCount}";
                return false;
            }
            
            // 检查闭合状态是否一致
            if (original.Closed != converted.IsClosed)
            {
                errorMessage = $"闭合状态不一致：原{original.Closed}，转换后{converted.IsClosed}";
                return false;
            }
            
            // 检查面积是否接近（允许5%误差）
            double originalArea = Math.Abs(original.Area);
            double convertedArea = converted.GetArea();
            double areaDiff = Math.Abs(originalArea - convertedArea) / originalArea;
            
            if (areaDiff > 0.05)
            {
                errorMessage = $"面积误差过大：{areaDiff * 100:F2}%";
                return false;
            }
            
            errorMessage = null;
            return true;
        }
    }
}

