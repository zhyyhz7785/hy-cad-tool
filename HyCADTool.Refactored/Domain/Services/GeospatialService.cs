using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using System;
using System.IO;

namespace HyCADTool.Refactored.Domain.Services
{
    /// <summary>
    /// 地理空间服务实现
    /// 负责处理地理坐标定位和TFW文件解析
    /// </summary>
    public class GeospatialService : IGeospatialService
    {
        /// <summary>
        /// 从TFW文件路径解析世界文件参数
        /// TFW文件格式：
        /// Line 1: Pixel size in X direction (dx)
        /// Line 2: Rotation parameter (usually 0)
        /// Line 3: Rotation parameter (usually 0)
        /// Line 4: Pixel size in Y direction (dy, usually negative)
        /// Line 5: X coordinate of upper left pixel
        /// Line 6: Y coordinate of upper left pixel
        /// </summary>
        /// <param name="tfwFilePath">TFW文件路径</param>
        /// <returns>世界文件参数</returns>
        /// <exception cref="FileNotFoundException">TFW文件不存在</exception>
        /// <exception cref="FormatException">TFW文件格式错误</exception>
        public IGeospatialService.WorldFileParameters ParseWorldFile(string tfwFilePath)
        {
            if (!File.Exists(tfwFilePath))
            {
                throw new FileNotFoundException($"TFW文件不存在: {tfwFilePath}");
            }

            var lines = File.ReadAllLines(tfwFilePath);
            if (lines.Length < 6)
            {
                throw new FormatException($"TFW文件格式错误: 需要6行参数，实际{lines.Length}行");
            }

            try
            {
                return new IGeospatialService.WorldFileParameters
                {
                    PixelSizeX = double.Parse(lines[0].Trim()),
                    RotationX = double.Parse(lines[1].Trim()),
                    RotationY = double.Parse(lines[2].Trim()),
                    PixelSizeY = double.Parse(lines[3].Trim()),
                    OriginX = double.Parse(lines[4].Trim()),
                    OriginY = double.Parse(lines[5].Trim())
                };
            }
            catch (Exception ex)
            {
                throw new FormatException($"TFW文件参数解析失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 根据世界文件参数和图像尺寸计算图像边界
        /// </summary>
        /// <param name="parameters">世界文件参数</param>
        /// <param name="imageWidth">图像宽度（像素）</param>
        /// <param name="imageHeight">图像高度（像素）</param>
        /// <returns>图像边界多边形</returns>
        public Polygon2D CalculateImageBoundary(IGeospatialService.WorldFileParameters parameters, int imageWidth, int imageHeight)
        {
            if (!parameters.IsValid)
            {
                throw new ArgumentException("世界文件参数无效");
            }

            // 计算四个角点的地理坐标
            var topLeft = new Point2D(parameters.OriginX, parameters.OriginY);
            var topRight = PixelToGeo(parameters, imageWidth, 0);
            var bottomRight = PixelToGeo(parameters, imageWidth, imageHeight);
            var bottomLeft = PixelToGeo(parameters, 0, imageHeight);

            return new Polygon2D(new[] { topLeft, topRight, bottomRight, bottomLeft });
        }

        /// <summary>
        /// 将像素坐标转换为地理坐标
        /// 使用仿射变换公式：
        /// X_geo = OriginX + PixelX * PixelSizeX + PixelY * RotationX
        /// Y_geo = OriginY + PixelX * RotationY + PixelY * PixelSizeY
        /// </summary>
        /// <param name="parameters">世界文件参数</param>
        /// <param name="pixelX">像素X坐标</param>
        /// <param name="pixelY">像素Y坐标</param>
        /// <returns>地理坐标点</returns>
        public Point2D PixelToGeo(IGeospatialService.WorldFileParameters parameters, double pixelX, double pixelY)
        {
            if (!parameters.IsValid)
            {
                throw new ArgumentException("世界文件参数无效");
            }

            double geoX = parameters.OriginX + pixelX * parameters.PixelSizeX + pixelY * parameters.RotationX;
            double geoY = parameters.OriginY + pixelX * parameters.RotationY + pixelY * parameters.PixelSizeY;

            return new Point2D(geoX, geoY);
        }

        /// <summary>
        /// 将地理坐标转换为像素坐标
        /// 使用仿射变换逆公式
        /// </summary>
        /// <param name="parameters">世界文件参数</param>
        /// <param name="geoX">地理X坐标</param>
        /// <param name="geoY">地理Y坐标</param>
        /// <returns>像素坐标点</returns>
        public Point2D GeoToPixel(IGeospatialService.WorldFileParameters parameters, double geoX, double geoY)
        {
            if (!parameters.IsValid)
            {
                throw new ArgumentException("世界文件参数无效");
            }

            // 计算行列式
            double det = parameters.PixelSizeX * parameters.PixelSizeY - parameters.RotationX * parameters.RotationY;
            if (Math.Abs(det) < 1e-10)
            {
                throw new InvalidOperationException("变换矩阵不可逆");
            }

            // 计算逆变换
            double pixelX = ((geoX - parameters.OriginX) * parameters.PixelSizeY - (geoY - parameters.OriginY) * parameters.RotationX) / det;
            double pixelY = ((geoY - parameters.OriginY) * parameters.PixelSizeX - (geoX - parameters.OriginX) * parameters.RotationY) / det;

            return new Point2D(pixelX, pixelY);
        }
    }
}










