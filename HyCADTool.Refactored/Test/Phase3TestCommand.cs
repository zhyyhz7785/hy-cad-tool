using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.Runtime;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Infrastructure.Configuration;
using System;
using Exception = System.Exception;

namespace HyCADTool.Refactored.Test
{
    /// <summary>
    /// 阶段 3 测试命令集
    /// 用于验证配置管理功能
    /// </summary>
    public static class Phase3TestCommand
    {
        #region 测试 1: 配置服务加载

        /// <summary>
        /// 测试配置服务是否正确加载
        /// </summary>
        public static void TestConfigurationLoading()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            ed.WriteMessage("\n=== 测试 1: 配置服务加载 ===");

            try
            {
                var configService = ServiceLocator.Resolve<IConfigurationService>();
                
                if (configService == null)
                {
                    ed.WriteMessage("\n❌ 测试失败：无法解析 IConfigurationService");
                    return;
                }

                ed.WriteMessage("\n✅ 配置服务解析成功");

                // 测试加载配置
                configService.LoadAllConfigurations();
                ed.WriteMessage("\n✅ 配置加载成功");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试失败: {ex.Message}");
            }
        }

        #endregion

        #region 测试 2: 基础配置

        /// <summary>
        /// 测试基础配置读取
        /// </summary>
        public static void TestBaseConfiguration()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            ed.WriteMessage("\n\n=== 测试 2: 基础配置 ===");

            try
            {
                var configService = ServiceLocator.Resolve<IConfigurationService>();
                var baseConfig = configService.GetBaseConfiguration();

                ed.WriteMessage($"\nScale: {baseConfig.Scale}");
                ed.WriteMessage($"\nElevationLength: {baseConfig.ElevationLength}");
                ed.WriteMessage($"\nToleranceDouble: {baseConfig.ToleranceDouble}");

                // 验证
                bool passed = true;

                if (baseConfig.Scale <= 0)
                {
                    ed.WriteMessage($"\n❌ Scale 验证失败: {baseConfig.Scale}（应 > 0）");
                    passed = false;
                }
                else
                {
                    ed.WriteMessage($"\n✅ Scale 验证通过: {baseConfig.Scale}");
                }

                if (baseConfig.ElevationLength <= 0)
                {
                    ed.WriteMessage($"\n❌ ElevationLength 验证失败: {baseConfig.ElevationLength}（应 > 0）");
                    passed = false;
                }
                else
                {
                    ed.WriteMessage($"\n✅ ElevationLength 验证通过: {baseConfig.ElevationLength}");
                }

                if (baseConfig.ToleranceDouble <= 0)
                {
                    ed.WriteMessage($"\n❌ ToleranceDouble 验证失败: {baseConfig.ToleranceDouble}（应 > 0）");
                    passed = false;
                }
                else
                {
                    ed.WriteMessage($"\n✅ ToleranceDouble 验证通过: {baseConfig.ToleranceDouble}");
                }

                if (passed)
                    ed.WriteMessage("\n✅ 所有基础配置验证通过");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试失败: {ex.Message}");
            }
        }

        #endregion

        #region 测试 3: 桩基配置

        /// <summary>
        /// 测试桩基配置读取
        /// </summary>
        public static void TestPileConfiguration()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            ed.WriteMessage("\n\n=== 测试 3: 桩基配置 ===");

            try
            {
                var configService = ServiceLocator.Resolve<IConfigurationService>();
                var pileConfig = configService.GetPileConfiguration();

                ed.WriteMessage($"\nSection: {pileConfig.Section}");
                ed.WriteMessage($"\nDiameterOrEdge: {pileConfig.DiameterOrEdge}mm");
                ed.WriteMessage($"\nArrangementType: {pileConfig.ArrangementType}");
                ed.WriteMessage($"\nPileArrangeRate: {pileConfig.PileArrangeRate}");
                ed.WriteMessage($"\nMargin: Up={pileConfig.Margin.Up}, Down={pileConfig.Margin.Down}, Left={pileConfig.Margin.Left}, Right={pileConfig.Margin.Right}");
                ed.WriteMessage($"\nMinPileCenterDistance: {pileConfig.MinPileCenterDistance}mm");
                ed.WriteMessage($"\nInputDisplacementRate: {pileConfig.InputDisplacementRate}");
                ed.WriteMessage($"\nInputDistanceFromContour: {pileConfig.InputDistanceFromContour}mm");

                // 验证
                bool passed = true;

                if (pileConfig.DiameterOrEdge < 200 || pileConfig.DiameterOrEdge > 1200)
                {
                    ed.WriteMessage($"\n❌ DiameterOrEdge 验证失败: {pileConfig.DiameterOrEdge}（应在 200-1200mm）");
                    passed = false;
                }
                else
                {
                    ed.WriteMessage($"\n✅ DiameterOrEdge 验证通过: {pileConfig.DiameterOrEdge}mm");
                }

                if (pileConfig.PileArrangeRate < 0 || pileConfig.PileArrangeRate > 1)
                {
                    ed.WriteMessage($"\n❌ PileArrangeRate 验证失败: {pileConfig.PileArrangeRate}（应在 0-1）");
                    passed = false;
                }
                else
                {
                    ed.WriteMessage($"\n✅ PileArrangeRate 验证通过: {pileConfig.PileArrangeRate}");
                }

                if (pileConfig.MinPileCenterDistance < 500 || pileConfig.MinPileCenterDistance > 5000)
                {
                    ed.WriteMessage($"\n❌ MinPileCenterDistance 验证失败: {pileConfig.MinPileCenterDistance}（应在 500-5000mm）");
                    passed = false;
                }
                else
                {
                    ed.WriteMessage($"\n✅ MinPileCenterDistance 验证通过: {pileConfig.MinPileCenterDistance}mm");
                }

                if (passed)
                    ed.WriteMessage("\n✅ 所有桩基配置验证通过");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试失败: {ex.Message}");
            }
        }

        #endregion

        #region 测试 4: 样式创建

        /// <summary>
        /// 测试从配置创建样式
        /// </summary>
        public static void TestStyleCreationFromConfig()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            ed.WriteMessage("\n\n=== 测试 4: 样式创建 ===");

            try
            {
                var styleService = ServiceLocator.Resolve<IStyleService>();

                // 检查文本样式
                bool textStyleExists = styleService.TextStyleExists("0_Hy_40") || 
                                       styleService.TextStyleExists("HyCAD_Standard");
                
                if (textStyleExists)
                {
                    ed.WriteMessage("\n✅ 文本样式已创建");
                }
                else
                {
                    ed.WriteMessage("\n⚠ 文本样式未找到（可能因配置文件不存在）");
                }

                // 检查标注样式
                bool dimStyleExists = styleService.DimensionStyleExists("0_Hy_40_Dim") || 
                                     styleService.DimensionStyleExists("HyCAD_Dim");
                
                if (dimStyleExists)
                {
                    ed.WriteMessage("\n✅ 标注样式已创建");
                }
                else
                {
                    ed.WriteMessage("\n⚠ 标注样式未找到（可能因配置文件不存在）");
                }

                if (textStyleExists && dimStyleExists)
                {
                    ed.WriteMessage("\n✅ 样式创建测试通过");
                }
                else
                {
                    ed.WriteMessage("\n⚠ 部分样式未创建（使用默认配置）");
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试失败: {ex.Message}");
            }
        }

        #endregion

        #region 测试 5: 图层创建

        /// <summary>
        /// 测试从配置创建图层
        /// </summary>
        public static void TestLayerCreationFromConfig()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            ed.WriteMessage("\n\n=== 测试 5: 图层创建 ===");

            try
            {
                var layerService = ServiceLocator.Resolve<ILayerService>();

                // 检查默认图层
                string[] expectedLayers = { "00_Hy_配筋", "00_Hy_标注", "00_Hy_轴线" };
                int foundCount = 0;

                foreach (var layerName in expectedLayers)
                {
                    if (layerService.LayerExists(layerName))
                    {
                        ed.WriteMessage($"\n✅ 图层已创建: {layerName}");
                        foundCount++;
                    }
                    else
                    {
                        ed.WriteMessage($"\n⚠ 图层未找到: {layerName}");
                    }
                }

                if (foundCount == expectedLayers.Length)
                {
                    ed.WriteMessage($"\n✅ 图层创建测试通过（{foundCount}/{expectedLayers.Length}）");
                }
                else if (foundCount > 0)
                {
                    ed.WriteMessage($"\n⚠ 部分图层已创建（{foundCount}/{expectedLayers.Length}）");
                }
                else
                {
                    ed.WriteMessage($"\n❌ 未找到任何预期图层");
                }
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n❌ 测试失败: {ex.Message}");
            }
        }

        #endregion

        #region 综合测试

        /// <summary>
        /// 运行所有阶段 3 测试
        /// </summary>
        public static void RunAllPhase3Tests()
        {
            var doc = Application.DocumentManager.MdiActiveDocument;
            var ed = doc.Editor;

            ed.WriteMessage("\n========================================");
            ed.WriteMessage("\nHyCADTool.Refactored 阶段 3 综合测试");
            ed.WriteMessage("\n========================================");

            try
            {
                TestConfigurationLoading();
                TestBaseConfiguration();
                TestPileConfiguration();
                TestStyleCreationFromConfig();
                TestLayerCreationFromConfig();

                ed.WriteMessage("\n");
                ed.WriteMessage("\n========================================");
                ed.WriteMessage("\n阶段 3 综合测试结束");
                ed.WriteMessage("\n========================================");
            }
            catch (Exception ex)
            {
                ed.WriteMessage($"\n\n❌ 测试执行失败: {ex.Message}\n");
            }
        }

        #endregion
    }
}

