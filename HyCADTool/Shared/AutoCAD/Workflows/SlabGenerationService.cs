using System.Collections.Generic;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using HyCADTool.Domain.Entities;
using HyCADTool.Domain.ValueObjects.Configuration.User;
using HyCADTool.Shared.AutoCAD.Configuration;
using HyCADTool.Shared.AutoCAD.Interfaces;
using HyCADTool.Shared.AutoCAD.Services;

namespace HyCADTool.Shared.AutoCAD.Workflows
{
    /// <summary>
    /// 筏板生成服务
    /// 负责从模型生成筏板实体
    /// </summary>
    public class SlabGenerationService
    {
        private readonly ISolid3DBuilder _solidBuilder;
        private readonly ILayerManager _layerManager;
        
        public SlabGenerationService(
            ISolid3DBuilder solidBuilder,
            ILayerManager layerManager)
        {
            _solidBuilder = solidBuilder;
            _layerManager = layerManager;
        }
        
        /// <summary>
        /// 生成所有筏板实体
        /// </summary>
        /// <param name="db">数据库</param>
        /// <param name="models">模型列表</param>
        /// <returns>成功创建的筏板数量</returns>
        public int GenerateSlabs(Database db, List<SurfaceBasedModel> models)
        {
            int count = 0;
            
            using (var tr = db.TransactionManager.StartTransaction())
            {
                var modelSpace = (BlockTableRecord)tr.GetObject(
                    db.CurrentSpaceId,
                    OpenMode.ForWrite);
                
                // 创建必要的图层
                string side = UserLayerNameResolver.Get(LayerSemanticIds.EquipFoundationSideSolid, LayerBuiltinDefaults.EquipSideSolid);
                string top = UserLayerNameResolver.Get(LayerSemanticIds.EquipFoundationTopSolid, LayerBuiltinDefaults.EquipTopSolid);
                string bottom = UserLayerNameResolver.Get(LayerSemanticIds.EquipFoundationBottomSolid, LayerBuiltinDefaults.EquipBottomSolid);
                _layerManager.EnsureLayers(tr, (side, 1), (top, 3), (bottom, 5));
                
                foreach (var model in models)
                {
                    try
                    {
                        var topElevation = model.TopSurface.Elevation.Value;
                        var bottomElevation = model.BottomSurface.Elevation.Value;
                        double height = topElevation - bottomElevation;
                        
                        var solid = _solidBuilder.CreateSlabSolid(
                            model.TopSurface.Polygon,
                            bottomElevation,
                            height);
                        
                        modelSpace.AppendEntity(solid);
                        tr.AddNewlyCreatedDBObject(solid, true);
                        solid.Layer = side;
                        count++;
                    }
                    catch
                    {
                        // 静默处理单个筏板创建失败
                    }
                }
                
                tr.Commit();
            }
            
            return count;
        }
    }
}


