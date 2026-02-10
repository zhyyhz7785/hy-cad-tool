using HyCADTool.Refactored.Domain.Enums;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Domain.Models.Configuration;
using System.Collections.Generic;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services
{
    /// <summary>
    /// 基础配筋服务（桩实现）
    /// 当前为 stub：每个步骤仅输出提示，后续逐步移植旧 BaseRein 逻辑
    /// </summary>
    public class BaseReinforcementService : IBaseReinforcementService
    {
        private readonly IStyleService _styleService;
        private BaseReinforcementConfig _config;

        public BaseReinforcementService(IStyleService styleService)
        {
            _styleService = styleService;
            _config = BaseReinforcementConfig.CreateDefault();
        }

        #region 样式设置

        public void ApplyStyles(double scale)
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;

            var textStyleName = $"0_Hy_{scale}";
            var dimStyleName = $"0_Hy_{scale}_Dim";
            var mleaderStyleName = $"0_Hy_{scale}_Mleader";
            var tableStyleName = $"0_Hy_{scale}_Table";

            _styleService.CreateTextStyle(textStyleName, "tssdeng.shx", "hztxt.shx", 2.5 * scale, 0.7);
            _styleService.SetCurrentTextStyle(textStyleName);

            _styleService.CreateDimensionStyle(dimStyleName, textStyleName, scale);
            _styleService.SetCurrentDimensionStyle(dimStyleName);

            _styleService.CreateMLeaderStyle(mleaderStyleName, textStyleName, scale);
            _styleService.SetCurrentMLeaderStyle(mleaderStyleName);

            _styleService.CreateTableStyle(tableStyleName, textStyleName);
            _styleService.SetCurrentTableStyle(tableStyleName);

            ed?.WriteMessage($"\n[BaseRein] 样式已应用 (Scale={scale})");
        }

        #endregion

        #region 步骤操作

        public void OptimizeBasemap()
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage("\n[BaseRein] 步骤1：整理底图 (stub - 待移植)");
            // TODO: 移植 BaseRein.OptimizeBasemap()
        }

        public void SelectAndDeleteUnusedText(List<string> fixedValues)
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage($"\n[BaseRein] 步骤2：选择删除 (stub - 待移植), 过滤值: {string.Join(", ", fixedValues)}");
            // TODO: 移植 BaseRein.SelectNoUseText()
        }

        public void GenerateReinforcementArea(BaseReinforcementConfig config)
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage($"\n[BaseRein] 步骤4：生成配筋面积 (stub - 待移植), ProximityThreshold={config.ProximityThreshold}");
            // TODO: 移植 BaseRein.SelectFiniteElementGrid → DrawBoundingPolyline → GroupBySpatialProximity → CreateOptimizedBoundingPolygonFromPolygons
        }

        public void DrawReinforcement(BaseReinforcementConfig config, bool dimAll)
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage($"\n[BaseRein] 步骤5：绘制钢筋 (stub - 待移植), DimAll={dimAll}, Direction={config.Direction}");
            // TODO: 移植 BaseRein.ReinforcementStepA → ReinforcementStepB / ReinforcementAll
        }

        public void DimensionReinforcementArea(IntersectionsDirection direction)
        {
            var ed = AcApp.DocumentManager.MdiActiveDocument?.Editor;
            ed?.WriteMessage($"\n[BaseRein] 步骤6：标注配筋区域 (stub - 待移植), Direction={direction}");
            // TODO: 移植 BaseRein.Poly4Dim()
        }

        #endregion

        #region 配置管理

        public BaseReinforcementConfig GetCurrentConfig()
        {
            return _config.Clone();
        }

        public void SaveConfig(BaseReinforcementConfig config)
        {
            _config = config.Clone();
        }

        public BaseReinforcementConfig ResetToDefault()
        {
            _config = BaseReinforcementConfig.CreateDefault();
            return _config.Clone();
        }

        #endregion
    }
}
