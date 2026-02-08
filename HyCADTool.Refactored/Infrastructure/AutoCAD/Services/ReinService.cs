using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Domain.Services;
using HyCADTool.Refactored.Domain.ValueObjects;
using System;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Services
{
    /// <summary>
    /// 钢筋服务实现
    /// 负责钢筋绘制、标注等核心功能的 AutoCAD 平台实现
    /// </summary>
    public class ReinService : IReinService
    {
        private readonly ILayerService _layerService;
        private readonly IStyleService _styleService;

        /// <summary>
        /// 默认文字样式名称
        /// </summary>
        private const string DefaultTextStyleName = "0_Hy_40";

        /// <summary>
        /// 默认标注样式名称
        /// </summary>
        private const string DefaultDimStyleName = "0_Hy_40_Dim";

        /// <summary>
        /// 默认多重引线样式名称
        /// </summary>
        private const string DefaultMLeaderStyleName = "0_Hy_40_MLeader";

        /// <summary>
        /// 默认表格样式名称
        /// </summary>
        private const string DefaultTableStyleName = "0_Hy_40_Table";

        public ReinService(ILayerService layerService, IStyleService styleService)
        {
            _layerService = layerService ?? throw new ArgumentNullException(nameof(layerService));
            _styleService = styleService ?? throw new ArgumentNullException(nameof(styleService));
        }

        /// <summary>
        /// 应用样式设置（创建图层、文字样式、标注样式等）
        /// 对应旧项目 BaseConfig.InitializeStyle()
        /// </summary>
        public void ApplyStyle(ReinParameters parameters)
        {
            // 创建钢筋相关图层
            _layerService.CreateMultipleLayers(
                ("01_hy_1钢筋_线钢筋", 1),      // 红色
                ("01_hy_1钢筋_点钢筋", 3),      // 绿色
                ("00_hy_2公共_视口", 7),         // 白色
                ("00_hy_3公共_标注3_引线", 7)    // 白色
            );

            // 创建文字样式
            _styleService.CreateTextStyle(
                DefaultTextStyleName,
                "tssdeng.shx",
                "hztxt.shx",
                parameters.TextSize * parameters.Scale,
                parameters.TextXScale
            );

            // 创建标注样式
            _styleService.CreateDimensionStyle(
                DefaultDimStyleName,
                DefaultTextStyleName,
                parameters.Scale
            );

            // 创建多重引线样式
            _styleService.CreateMLeaderStyle(
                DefaultMLeaderStyleName,
                DefaultTextStyleName
            );

            // 创建表格样式
            _styleService.CreateTableStyle(
                DefaultTableStyleName,
                DefaultTextStyleName
            );
        }

        /// <summary>
        /// 绘制钢筋
        /// TODO: 待实现，对应旧项目 Reinforcement.Rein()
        /// </summary>
        public void DrawReinforcement(ReinParameters parameters)
        {
            throw new NotImplementedException("钢筋绘制功能尚未迁移。对应旧项目 Reinforcement.Rein() 方法。");
        }

        /// <summary>
        /// 标注钢筋
        /// TODO: 待实现，对应旧项目 MleaderRein/MleaderReinOne/MleaderReinTwo
        /// </summary>
        public void DimensionRein(int mode, ReinParameters parameters)
        {
            throw new NotImplementedException($"钢筋标注模式 {mode} 功能尚未迁移。");
        }
    }
}
