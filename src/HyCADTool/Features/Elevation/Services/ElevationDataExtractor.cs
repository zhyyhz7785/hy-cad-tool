using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using HyCADTool.Domain.Services;
using HyCADTool.Domain.Services.Geometry;
using HyCADTool.Domain.ValueObjects;
using HyCADTool.Shared.Geometry;
using ElevationVo = HyCADTool.Domain.ValueObjects.Elevation;

namespace HyCADTool.Features.Elevation.Services
{
    /// <summary>
    /// 标高数据提取服务
    /// 负责从 AutoCAD 图纸中提取标高文本并匹配到多边形
    /// </summary>
    public class ElevationDataExtractor
    {
        private readonly Editor _editor;
        private readonly bool _silentMode;
        
        public ElevationDataExtractor(Editor editor, bool silentMode = false)
        {
            _editor = editor;
            _silentMode = silentMode;
        }
        
        /// <summary>
        /// 从 AutoCAD 图纸中提取标高数据并匹配到多边形
        /// </summary>
        /// <param name="tr">事务</param>
        /// <param name="polygons">多边形列表</param>
        /// <param name="textObjectIds">文本对象ID列表</param>
        /// <returns>字典：Polyline → Elevation</returns>
        public Dictionary<Polyline, ElevationVo> ExtractElevationData(
            Transaction tr,
            List<Polyline> polygons,
            ObjectIdCollection textObjectIds)
        {
            var result = new Dictionary<Polyline, ElevationVo>();
            
            // 1. 提取文本实体和标高数据
            var textEntities = ExtractTextEntities(tr, textObjectIds);
            
            // 2. 匹配多边形和标高（面积优先算法）
            MatchPolygonsToElevations(polygons, textEntities, result);
            
            // 3. 标记未匹配的多边形为红色
            HighlightUnmatchedPolygons(tr, polygons, result);
            
            return result;
        }
        
        /// <summary>
        /// 提取文本实体和标高数据
        /// </summary>
        private List<(string text, Point3d centerPoint)> ExtractTextEntities(
            Transaction tr,
            ObjectIdCollection textObjectIds)
        {
            var textEntities = new List<(string text, Point3d centerPoint)>();
            
            foreach (ObjectId id in textObjectIds)
            {
                var entity = tr.GetObject(id, OpenMode.ForRead);
                string text = null;
                Point3d centerPoint = Point3d.Origin;
                
                if (entity is DBText dbText)
                {
                    text = dbText.TextString;
                    // 使用几何中心点
                    var extents = dbText.GeometricExtents;
                    centerPoint = new Point3d(
                        (extents.MinPoint.X + extents.MaxPoint.X) / 2,
                        (extents.MinPoint.Y + extents.MaxPoint.Y) / 2,
                        0);
                }
                else if (entity is MText mText)
                {
                    text = mText.Text;
                    // 使用几何中心点
                    var extents = mText.GeometricExtents;
                    centerPoint = new Point3d(
                        (extents.MinPoint.X + extents.MaxPoint.X) / 2,
                        (extents.MinPoint.Y + extents.MaxPoint.Y) / 2,
                        0);
                }
                
                if (text != null)
                {
                    textEntities.Add((text, centerPoint));
                }
            }
            
            return textEntities;
        }
        
        /// <summary>
        /// 匹配多边形和标高（面积优先算法）
        /// 对于每个文本，找到所有包含它的多边形，选择面积最小的那个
        /// </summary>
        private void MatchPolygonsToElevations(
            List<Polyline> polygons,
            List<(string text, Point3d centerPoint)> textEntities,
            Dictionary<Polyline, ElevationVo> result)
        {
            foreach (var textEntity in textEntities)
            {
                var checkPoint = textEntity.centerPoint;
                
                if (!_silentMode)
                    _editor?.WriteMessage($"\n[文本匹配] 文本=\"{textEntity.text}\", 中心点=({checkPoint.X:F1}, {checkPoint.Y:F1})");
                
                // 查找所有包含该文本的多边形，选择面积最小的
                Polyline bestMatch = null;
                double minArea = double.MaxValue;
                int bestVertexCount = 0;
                
                foreach (var polyline in polygons)
                {
                    // 检查文本中心点是否在多边形内部
                    if (IsPointInsidePolyline(checkPoint, polyline))
                    {
                        double area = polyline.Area;
                        int vertexCount = polyline.NumberOfVertices;
                        
                        // 选择面积最小的多边形（内部多边形 < 外轮廓）
                        if (area < minArea)
                        {
                            minArea = area;
                            bestMatch = polyline;
                            bestVertexCount = vertexCount;
                        }
                    }
                }
                
                if (bestMatch != null)
                {
                    // 解析标高（ElevationParser 已经处理单位转换）
                    if (ElevationParser.TryParse(textEntity.text, out ElevationVo elevation))
                    {
                        result[bestMatch] = elevation;
                        
                        if (!_silentMode)
                            _editor?.WriteMessage($"\n  ✓ 匹配到多边形（顶点数={bestVertexCount}, 面积={minArea:F0}），标高={elevation.Value / 1000.0:F3}m");
                    }
                    else if (!_silentMode)
                    {
                        _editor?.WriteMessage($"\n  ✗ 解析失败：无法解析文本=\"{textEntity.text}\"");
                    }
                }
                else if (!_silentMode)
                {
                    _editor?.WriteMessage($"\n  ✗ 未匹配到任何多边形（文本中心点不在任何多边形内）");
                }
            }
        }
        
        /// <summary>
        /// 标记未匹配的多边形为红色
        /// </summary>
        private void HighlightUnmatchedPolygons(
            Transaction tr,
            List<Polyline> polygons,
            Dictionary<Polyline, ElevationVo> result)
        {
            if (!_silentMode)
                _editor?.WriteMessage("\n[多边形匹配结果]");
            
            foreach (var polyline in polygons)
            {
                if (result.ContainsKey(polyline))
                {
                    var elevation = result[polyline];
                    if (!_silentMode)
                    {
                        var center = GetPolylineCenter(polyline);
                        _editor?.WriteMessage($"\n  ✓ 多边形（顶点数={polyline.NumberOfVertices}, 中心≈({center.X:F1},{center.Y:F1})）匹配标高 {elevation.Value / 1000.0:F3}");
                    }
                }
                else
                {
                    // 未匹配的多边形标记为红色
                    polyline.UpgradeOpen();
                    polyline.ColorIndex = 1; // 红色
                    polyline.DowngradeOpen();
                    
                    if (!_silentMode)
                    {
                        var center = GetPolylineCenter(polyline);
                        _editor?.WriteMessage($"\n  ⚠️ 多边形（顶点数={polyline.NumberOfVertices}, 中心≈({center.X:F1},{center.Y:F1})）未匹配标高");
                    }
                }
            }
        }
        
        /// <summary>
        /// 判断点是否在多边形内部（使用 Domain 层工具类）
        /// </summary>
        private bool IsPointInsidePolyline(Point3d point, Polyline polyline)
        {
            if (polyline == null || !polyline.Closed)
                return false;
            
            // 转换 AutoCAD Polyline 为 Domain Polygon2D
            var vertices = new List<Point2D>();
            for (int i = 0; i < polyline.NumberOfVertices; i++)
            {
                var pt = polyline.GetPoint2dAt(i);
                vertices.Add(new Point2D(pt.X, pt.Y));
            }
            
            var polygon = new Polygon2D(vertices, isClosed: true);
            var checkPoint = new Point2D(point.X, point.Y);
            
            // 使用 Domain 层工具类
            return GeometryUtils.IsPointInsidePolygon(checkPoint, polygon);
        }
        
        /// <summary>
        /// 获取多边形中心点（用于调试输出）
        /// </summary>
        private Point3d GetPolylineCenter(Polyline polyline)
        {
            double sumX = 0, sumY = 0;
            int count = polyline.NumberOfVertices;
            
            for (int i = 0; i < count; i++)
            {
                var pt = polyline.GetPoint2dAt(i);
                sumX += pt.X;
                sumY += pt.Y;
            }
            
            return new Point3d(sumX / count, sumY / count, 0);
        }
    }
}


