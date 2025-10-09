using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Domain.Models.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using static HyCADTool.BaseRein;

namespace HyCADTool.Refactored.Application.Services
{
    /// <summary>
    /// 基础钢筋配置服务实现
    /// 作为适配器，将新架构的接口调用转发到原有的静态类 BaseRein
    /// </summary>
    public class BaseReinforcementService : IBaseReinforcementService
    {
        private readonly IConfigurationService _configService;
        private BaseReinforcementConfig _currentConfig;

        public BaseReinforcementService(IConfigurationService configService)
        {
            _configService = configService ?? throw new ArgumentNullException(nameof(configService));
            _currentConfig = BaseReinforcementConfig.CreateDefault();
        }

        #region 样式设置

        public void ApplyStyles(double scale)
        {
            try
            {
                BaseRein.Scale = scale;
                BaseRein.SetUp();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"应用样式失败: {ex.Message}", ex);
            }
        }

        #endregion

        #region 步骤操作

        public void OptimizeBasemap()
        {
            try
            {
                BaseRein.OptimizeBasemap();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"整理底图失败: {ex.Message}", ex);
            }
        }

        public void SelectAndDeleteUnusedText(List<string> fixedValues)
        {
            if (fixedValues == null || fixedValues.Count == 0)
            {
                throw new ArgumentException("固定值列表不能为空", nameof(fixedValues));
            }

            try
            {
                BaseRein.FixedValues = fixedValues;
                BaseRein.SelectNoUseText();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"选择删除失败: {ex.Message}", ex);
            }
        }

        public void GenerateReinforcementArea(BaseReinforcementConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            try
            {
                // 应用配置到静态类
                ApplyConfigToStaticClass(config);

                // 执行步骤 4 的逻辑
                BaseRein.SourceTextAndFinitePoly = BaseRein.SelectFiniteElementGrid();
                BaseRein.SourceTextAndEnvelopePoly = BaseRein.DrawBoundingPolyline(BaseRein.SourceTextAndFinitePoly);
                BaseRein.GroupByTextAndEnvelopePoly = BaseRein.GroupBySpatialProximity(
                    BaseRein.SourceTextAndEnvelopePoly, 
                    BaseRein.proximityThreshold);
                BaseRein.CreateOptimizedBoundingPolygonFromPolygons(
                    BaseRein.GroupByTextAndEnvelopePoly, 
                    "00_hy_调整配筋轮廓");
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"生成配筋面积失败: {ex.Message}", ex);
            }
        }

        public void DrawReinforcement(BaseReinforcementConfig config, bool dimAll)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            try
            {
                // 应用配置到静态类
                ApplyConfigToStaticClass(config);
                BaseRein.DimAll = dimAll;

                // 执行步骤 5 的逻辑
                var reinforcementData = BaseRein.ReinforcementStepA();

                if (dimAll)
                {
                    BaseRein.ReinforcementAll(reinforcementData);
                }
                else
                {
                    BaseRein.ReinforcementStepB(reinforcementData);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"绘制钢筋失败: {ex.Message}", ex);
            }
        }

        public void DimensionReinforcementArea(IntersectionsDirection direction)
        {
            try
            {
                BaseRein.InterDirection = direction;
                BaseRein.Poly4Dim();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"标注配筋区域失败: {ex.Message}", ex);
            }
        }

        #endregion

        #region 配置管理

        public BaseReinforcementConfig GetCurrentConfig()
        {
            return _currentConfig.Clone();
        }

        public void SaveConfig(BaseReinforcementConfig config)
        {
            if (config == null)
            {
                throw new ArgumentNullException(nameof(config));
            }

            _currentConfig = config.Clone();
            ApplyConfigToStaticClass(_currentConfig);

            // TODO: 将配置持久化到文件（通过 IConfigService）
            // _configService.SaveBaseReinforcementConfig(_currentConfig);
        }

        public BaseReinforcementConfig ResetToDefault()
        {
            _currentConfig = BaseReinforcementConfig.CreateDefault();
            ApplyConfigToStaticClass(_currentConfig);
            return _currentConfig.Clone();
        }

        #endregion

        #region 私有辅助方法

        /// <summary>
        /// 将配置对象的属性应用到 BaseRein 静态类
        /// </summary>
        private void ApplyConfigToStaticClass(BaseReinforcementConfig config)
        {
            // 基本设置
            BaseRein.Scale = config.Scale;
            BaseRein.PlateThickness = config.PlateThickness;

            // 钢筋参数
            BaseRein.RebarDiameter = config.RebarDiameter;
            BaseRein.RebarSpacing = config.RebarSpacing;
            BaseRein.MinAdditionalDiameter = config.MinAdditionalDiameter;
            BaseRein.AdditionalSpacing = config.AdditionalSpacing;
            BaseRein.ReinforceSafety = config.ReinforceSafety;

            // 文字偏移
            BaseRein.ReinforceTextDistanceX = config.ReinforceTextDistanceX;
            BaseRein.ReinforceTextDistanceY = config.ReinforceTextDistanceY;

            // 高级设置
            BaseRein.AnchorFactor = config.AnchorFactor;
            BaseRein.proximityThreshold = config.ProximityThreshold;
            BaseRein.ReinforceDistance = config.ReinforceDistance;
            BaseRein.TextToLineDistance = config.TextToLineDistance;
            BaseRein.HookLength = config.HookLength;
            BaseRein.PolylineWidth = config.PolylineWidth;
            BaseRein.AxisExtend = config.AxisExtend;
            BaseRein.DimensionDistanceWithDim = config.DimensionDistanceWithDim;
            BaseRein.Interval = config.Interval;

            // 复选框状态
            BaseRein.AddAnchorLength = config.AddAnchorLength;
            BaseRein.ExistingRebar = config.ExistingRebar;
            BaseRein.DimAll = config.DimAll;

            // 枚举选择
            BaseRein.Direction = config.Direction;
            BaseRein.InterDirection = config.DimDirection;
        }

        #endregion
    }
}

