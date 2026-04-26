using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Domain.ValueObjects;
using HyCADTool.Shared.Geometry;

namespace HyCADTool.Domain.Services
{
    /// <summary>
    /// 墙体缓冲区生成服务
    /// 负责为墙体边线生成缓冲区多边形
    /// 支持矩形、圆角、变厚度等多种缓冲区类型
    /// </summary>
    public class WallBufferGenerator
    {
        /// <summary>
        /// 创建矩形缓冲区（基础版本，保持原逻辑）
        /// 算法：沿墙体法向量（逆时针旋转90度）偏移 thickness
        /// </summary>
        /// <param name="wall">墙体边线</param>
        /// <param name="thickness">墙体厚度</param>
        /// <returns>4顶点封闭多边形</returns>
        public Polygon2D CreateRectangularBuffer(Line2D wall, WallThickness thickness)
        {
            if (wall == null)
                throw new ArgumentNullException(nameof(wall));
            if (thickness == null)
                throw new ArgumentNullException(nameof(thickness));
            
            Point2D start = wall.StartPoint;
            Point2D end = wall.EndPoint;
            
            // 计算墙体方向向量
            Vector2D direction = start.VectorTo(end);
            
            // 计算法向量（逆时针旋转90度）
            // 注意：GetNormal() 已经旋转了90度，不需要再旋转
            Vector2D normal = direction.Normalize().GetNormal();
            
            // 计算4个顶点
            // 顶点顺序：[外侧起点, 内侧起点, 内侧终点, 外侧终点]
            Point2D p1 = start.Add(normal.Scale(thickness.Value));  // 外侧起点
            Point2D p2 = start;                                      // 内侧起点
            Point2D p3 = end;                                        // 内侧终点
            Point2D p4 = end.Add(normal.Scale(thickness.Value));    // 外侧终点
            
            // 创建闭合多边形
            var vertices = new List<Point2D> { p1, p2, p3, p4 };
            return new Polygon2D(vertices, isClosed: true);
        }
        
        /// <summary>
        /// 创建圆角缓冲区（优化版本）
        /// 在墙体端点处添加圆弧，使缓冲区更平滑
        /// </summary>
        /// <param name="wall">墙体边线</param>
        /// <param name="thickness">墙体厚度</param>
        /// <param name="arcSegments">圆弧段数（默认从 ToleranceSettings 获取）</param>
        /// <returns>带圆角的多边形</returns>
        public Polygon2D CreateRoundedBuffer(
            Line2D wall, 
            WallThickness thickness, 
            int? arcSegments = null)
        {
            if (wall == null)
                throw new ArgumentNullException(nameof(wall));
            if (thickness == null)
                throw new ArgumentNullException(nameof(thickness));
            
            int segments = arcSegments ?? ToleranceSettings.Instance.RoundedBufferSegments;
            
            Point2D start = wall.StartPoint;
            Point2D end = wall.EndPoint;
            
            Vector2D direction = start.VectorTo(end);
            Vector2D normal = direction.Normalize().GetNormal();
            
            var vertices = new List<Point2D>();
            
            // 起点圆弧（外侧半圆）
            Point2D outerStart = start.Add(normal.Scale(thickness.Value));
            AddSemicircleVertices(vertices, start, outerStart, start, segments);
            
            // 外侧边
            Point2D outerEnd = end.Add(normal.Scale(thickness.Value));
            vertices.Add(outerEnd);
            
            // 终点圆弧（外侧半圆）
            AddSemicircleVertices(vertices, end, outerEnd, end, segments);
            
            // 内侧边（回到起点）
            vertices.Add(start);
            
            return new Polygon2D(vertices, isClosed: true);
        }
        
        /// <summary>
        /// 创建变厚度缓冲区（高级版本）
        /// 墙体厚度从起点到终点线性渐变
        /// </summary>
        /// <param name="wall">墙体边线</param>
        /// <param name="startThickness">起点厚度</param>
        /// <param name="endThickness">终点厚度</param>
        /// <returns>梯形缓冲区</returns>
        public Polygon2D CreateTaperedBuffer(
            Line2D wall,
            WallThickness startThickness,
            WallThickness endThickness)
        {
            if (wall == null)
                throw new ArgumentNullException(nameof(wall));
            if (startThickness == null)
                throw new ArgumentNullException(nameof(startThickness));
            if (endThickness == null)
                throw new ArgumentNullException(nameof(endThickness));
            
            Point2D start = wall.StartPoint;
            Point2D end = wall.EndPoint;
            
            Vector2D direction = start.VectorTo(end);
            Vector2D normal = direction.GetNormal().RotateBy(Math.PI / 2);
            
            // 计算4个顶点（梯形）
            Point2D p1 = start.Add(normal.Scale(startThickness.Value));  // 外侧起点
            Point2D p2 = start;                                           // 内侧起点
            Point2D p3 = end;                                             // 内侧终点
            Point2D p4 = end.Add(normal.Scale(endThickness.Value));      // 外侧终点
            
            var vertices = new List<Point2D> { p1, p2, p3, p4 };
            return new Polygon2D(vertices, isClosed: true);
        }
        
        /// <summary>
        /// 批量创建缓冲区并自动处理连接
        /// 为多条墙体边生成缓冲区，并在连接处进行优化
        /// </summary>
        /// <param name="walls">墙体数据列表</param>
        /// <param name="bufferType">缓冲区类型</param>
        /// <returns>缓冲区列表</returns>
        public List<Polygon2D> CreateConnectedBuffers(
            IEnumerable<WallData> walls,
            BufferType bufferType = BufferType.Rectangular)
        {
            var buffers = new List<Polygon2D>();
            
            foreach (var wall in walls)
            {
                Polygon2D buffer = bufferType switch
                {
                    BufferType.Rectangular => CreateRectangularBuffer(wall.Edge, wall.Thickness),
                    BufferType.Rounded => CreateRoundedBuffer(wall.Edge, wall.Thickness),
                    BufferType.Tapered => CreateRectangularBuffer(wall.Edge, wall.Thickness), // 默认矩形
                    _ => CreateRectangularBuffer(wall.Edge, wall.Thickness)
                };
                
                buffers.Add(buffer);
            }
            
            return buffers;
        }
        
        /// <summary>
        /// 添加半圆弧顶点
        /// </summary>
        private void AddSemicircleVertices(
            List<Point2D> vertices,
            Point2D center,
            Point2D startPoint,
            Point2D endPoint,
            int segments)
        {
            double radius = center.DistanceTo(startPoint);
            
            // 计算起始角度和终止角度
            double startAngle = Math.Atan2(
                startPoint.Y - center.Y,
                startPoint.X - center.X);
            double endAngle = Math.Atan2(
                endPoint.Y - center.Y,
                endPoint.X - center.X);
            
            // 确保逆时针方向
            if (endAngle < startAngle)
                endAngle += 2 * Math.PI;
            
            // 生成圆弧顶点
            for (int i = 0; i <= segments; i++)
            {
                double t = (double)i / segments;
                double angle = startAngle + (endAngle - startAngle) * t;
                
                double x = center.X + radius * Math.Cos(angle);
                double y = center.Y + radius * Math.Sin(angle);
                
                vertices.Add(new Point2D(x, y));
            }
        }
        
        /// <summary>
        /// 处理端点（开放曲线）
        /// 根据端点类型生成不同的缓冲区
        /// </summary>
        public Polygon2D HandleEndpoint(
            WallData wall,
            bool isStart,
            EndType endType)
        {
            Point2D p1 = isStart ? wall.Edge.StartPoint : wall.Edge.EndPoint;
            Point2D p2 = isStart ? wall.Edge.EndPoint : wall.Edge.StartPoint;
            
            Vector2D direction = p1.VectorTo(p2);
            Vector2D normal = direction.GetNormal().RotateBy(Math.PI / 2);
            double thickness = wall.Thickness.Value;
            
            switch (endType)
            {
                case EndType.Butt:
                    // 不延伸，直接生成矩形缓冲区
                    return CreateRectangularBuffer(wall.Edge, wall.Thickness);
                
                case EndType.Square:
                    // 端点延伸厚度的一半
                    double extendLength = thickness / 2;
                    Vector2D extendVector = direction.GetNormal().Scale(extendLength);
                    
                    Point2D p11 = isStart ? p1.Add(extendVector.Negate()) : p1;
                    Point2D p14 = isStart ? p2 : p2.Add(extendVector);
                    Point2D p12 = p11.Add(normal.Scale(thickness));
                    Point2D p13 = p14.Add(normal.Scale(thickness));
                    
                    var squareVertices = new List<Point2D> { p11, p12, p13, p14 };
                    return new Polygon2D(squareVertices, isClosed: true);
                
                case EndType.Round:
                    // 圆形端点（使用圆角缓冲区）
                    return CreateRoundedBuffer(wall.Edge, wall.Thickness);
                
                case EndType.Polygon:
                default:
                    // 默认按矩形缓冲区生成
                    return CreateRectangularBuffer(wall.Edge, wall.Thickness);
            }
        }
        
        /// <summary>
        /// 验证缓冲区是否有效
        /// </summary>
        public bool ValidateBuffer(Polygon2D buffer, out string errorMessage)
        {
            if (buffer == null)
            {
                errorMessage = "缓冲区为空";
                return false;
            }
            
            if (!buffer.IsClosed)
            {
                errorMessage = "缓冲区必须是闭合多边形";
                return false;
            }
            
            if (buffer.VertexCount < 3)
            {
                errorMessage = $"缓冲区顶点数不足：{buffer.VertexCount}";
                return false;
            }
            
            double area = buffer.GetArea();
            if (area < 1.0) // 至少 1mm²
            {
                errorMessage = $"缓冲区面积过小：{area:F2}mm²";
                return false;
            }
            
            errorMessage = null;
            return true;
        }
    }
    
    /// <summary>
    /// 缓冲区类型
    /// </summary>
    public enum BufferType
    {
        /// <summary>
        /// 矩形缓冲区（基础版本）
        /// </summary>
        Rectangular,
        
        /// <summary>
        /// 圆角缓冲区（优化版本）
        /// </summary>
        Rounded,
        
        /// <summary>
        /// 变厚度缓冲区（高级版本）
        /// </summary>
        Tapered
    }
}



