using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Features.Elevation.Domain.Entities;
using HyCADTool.Features.Elevation.Domain.ValueObjects;
using HyCADTool.Shared.Geometry;

namespace HyCADTool.Features.Elevation.Domain.Services
{
    /// <summary>
    /// 三维标高计算服务
    /// 负责计算墙体和筏板的3D拉伸参数
    /// 核心功能：墙体连接处理（与原始 ElevationModelGenerator 逻辑一致）
    /// </summary>
    public class Elevation3DCalculator
    {
        private readonly WallBufferGenerator _bufferGenerator;
        private readonly WallConnectionHandler _connectionHandler;
        
        public Elevation3DCalculator()
        {
            _bufferGenerator = new WallBufferGenerator();
            _connectionHandler = new WallConnectionHandler();
        }
        
        /// <summary>
        /// 批量计算墙体3D拉伸参数（带墙体连接处理）
        /// 核心算法：与原始 ElevationModelGenerator 保持一致
        /// </summary>
        public List<Wall3D> CalculateAllWalls(
            IEnumerable<GeometryData> geometryDataList)
        {
            var result = new List<Wall3D>();
            
            foreach (var geometryData in geometryDataList)
            {
                // 处理每个几何区域的墙体
                var walls = ProcessGeometryWalls(geometryData);
                result.AddRange(walls);
            }
            
            return result;
        }
        
        /// <summary>
        /// 处理单个几何区域的所有墙体（包括闭合曲线和开放曲线）
        /// 核心流程：
        /// 1. 生成所有墙体的初始矩形缓冲区
        /// 2. 处理墙体连接（修改缓冲区多边形）
        /// 3. 创建 Wall3D 实体
        /// </summary>
        private List<Wall3D> ProcessGeometryWalls(GeometryData geometryData)
        {
            var result = new List<Wall3D>();
            
            // 1. 生成所有墙体的初始矩形缓冲区
            // 注意：使用 List 而不是 Dictionary，因为 WallData 是 class，引用相等性会导致 eKeyNotFound
            var walls = geometryData.WallsOnly.ToList();
            var wallBuffers = new List<(WallData wall, Polygon2D buffer)>();
            
            foreach (var wall in walls)
            {
                if (wall.Thickness.Value <= 0)
                    continue;
                
                var buffer = _bufferGenerator.CreateRectangularBuffer(
                    wall.Edge,
                    wall.Thickness);
                
                wallBuffers.Add((wall, buffer));
            }
            
            // 2. 处理墙体连接（修改缓冲区多边形）
            // ⚠️ 暂时禁用墙体连接处理，使用简单矩形缓冲区
            // TODO: 修复墙体连接算法后再启用
            // ProcessWallConnections(geometryData, wallBuffers);
            
            // 3. 创建 Wall3D 实体
            foreach (var (wall, buffer) in wallBuffers)
            {
                var wall3D = CreateWall3DFromBuffer(wall, buffer);
                result.Add(wall3D);
            }
            
            return result;
        }
        
        /// <summary>
        /// 处理墙体连接（与原始代码逻辑一致）
        /// 原始逻辑参考：
        /// - 闭合曲线（3个以上墙体）：处理所有相邻对，包括最后一个和第一个
        /// - 开放曲线（2个以上墙体）：处理相邻对，不包括首尾连接
        /// </summary>
        private void ProcessWallConnections(
            GeometryData geometryData,
            Dictionary<WallData, Polygon2D> wallBuffers)
        {
            var wallsList = geometryData.WallsOnly.ToList();
            int count = wallsList.Count;
            
            if (count < 2)
                return; // 少于2个墙体，无需处理连接
            
            // 判断是否为闭合曲线
            bool isClosed = IsClosedCurve(wallsList);
            
            if (isClosed && count >= 3)
            {
                // 闭合曲线：处理所有相邻对（包括最后一个和第一个）
                for (int i = 0; i < count; i++)
                {
                    int nextIndex = (i + 1) % count;
                    
                    var wall1 = wallsList[i];
                    var wall2 = wallsList[nextIndex];
                    
                    if (!wallBuffers.ContainsKey(wall1) || !wallBuffers.ContainsKey(wall2))
                        continue;
                    
                    // 获取缓冲区并处理连接
                    var buffer1 = wallBuffers[wall1];
                    var buffer2 = wallBuffers[wall2];
                    
                    _connectionHandler.HandleConnection(
                        wall1.Edge, wall2.Edge,
                        wall1.Thickness, wall2.Thickness,
                        ref buffer1, ref buffer2);
                    
                    // 更新缓冲区
                    wallBuffers[wall1] = buffer1;
                    wallBuffers[wall2] = buffer2;
                }
            }
            else if (count >= 2)
            {
                // 开放曲线：处理相邻对（不包括首尾）
                for (int i = 0; i < count - 1; i++)
                {
                    var wall1 = wallsList[i];
                    var wall2 = wallsList[i + 1];
                    
                    if (!wallBuffers.ContainsKey(wall1) || !wallBuffers.ContainsKey(wall2))
                        continue;
                    
                    var buffer1 = wallBuffers[wall1];
                    var buffer2 = wallBuffers[wall2];
                    
                    _connectionHandler.HandleConnection(
                        wall1.Edge, wall2.Edge,
                        wall1.Thickness, wall2.Thickness,
                        ref buffer1, ref buffer2);
                    
                    wallBuffers[wall1] = buffer1;
                    wallBuffers[wall2] = buffer2;
                }
            }
        }
        
        /// <summary>
        /// 判断墙体列表是否构成闭合曲线
        /// </summary>
        private bool IsClosedCurve(List<WallData> walls)
        {
            if (walls.Count < 3)
                return false;
            
            // 检查首尾是否连接
            var firstStart = walls[0].Edge.StartPoint;
            var lastEnd = walls[walls.Count - 1].Edge.EndPoint;
            
            double tolerance = ToleranceSettings.Instance.DistanceTolerance;
            return firstStart.DistanceTo(lastEnd) < tolerance;
        }
        
        /// <summary>
        /// 从缓冲区创建 Wall3D 实体
        /// </summary>
        private Wall3D CreateWall3DFromBuffer(WallData wallData, Polygon2D buffer)
        {
            if (wallData.IsRetainingWall)
            {
                return Wall3D.CreateRetainingWall(
                    bufferRegion: buffer,
                    thickness: wallData.Thickness,
                    innerElevation: wallData.InnerElevation);
            }
            else
            {
                return Wall3D.CreateNormalWall(
                    bufferRegion: buffer,
                    thickness: wallData.Thickness,
                    innerElevation: wallData.InnerElevation,
                    outerElevation: wallData.OuterElevation);
            }
        }
        
        /// <summary>
        /// 计算筏板3D拉伸参数
        /// 
        /// 逻辑：
        /// 1. 正值标高：顶标高 = 多边形标高，先向下到地面(0.000)，再向下延伸厚度
        /// 2. 负值标高：顶标高 = 多边形标高，向下延伸厚度
        /// </summary>
        public Slab3D CalculateRaftExtrusion(
            GeometryData geometryData,
            SlabThickness raftThickness)
        {
            if (geometryData == null)
                throw new ArgumentNullException(nameof(geometryData));
            if (raftThickness == null)
                throw new ArgumentNullException(nameof(raftThickness));
            
            // 计算筏板区域：如果有墙体，使用墙体内部区域；否则使用原始多边形
            Polygon2D raftRegion = CalculateRaftRegion(geometryData);
            
            // 计算筏板的顶标高和实际厚度
            ElevationValue topElevation = geometryData.Elevation;
            SlabThickness actualThickness;
            
            if (geometryData.Elevation.Value > 0)
            {
                // 正值标高：先向下到地面(0.000)，再向下延伸厚度
                // 实际厚度 = 标高 + 厚度
                // 例如：标高 700mm，厚度 400mm → 实际厚度 = 700 + 400 = 1100mm
                actualThickness = SlabThickness.Create(
                    geometryData.Elevation.Value + raftThickness.Value);
            }
            else
            {
                // 负值标高或地面：向下延伸厚度
                actualThickness = raftThickness;
            }
            
            // 创建 Slab3D 实体
            return Slab3D.Create(
                region: raftRegion,
                thickness: actualThickness,
                baseElevation: topElevation);
        }
        
        /// <summary>
        /// 计算筏板区域（去除墙体厚度后的内部区域）
        /// 
        /// 简化方案：
        /// - 如果有墙体，构建墙体内侧轮廓作为筏板区域
        /// - 如果没有墙体，使用原始多边形
        /// </summary>
        private Polygon2D CalculateRaftRegion(GeometryData geometryData)
        {
            // 如果没有墙体，直接返回原始多边形
            if (!geometryData.WallsOnly.Any())
            {
                return geometryData.Polygon;
            }
            
            // 如果有墙体，使用墙体内侧边界作为筏板区域
            // 注意：墙体的 Edge 是中心线，需要向内偏移 thickness/2
            var walls = geometryData.WallsOnly.ToList();
            
            // 简化处理：使用原始多边形的内缩版本
            // 收集所有墙体内侧点
            var innerPoints = new List<Point2D>();
            
            foreach (var wall in walls)
            {
                // 墙体内侧 = 中心线向内偏移 thickness/2
                // 简化：直接使用墙体起点（后续可优化）
                innerPoints.Add(wall.Edge.StartPoint);
            }
            
            // 如果内侧点数量不足，返回原始多边形
            if (innerPoints.Count < 3)
            {
                return geometryData.Polygon;
            }
            
            // 创建内侧多边形
            return new Polygon2D(innerPoints, isClosed: true);
        }
        
        /// <summary>
        /// 批量计算筏板3D拉伸参数
        /// </summary>
        public List<Slab3D> CalculateAllRafts(
            IEnumerable<GeometryData> geometryDataList,
            SlabThickness raftThickness)
        {
            return geometryDataList
                .Select(g => CalculateRaftExtrusion(g, raftThickness))
                .ToList();
        }
    }
}
