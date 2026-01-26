using HyCADTool.Refactored.Domain.ValueObjects.Geometry;
using System;

namespace HyCADTool.Refactored.Domain.Interfaces
{
    /// <summary>
    /// 地理空间服务接口
    /// 负责处理地理坐标定位和TFW文件解析
    /// </summary>
    public interface IGeospatialService
    {
        /// <summary>
        /// 世界文件参数结构
        /// </summary>
        public struct WorldFileParameters
        {
            /// <summary>
            /// 像素大小在X方向（米/像素）
            /// </summary>
            public double PixelSizeX { get; set; }

            /// <summary>
            /// 旋转参数X（通常为0）
            /// </summary>
            public double RotationX { get; set; }

            /// <summary>
            /// 旋转参数Y（通常为0）
            /// </summary>
            public double RotationY { get; set; }

            /// <summary>
            /// 像素大小在Y方向（米/像素，通常为负值）
            /// </summary>
            public double PixelSizeY { get; set; }

            /// <summary>
            /// 左上角像素的X坐标（地理坐标）
            /// </summary>
            public double OriginX { get; set; }

            /// <summary>
            /// 左上角像素的Y坐标（地理坐标）
            /// </summary>
            public double OriginY { get; set; }

            /// <summary>
            /// 检查参数是否有效
            /// </summary>
            public bool IsValid => PixelSizeX != 0 && PixelSizeY != 0;
        }

        /// <summary>
        /// 从TFW文件路径解析世界文件参数
        /// </summary>
        /// <param name="tfwFilePath">TFW文件路径</param>
        /// <returns>世界文件参数</returns>
        WorldFileParameters ParseWorldFile(string tfwFilePath);

        /// <summary>
        /// 根据世界文件参数和图像尺寸计算图像边界
        /// </summary>
        /// <param name="parameters">世界文件参数</param>
        /// <param name="imageWidth">图像宽度（像素）</param>
        /// <param name="imageHeight">图像高度（像素）</param>
        /// <returns>图像边界多边形</returns>
        Polygon2D CalculateImageBoundary(WorldFileParameters parameters, int imageWidth, int imageHeight);

        /// <summary>
        /// 将像素坐标转换为地理坐标
        /// </summary>
        /// <param name="parameters">世界文件参数</param>
        /// <param name="pixelX">像素X坐标</param>
        /// <param name="pixelY">像素Y坐标</param>
        /// <returns>地理坐标点</returns>
        Point2D PixelToGeo(WorldFileParameters parameters, double pixelX, double pixelY);

        /// <summary>
        /// 将地理坐标转换为像素坐标
        /// </summary>
        /// <param name="parameters">世界文件参数</param>
        /// <param name="geoX">地理X坐标</param>
        /// <param name="geoY">地理Y坐标</param>
        /// <returns>像素坐标点</returns>
        Point2D GeoToPixel(WorldFileParameters parameters, double geoX, double geoY);
    }
}










