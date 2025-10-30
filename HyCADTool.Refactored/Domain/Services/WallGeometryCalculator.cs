using System;
using HyCADTool.Refactored.Domain.ValueObjects;
using HyCADTool.Refactored.Domain.Services;

namespace HyCADTool.Refactored.Domain.Services
{
    /// <summary>
    /// 墙体几何计算服务（Domain 层 - 平台无关）
    /// 负责计算墙体的高度、偏移方向等几何参数
    /// </summary>
    public class WallGeometryCalculator
    {
        private readonly SlabThickness _defaultSlabThickness;
        
        public WallGeometryCalculator(SlabThickness defaultSlabThickness = null)
        {
            _defaultSlabThickness = defaultSlabThickness ?? SlabThickness.Create(400.0);
        }
        
        /// <summary>
        /// 计算连接墙体的几何参数
        /// 连接墙体：连接两个不同标高的基础
        /// </summary>
        /// <param name="currentElevation">当前多边形标高</param>
        /// <param name="adjacentElevation">相邻多边形标高</param>
        /// <returns>(底标高, 顶标高, 偏移方向)</returns>
        public (double bottom, double top, int offsetDirection) CalculateConnectingWall(
            Elevation currentElevation,
            Elevation adjacentElevation)
        {
            double currentValue = currentElevation.Value;
            double adjacentValue = adjacentElevation.Value;
            
            // 底标高：两者中较低的标高 - 筏板厚度
            double bottom = Math.Min(currentValue, adjacentValue) - _defaultSlabThickness.Value;
            
            // 顶标高：两者中较高的标高 - 筏板厚度（避免与筏板重叠）
            double higherElevation = Math.Max(currentValue, adjacentValue);
            double top = higherElevation - _defaultSlabThickness.Value;
            
            // 偏移方向：向较高标高的多边形偏移
            // -1 = 向内（当前标高更高），+1 = 向外（相邻标高更高）
            int offsetDirection = (currentValue > adjacentValue) ? -1 : 1;
            
            return (bottom, top, offsetDirection);
        }
        
        /// <summary>
        /// 计算挡土墙的几何参数
        /// 挡土墙：基础边缘与土壤接触的墙体
        /// </summary>
        /// <param name="baseElevation">基础标高</param>
        /// <param name="bottomElevation">基础底标高</param>
        /// <returns>(底标高, 顶标高, 偏移方向)</returns>
        public (double bottom, double top, int offsetDirection) CalculateRetainingWall(
            Elevation baseElevation,
            double bottomElevation)
        {
            // 底标高：基础底面
            double bottom = bottomElevation;
            
            // 顶标高：0.000（地面）
            double top = 0.0;
            
            // 偏移方向：挡土墙总是向外偏移
            int offsetDirection = 1;
            
            return (bottom, top, offsetDirection);
        }
        
        /// <summary>
        /// 计算墙体高度
        /// </summary>
        public double CalculateWallHeight(double bottomElevation, double topElevation)
        {
            return topElevation - bottomElevation;
        }
        
        /// <summary>
        /// 判断是否需要生成挡土墙
        /// 只有标高低于0的基础才需要挡土墙
        /// </summary>
        public bool NeedsRetainingWall(Elevation elevation)
        {
            return elevation.Value < 0;
        }
    }
}


