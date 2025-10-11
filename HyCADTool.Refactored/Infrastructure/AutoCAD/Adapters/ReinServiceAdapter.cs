using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using HyCADTool.Refactored.Domain.Services;
using HyCADTool.Refactored.Domain.ValueObjects;
using HyCADTool.Commands;
using HyCADTool.Config;

namespace HyCADTool.Refactored.Infrastructure.AutoCAD.Adapters
{
    /// <summary>
    /// 钢筋服务适配器
    /// 将新架构的调用适配到原有的静态类实现
    /// </summary>
    public class ReinServiceAdapter : IReinService
    {
        #region 字段

        private readonly Autodesk.AutoCAD.ApplicationServices.DocumentCollection _documents;

        #endregion

        #region 构造函数

        public ReinServiceAdapter()
        {
            _documents = AcApp.DocumentManager;
        }

        #endregion

        #region IReinService 实现

        /// <summary>
        /// 绘制钢筋
        /// </summary>
        public void DrawReinforcement(ReinParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            var doc = _documents.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("没有活动文档");

            try
            {
                // 1. 更新参数到原系统
                UpdateReinforcementParameters(parameters);

                // 2. 调用原有的钢筋绘制逻辑
                Reinforcement.Rein();
            }
            catch (Exception ex)
            {
                doc.Editor.WriteMessage($"\n绘制钢筋时发生错误: {ex.Message}\n");
                throw;
            }
        }

        /// <summary>
        /// 应用样式设置
        /// </summary>
        public void ApplyStyle(ReinParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            var doc = _documents.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("没有活动文档");

            try
            {
                // 1. 更新参数到原系统
                UpdateReinforcementParameters(parameters);

                // 2. 初始化样式
                BaseConfig.InitializeStyle();

                doc.Editor.WriteMessage("\n样式设置成功\n");
            }
            catch (Exception ex)
            {
                doc.Editor.WriteMessage($"\n设置样式时发生错误: {ex.Message}\n");
                throw;
            }
        }

        /// <summary>
        /// 标注钢筋
        /// </summary>
        public void DimensionRein(int mode, ReinParameters parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            var doc = _documents.MdiActiveDocument;
            if (doc == null)
                throw new InvalidOperationException("没有活动文档");

            try
            {
                // 1. 更新参数到原系统
                UpdateReinforcementParameters(parameters);

                // 2. 根据模式调用相应的标注命令
                switch (mode)
                {
                    case 1:
                        // gb 命令 - 三点标注
                        HyCommand.MleaderRein();
                        break;
                    case 2:
                        // gb1 命令 - 单点标注
                        HyCommand.MleaderReinOne();
                        break;
                    case 3:
                        // gb2 命令 - 六点标注
                        HyCommand.MleaderReinTwo();
                        break;
                    default:
                        throw new ArgumentException($"无效的标注模式: {mode}，支持的模式为 1、2、3");
                }
            }
            catch (Exception ex)
            {
                doc.Editor.WriteMessage($"\n标注钢筋时发生错误: {ex.Message}\n");
                throw;
            }
        }

        #endregion

        #region 私有方法

        /// <summary>
        /// 将参数更新到原系统
        /// 通过创建临时面板并设置为 ActivePanel 来传递参数
        /// </summary>
        private void UpdateReinforcementParameters(ReinParameters parameters)
        {
            // 创建临时面板实例
            var tempPanel = new HyCADTool.Views.ReinPanel();

            // 设置比例
            BaseConfig.Scale = parameters.Scale;

            // 直接设置面板属性（不通过控件）
            tempPanel.AnchorageLength = parameters.AnchorageLength;
            tempPanel.DotSeparation = parameters.DotSeparation;
            tempPanel.BendingLineMinLength = parameters.BendingLineMinLength;
            tempPanel.AnchorageJoinLength = parameters.AnchorageJoinLength;
            tempPanel.HookLength = parameters.HookLength;
            tempPanel.ProtectionThickness = parameters.ProtectionThickness;
            tempPanel.ReinforcementDiameter = parameters.ReinforcementDiameter;
            tempPanel.DotReinOffset = parameters.DotReinOffset;
            tempPanel.RebarDiameter = parameters.RebarDiameter;
            tempPanel.RebarSpacing = parameters.RebarSpacing;
            tempPanel.DimensionDistanceInside = parameters.DimensionDistanceInside;
            tempPanel.DimensionDistanceOutside = parameters.DimensionDistanceOutside;
            tempPanel.DimensionDistanceWithDim = parameters.DimensionDistanceWithDim;
            tempPanel.MleaderDistance = parameters.MleaderDistance;
            tempPanel.DimDistanceTolerance = parameters.DimDistanceTolerance;
            tempPanel.TextXScale = parameters.TextXScale;
            tempPanel.TextSize = parameters.TextSize;

            // 设置为活动面板（这样 Reinforcement 类可以通过 ActivePanel 访问参数）
            HyCADTool.Views.ReinPanel.ActivePanel = tempPanel;
        }

        #endregion
    }
}

