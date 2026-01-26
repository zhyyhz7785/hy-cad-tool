using Autodesk.AutoCAD.Runtime;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Domain.ValueObjects.Configuration.Modules;
using HyCADTool.Refactored.Infrastructure.Configuration;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

namespace HyCADTool.Refactored.Presentation.Commands
{
    /// <summary>
    /// 测试配置系统命令
    /// </summary>
    public class TestConfigCommand
    {
        /// <summary>
        /// 测试全局配置
        /// </summary>
        [CommandMethod("HYTESTGLOBALCONFIG")]
        public void TestGlobalConfig()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;

            try
            {
                // 从服务定位器获取配置服务
                var configService = ServiceLocator.Resolve<IConfigurationService>();

                // 测试全局配置
                ed.WriteMessage("\n========== 全局配置测试 ==========");
                ed.WriteMessage($"\n比例: {configService.Global.Scale.Default}");
                ed.WriteMessage($"\n最小比例: {configService.Global.Scale.MinValue}");
                ed.WriteMessage($"\n最大比例: {configService.Global.Scale.MaxValue}");
                ed.WriteMessage($"\n容差(Double): {configService.Global.Tolerance.Double:E2}");
                ed.WriteMessage($"\n容差(Vector): {configService.Global.Tolerance.Vector:E2}");
                ed.WriteMessage($"\n容差(Point): {configService.Global.Tolerance.Point:E2}");
                ed.WriteMessage($"\n标高长度: {configService.Global.ElevationLength}");
                
                // 测试样式配置
                ed.WriteMessage($"\n\n文本样式名称: {configService.Global.Styles.TextStyle.Name}");
                ed.WriteMessage($"\n文本样式字体: {configService.Global.Styles.TextStyle.FontFileName}");
                ed.WriteMessage($"\n文本样式大字体: {configService.Global.Styles.TextStyle.BigFontFileName}");
                ed.WriteMessage($"\n文本高度: {configService.Global.Styles.TextStyle.TextSize}");
                ed.WriteMessage($"\n文本宽度比例: {configService.Global.Styles.TextStyle.XScale}");
                
                ed.WriteMessage($"\n\n标注样式名称: {configService.Global.Styles.DimensionStyle.Name}");
                ed.WriteMessage($"\n标注文本样式: {configService.Global.Styles.DimensionStyle.TextStyleName}");
                ed.WriteMessage($"\n标注文本高度: {configService.Global.Styles.DimensionStyle.TextHeight}");
                
                ed.WriteMessage($"\n\n多重引线样式名称: {configService.Global.Styles.MLeaderStyle.Name}");
                ed.WriteMessage($"\n多重引线文本样式: {configService.Global.Styles.MLeaderStyle.TextStyleName}");
                
                // 测试路径配置
                ed.WriteMessage($"\n\n导出路径: {configService.Global.Paths.DefaultExportPath}");
                ed.WriteMessage($"\n导入路径: {configService.Global.Paths.DefaultImportPath}");
                ed.WriteMessage($"\n临时文件路径: {configService.Global.Paths.TempFilesPath}");
                
                ed.WriteMessage("\n\n全局配置测试完成！");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}");
                ed.WriteMessage($"\n堆栈: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 测试模块配置
        /// </summary>
        [CommandMethod("HYTESTMODULECONFIG")]
        public void TestModuleConfig()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;

            try
            {
                // 从服务定位器获取配置服务
                var configService = ServiceLocator.Resolve<IConfigurationService>();

                ed.WriteMessage("\n========== 模块配置测试 ==========");

                // 测试桩基配置
                var pileConfig = configService.GetModuleConfig<PileConfiguration>("Pile");
                ed.WriteMessage($"\n\n【桩基配置】");
                ed.WriteMessage($"\n桩截面类型: {pileConfig.Section}");
                ed.WriteMessage($"\n桩直径/边长: {pileConfig.DiameterOrEdge} mm");
                ed.WriteMessage($"\n布置类型: {pileConfig.ArrangementType}");
                ed.WriteMessage($"\n桩布置率: {pileConfig.PileArrangeRate}");
                ed.WriteMessage($"\n边距 (上/下/左/右): {pileConfig.Margin.Up}/{pileConfig.Margin.Down}/{pileConfig.Margin.Left}/{pileConfig.Margin.Right} mm");
                ed.WriteMessage($"\n最小桩心距: {pileConfig.MinPileCenterDistance} mm");
                
                // 测试基础配置
                var foundationConfig = configService.GetModuleConfig<FoundationConfiguration>("Foundation");
                ed.WriteMessage($"\n\n【基础配置】");
                ed.WriteMessage($"\n基础类型: {foundationConfig.Type}");
                ed.WriteMessage($"\n默认厚度: {foundationConfig.DefaultThickness} mm");
                ed.WriteMessage($"\n最小保护层: {foundationConfig.MinConcreteCover} mm");
                ed.WriteMessage($"\n默认埋深: {foundationConfig.DefaultDepth} mm");
                ed.WriteMessage($"\n底标高: {foundationConfig.BottomElevation} m");
                
                // 测试钢筋配置
                var reinConfig = configService.GetModuleConfig<ReinforcementConfiguration>("Reinforcement");
                ed.WriteMessage($"\n\n【钢筋配置】");
                ed.WriteMessage($"\n默认直径: {reinConfig.DefaultDiameter} mm");
                ed.WriteMessage($"\n默认间距: {reinConfig.DefaultSpacing} mm");
                ed.WriteMessage($"\n锚固长度: {reinConfig.AnchorageLength} mm");
                ed.WriteMessage($"\n保护层厚度: {reinConfig.ConcreteCover} mm");
                ed.WriteMessage($"\n搭接长度系数: {reinConfig.LapLengthFactor}");
                ed.WriteMessage($"\n最小/最大间距: {reinConfig.MinSpacing}/{reinConfig.MaxSpacing} mm");
                
                // 测试标高配置
                var elevConfig = configService.GetModuleConfig<ElevationConfiguration>("Elevation");
                ed.WriteMessage($"\n\n【标高配置】");
                ed.WriteMessage($"\n符号长度: {elevConfig.SymbolLength}");
                ed.WriteMessage($"\n文字格式: {elevConfig.TextFormat}");
                ed.WriteMessage($"\n显示单位: {elevConfig.ShowUnit}");
                ed.WriteMessage($"\n单位文本: {elevConfig.UnitText}");
                ed.WriteMessage($"\n符号类型: {elevConfig.SymbolType}");
                ed.WriteMessage($"\n符号大小: {elevConfig.SymbolSize}");
                ed.WriteMessage($"\n小数位数: {elevConfig.DecimalPlaces}");
                
                ed.WriteMessage("\n\n模块配置测试完成！");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}");
                ed.WriteMessage($"\n堆栈: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// 测试配置更新
        /// </summary>
        [CommandMethod("HYTESTUPDATECONFIG")]
        public void TestUpdateConfig()
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            var ed = doc.Editor;

            try
            {
                // 从服务定位器获取配置服务
                var configService = ServiceLocator.Resolve<IConfigurationService>();

                ed.WriteMessage("\n========== 配置更新测试 ==========");

                // 获取并显示当前比例
                double oldScale = configService.GetScale();
                ed.WriteMessage($"\n当前比例: {oldScale}");

                // 更新比例
                double newScale = 50.0;
                configService.SetScale(newScale);
                ed.WriteMessage($"\n更新比例为: {newScale}");

                // 验证更新
                double currentScale = configService.GetScale();
                ed.WriteMessage($"\n验证比例: {currentScale}");

                if (System.Math.Abs(currentScale - newScale) < 1e-6)
                {
                    ed.WriteMessage("\n✓ 比例更新成功！");
                }
                else
                {
                    ed.WriteMessage("\n✗ 比例更新失败！");
                }

                // 恢复原始比例
                configService.SetScale(oldScale);
                ed.WriteMessage($"\n恢复原始比例: {oldScale}");

                ed.WriteMessage("\n\n配置更新测试完成！");
            }
            catch (System.Exception ex)
            {
                ed.WriteMessage($"\n错误: {ex.Message}");
                ed.WriteMessage($"\n堆栈: {ex.StackTrace}");
            }
        }
    }
}

