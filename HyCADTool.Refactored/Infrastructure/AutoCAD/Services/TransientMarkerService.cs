using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using System;
using System.Collections.Generic;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services
{
    /// <summary>
    /// 临时标记服务 - 使用 AutoCAD 临时图形
    /// 用于 HYOV 命令标记近距离端点
    /// </summary>
    public class TransientMarkerService : IDisposable
    {
        private readonly List<Drawable> _transients = new List<Drawable>();
        private bool _disposed = false;

        /// <summary>
        /// 在指定位置创建圆形标记
        /// </summary>
        /// <param name="center">圆心位置</param>
        /// <param name="radius">半径</param>
        /// <param name="colorIndex">颜色索引（0-255）</param>
        public void AddCircleMarker(Point3d center, double radius, short colorIndex)
        {
            var circle = new Circle
            {
                Center = center,
                Radius = radius,
                ColorIndex = colorIndex
            };

            TransientManager.CurrentTransientManager.AddTransient(
                circle,
                TransientDrawingMode.Highlight,
                128,
                new IntegerCollection());

            _transients.Add(circle);
        }

        /// <summary>
        /// 创建连接线预览
        /// </summary>
        /// <param name="start">起点</param>
        /// <param name="end">终点</param>
        /// <param name="colorIndex">颜色索引（0-255）</param>
        public void AddLineMarker(Point3d start, Point3d end, short colorIndex)
        {
            var line = new Line(start, end)
            {
                ColorIndex = colorIndex
            };

            TransientManager.CurrentTransientManager.AddTransient(
                line,
                TransientDrawingMode.Highlight,
                128,
                new IntegerCollection());

            _transients.Add(line);
        }

        /// <summary>
        /// 清除所有临时标记
        /// </summary>
        public void ClearAll()
        {
            foreach (var drawable in _transients)
            {
                try
                {
                    TransientManager.CurrentTransientManager.EraseTransient(
                        drawable,
                        new IntegerCollection());
                    drawable.Dispose();
                }
                catch
                {
                    // 忽略清除错误
                }
            }
            _transients.Clear();
        }

        /// <summary>
        /// 释放资源
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                ClearAll();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }

        ~TransientMarkerService()
        {
            Dispose();
        }
    }
}




