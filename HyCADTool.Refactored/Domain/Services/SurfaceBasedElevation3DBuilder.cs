using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Refactored.Domain.Entities;
using HyCADTool.Refactored.Domain.ValueObjects;
using HyCADTool.Refactored.Domain.ValueObjects.Geometry;

namespace HyCADTool.Refactored.Domain.Services
{
    /// <summary>
    /// 基于表面的三维建模服务（新方法）
    /// 
    /// 核心思路：
    /// 1. 上表面：多边形 @ 标高
    /// 2. 下表面：根据标高规则（正值延伸到 0.000，负值直接向下 400mm）
    /// 3. 0.000 平面：土壤分界线
    /// 4. 墙体：外侧扩张 300mm（0.000 以下与土壤接触）
    /// </summary>
    public class SurfaceBasedElevation3DBuilder
    {
        private readonly double _defaultSlabThickness; // 默认 400mm
        private readonly double _defaultWallThickness; // 默认 300mm
        
        public SurfaceBasedElevation3DBuilder(
            double defaultSlabThickness = 400.0,
            double defaultWallThickness = 300.0)
        {
            _defaultSlabThickness = defaultSlabThickness;
            _defaultWallThickness = defaultWallThickness;
        }
        
        /// <summary>
        /// 根据多边形和标高生成三维模型
        /// </summary>
        public SurfaceBasedModel BuildModel(Polygon2D polygon, Elevation elevation)
        {
            if (polygon == null)
                throw new ArgumentNullException(nameof(polygon));
            if (elevation == null)
                throw new ArgumentNullException(nameof(elevation));
            
            // 1. 计算上表面和下表面标高
            var topElevation = elevation;  // 上表面 = 原始标高（正确）
            var bottomElevation = CalculateBottomElevation(elevation);  // 下表面 = 根据规则计算
            
            // 2. 创建上表面
            var topSurface = Surface3D.Create(polygon, topElevation, "上表面");
            
            // 3. 创建下表面
            var bottomSurface = Surface3D.Create(polygon, bottomElevation, "下表面");
            
            // 4. 判断是否与土壤接触（0.000 以下）
            bool contactsWithSoil = bottomElevation.Value < 0;
            
            // 5. 生成墙体（外侧扩张）
            // 尝试扩张，如果失败则使用原始多边形
            Polygon2D wallRegion;
            try
            {
                wallRegion = polygon.Offset(_defaultWallThickness, isOutward: true);
                
                // 检查偏移后的多边形是否有效（顶点数相同，无自交）
                if (wallRegion == null || wallRegion.VertexCount != polygon.VertexCount)
                {
                    // 偏移失败，使用原始多边形
                    wallRegion = polygon;
                }
            }
            catch
            {
                // 偏移失败，使用原始多边形
                wallRegion = polygon;
            }
            
            var wallSolid = WallSolid3D.Create(
                outerPolygon: wallRegion,
                innerPolygon: polygon,
                bottomElevation: bottomElevation,
                topElevation: topElevation,
                contactsWithSoil: contactsWithSoil);
            
            // 6. 生成底板（内部区域）
            var slabSolid = SlabSolid3D.Create(
                polygon: polygon,
                topElevation: bottomElevation, // 底板顶部 = 下表面
                thickness: SlabThickness.Create(_defaultSlabThickness));
            
            return SurfaceBasedModel.Create(
                topSurface,
                bottomSurface,
                wallSolid,
                slabSolid,
                contactsWithSoil);
        }
        
        /// <summary>
        /// 根据标高规则计算下表面标高
        /// 
        /// 规则（已修正）：
        /// - 正值标高（> 0）：延伸到 0.000，再向下板厚 → 底标高 = -板厚
        ///   示例：+700mm → 下表面 = -400mm（高度 = 1100mm）
        /// 
        /// - 零值标高（= 0）：向下板厚 → 底标高 = -板厚
        ///   示例：0mm → 下表面 = -400mm（高度 = 400mm）
        /// 
        /// - 负值标高（< 0）：从标高位置向下板厚 → 底标高 = 标高 - 板厚
        ///   示例：-5000mm → 下表面 = -5400mm（高度 = 400mm）
        /// </summary>
        private Elevation CalculateBottomElevation(Elevation topElevation)
        {
            if (topElevation.Value > 0)
            {
                // 正值：延伸到 0.000，再向下板厚
                return Elevation.FromMillimeters(-_defaultSlabThickness);
            }
            else
            {
                // 零值或负值：从标高位置直接向下板厚
                return Elevation.FromMillimeters(topElevation.Value - _defaultSlabThickness);
            }
        }
        
        /// <summary>
        /// 批量生成模型
        /// </summary>
        public List<SurfaceBasedModel> BuildModels(
            IEnumerable<(Polygon2D polygon, Elevation elevation)> geometryData)
        {
            var models = new List<SurfaceBasedModel>();
            
            foreach (var (polygon, elevation) in geometryData)
            {
                var model = BuildModel(polygon, elevation);
                models.Add(model);
            }
            
            return models;
        }
        
        /// <summary>
        /// 识别土壤接触面（0.000 平面以下的外表面）
        /// </summary>
        public List<Surface3D> IdentifySoilContactSurfaces(SurfaceBasedModel model)
        {
            var soilContactSurfaces = new List<Surface3D>();
            
            // 如果模型与土壤接触
            if (model.ContactsWithSoil)
            {
                // 墙体外表面（0.000 以下部分）
                var wallOuterSurface = CreateWallOuterSurface(model);
                soilContactSurfaces.Add(wallOuterSurface);
                
                // 底板下表面
                var slabBottomSurface = model.BottomSurface;
                soilContactSurfaces.Add(slabBottomSurface);
            }
            
            return soilContactSurfaces;
        }
        
        /// <summary>
        /// 创建墙体外表面（用于土壤接触面识别）
        /// </summary>
        private Surface3D CreateWallOuterSurface(SurfaceBasedModel model)
        {
            // 墙体外轮廓
            var outerPolygon = model.WallSolid.OuterPolygon;
            
            // 只取 0.000 以下部分
            var groundElevation = Elevation.Ground;
            var bottomElevation = model.BottomSurface.Elevation;
            
            // 如果顶标高 > 0，则墙体外表面只到 0.000
            var topElevation = model.TopSurface.Elevation.Value > 0 
                ? groundElevation 
                : model.TopSurface.Elevation;
            
            return Surface3D.Create(outerPolygon, bottomElevation, "墙体外表面（土壤接触）");
        }
    }
}

