using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;
using Autofac;
using HyCADTool.Refactored.Infrastructure.Configuration;
using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

// 已移除 [assembly: CommandClass(typeof(PluginInitializer))]
// 不指定 CommandClass 时 AutoCAD 自动扫描所有类型的 [CommandMethod]
// CommandRegistry.cs 集中注册所有命令简写

namespace HyCADTool.Refactored.Presentation
{
    /// <summary>
    /// 插件初始化器
    /// 实现 IExtensionApplication 接口，由 AutoCAD 自动调用
    /// </summary>
    public class PluginInitializer : IExtensionApplication
    {
        /// <summary>
        /// 插件初始化
        /// </summary>
        public void Initialize()
        {
            try
            {
                WriteMessage("\n========================================");
                WriteMessage("\nHyCADTool.Refactored 插件初始化中...");
                WriteMessage("\n========================================");

                // 构建 Autofac 容器
                var builder = new ContainerBuilder();
                builder.RegisterModule<AutofacModule>();
                var container = builder.Build();

                // 注册到服务定位器
                ServiceLocator.Initialize(container);

                WriteMessage("\n✓ 依赖注入容器已初始化");

                // 加载配置
                LoadConfigurations();

                WriteMessage("\n✓ 配置已加载");

                // 一次性初始化所有样式和图层
                InitializeStylesAndLayers();

                // 注册文档事件（可选）
                RegisterDocumentEvents();

                WriteMessage("\n========================================");
                WriteMessage("\n✓ HyCADTool.Refactored 插件初始化完成！");
                WriteMessage("\n========================================\n");
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n✗ [错误] 插件初始化失败：{ex.Message}");
                WriteMessage($"\n堆栈跟踪：{ex.StackTrace}");
            }
        }

        /// <summary>
        /// 插件终止
        /// </summary>
        public void Terminate()
        {
            try
            {
                WriteMessage("\n========================================");
                WriteMessage("\nHyCADTool.Refactored 插件正在卸载...");

                // 保存当前面板设置
                try
                {
                    ViewModels.SettingsPanelViewModel.Current?.SaveSettings();
                }
                catch { }

                // 清理事件订阅
                UnregisterDocumentEvents();

                // 清理容器
                ServiceLocator.Reset();

                WriteMessage("\n✓ HyCADTool.Refactored 插件已卸载");
                WriteMessage("\n========================================\n");
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n✗ [错误] 插件终止失败：{ex.Message}");
            }
        }

        /// <summary>
        /// 加载配置
        /// </summary>
        private void LoadConfigurations()
        {
            try
            {
                var configService = ServiceLocator.Resolve<Domain.Interfaces.IConfigurationService>();
                configService.LoadAll();

                // 显示配置信息
                var globalConfig = configService.Global;
                WriteMessage($"\n  - Scale: {globalConfig.Scale.Default}");
                WriteMessage($"\n  - ElevationLength: {globalConfig.ElevationLength}");
                WriteMessage($"\n  - Tolerance: {globalConfig.Tolerance.Double}");

                var pileConfig = configService.GetModuleConfig<Domain.ValueObjects.Configuration.Modules.PileConfiguration>("Pile");
                WriteMessage($"\n  - Pile Diameter: {pileConfig.DiameterOrEdge}mm");
                WriteMessage($"\n  - Pile Section: {pileConfig.Section}");
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n  ⚠ 配置加载警告：{ex.Message}（使用默认值）");
            }
        }

        /// <summary>
        /// 一次性初始化所有样式和图层（插件启动时执行）
        /// 从 hy-settings.json 加载上次持久化的参数（Scale、字体等），不再硬编码
        /// 后续命令不再重复创建，仅面板参数变更时按需更新
        /// </summary>
        private void InitializeStylesAndLayers()
        {
            try
            {
                var styleService = ServiceLocator.Resolve<Domain.Interfaces.IStyleService>();
                var layerService = ServiceLocator.Resolve<Domain.Interfaces.ILayerService>();

                // 从 SettingsPanelViewModel 获取持久化参数（构造时已自动 LoadSettings）
                var vm = ViewModels.SettingsPanelViewModel.GetOrCreate(
                    AcApp.DocumentManager.MdiActiveDocument?.Name ?? "default",
                    styleService);

                double scale = vm.Scale;
                string dimStyleName = vm.DimStyleName;
                string mleaderStyleName = vm.MLeaderStyleName;
                string tableStyleName = vm.TableStyleName;

                styleService.CreateTextStyle(vm.StyleTName, vm.StyleTFont, "", vm.TextSize * scale, vm.TextXScale);
                styleService.CreateTextStyle(vm.StyleSName, vm.StyleSFont, vm.StyleSBigFont, vm.TextSize * scale, vm.TextXScale);
                styleService.SetCurrentTextStyle(vm.StyleSName);
                styleService.CreateDimensionStyle(dimStyleName, vm.TextStyleName, scale, vm.Dimtxt, vm.Dimexo, vm.Dimexe, vm.Dimdle, vm.Dimgap, vm.Dimasz);
                styleService.SetCurrentDimensionStyle(dimStyleName);
                styleService.CreateMLeaderStyle(mleaderStyleName, vm.TextStyleName, scale, vm.MLeaderArrowSize, vm.MLeaderLandingGap, vm.TextSize, vm.MLeaderTextColorIndex);
                styleService.SetCurrentMLeaderStyle(mleaderStyleName);
                styleService.CreateTableStyle(tableStyleName, vm.TextStyleName);
                styleService.SetCurrentTableStyle(tableStyleName);

                WriteMessage($"\n  ✓ 样式已创建 (Scale={scale}, 从 hy-settings.json 加载)");

                // 创建所有需要的图层（静默模式，不输出每个图层）
                layerService.CreateMultipleLayers(
                    // ── 钢筋 ──
                    ("01_hy_1钢筋_线钢筋", 1),
                    ("01_hy_1钢筋_点钢筋", 5),
                    ("01_hy_1钢筋_线钢筋_外部", 1),
                    // ── 公共标注 ──
                    ("00_hy_3公共_标注1_外", 3),
                    ("00_hy_3公共_标注3_引线", 92),
                    // ── 筏板附加配筋 ──
                    ("00_hy_配筋轮廓", 1),
                    ("00_hy_调整配筋轮廓", 3),
                    ("00_hy_筏板附加配筋x_上", 1),
                    ("00_hy_筏板附加配筋x_下", 1),
                    ("00_hy_筏板附加配筋y_上", 3),
                    ("00_hy_筏板附加配筋y_下", 3),
                    ("00_hy_筏板附加配筋文字_x", 7),
                    ("00_hy_筏板附加配筋文字_y", 7),
                    ("00_hy_筏板附加配筋x_标注", 1),
                    ("00_hy_筏板附加配筋Y_标注", 3),
                    // ── 视口 ──
                    ("00_hy_2公共_视口", 1),
                    // ── 桩基 ──
                    ("02_hy_1桩_主", 3),
                    ("02_hy_3桩_地基内轮廓", 8),
                    // ── 垫层 ──
                    ("00_hy_垫层", 7),
                    // ── 图框 ──
                    ("00_hy_图框", 7),
                    // ── 配筋文字分类 ──
                    ("HY_H向钢筋", 7),
                    ("HY_V向钢筋", 2),
                    ("HY_手动配筋", 1),
                    // ── 聚类分析 ──
                    ("00_hy_BP", 3),
                    ("00_hy_AAP", 1),
                    ("00_hy_BAP", 4),
                    ("00_hy_ABolt", 2),
                    ("00_hy_SteelPlate", 5),
                    ("00_hy_AxisCircle", 7),
                    ("00_hy_AxisText", 7),
                    ("00_hy_Region", 9),
                    ("00_hy_RegionText", 9),
                    ("00_hy_Dim_X", 7),
                    ("00_hy_Dim_Y", 7),
                    ("00_hy_ClusterEP", 8),
                    ("00_hy_ClusterEEP", 8),
                    ("00_hy_ClusterHull", 6),
                    ("00_hy_ClusterPts", 34)
                );

                WriteMessage("\n  ✓ 图层已创建");
                WriteMessage($"\n  ✓ 设置文件: {ViewModels.SettingsPanelViewModel.GetSettingsFilePath()}");
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n  ⚠ 样式/图层初始化警告：{ex.Message}");
            }
        }

        /// <summary>
        /// 注册文档事件
        /// </summary>
        private void RegisterDocumentEvents()
        {
            try
            {
                AcApp.DocumentManager.DocumentActivated += OnDocumentActivated;
                AcApp.DocumentManager.DocumentCreated += OnDocumentCreated;
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n  ⚠ 事件注册警告：{ex.Message}");
            }
        }

        /// <summary>
        /// 取消注册文档事件
        /// </summary>
        private void UnregisterDocumentEvents()
        {
            try
            {
                AcApp.DocumentManager.DocumentActivated -= OnDocumentActivated;
                AcApp.DocumentManager.DocumentCreated -= OnDocumentCreated;
            }
            catch
            {
                // 忽略取消注册错误
            }
        }

        /// <summary>
        /// 文档激活事件处理
        /// </summary>
        private void OnDocumentActivated(object sender, DocumentCollectionEventArgs e)
        {
            // 可以在这里处理文档切换逻辑
        }

        /// <summary>
        /// 文档创建事件处理
        /// </summary>
        private void OnDocumentCreated(object sender, DocumentCollectionEventArgs e)
        {
            // 可以在这里为新文档初始化默认设置
        }

        /// <summary>
        /// 向 AutoCAD 命令行写入消息
        /// </summary>
        private void WriteMessage(string message)
        {
            try
            {
                var doc = AcApp.DocumentManager.MdiActiveDocument;
                doc?.Editor?.WriteMessage(message);
            }
            catch
            {
                // 如果没有活动文档，忽略错误
            }
        }
    }
}

