using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;
using Autofac;
using HyCADTool.Refactored.Infrastructure.Configuration;
using System;
using System.Collections.Generic;
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
        private static readonly HashSet<string> _initializedDocuments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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

                // 道路子系统启动（P0 落地：事件总线 + JSON 持久化）
                InitializeRoadSubsystem();

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

                // 道路子系统收尾：
                // v1.1 起各命令在 tr.Commit() 后已同步写盘，卸载时不再需要 FlushAll；
                // 如果用户在执行过命令后直接关闭 AutoCAD，数据已经在 .roaddesign.json 里。

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
                EnsureCurrentDocumentResourcesInitialized(force: true);
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
            EnsureCurrentDocumentResourcesInitialized(force: false);
        }

        /// <summary>
        /// 文档创建事件处理
        /// </summary>
        private void OnDocumentCreated(object sender, DocumentCollectionEventArgs e)
        {
            if (e.Document == null) return;

            try
            {
                var styleService = ServiceLocator.Resolve<Domain.Interfaces.IStyleService>();
                ViewModels.SettingsPanelViewModel.GetOrCreate(e.Document.Name, styleService);
            }
            catch
            {
                // 忽略新文档预热失败，切换到该文档时会再次初始化
            }
        }

        /// <summary>
        /// 启动道路子系统（v1.1）。
        ///
        /// 变更历史：
        /// - v1.0：事件驱动 + 500ms 防抖自动持久化；
        /// - v1.1：取消防抖服务，各写命令（hyRoadA / P / T / C / Save）在 <c>tr.Commit()</c> 后同步落盘，
        ///   彻底消除 SAVEAS / 多文档切换时 Timer 回调与 <c>doc.Name</c> 不一致导致的"空 JSON / 路径失配"bug。
        ///   事件总线（<see cref="Domain.Events.Road.IRoadEventBus"/>）仍保留，供 v2 UI / Blender 插件等未来订阅者使用。
        ///
        /// v2（P7）会在此处启动 <c>RoadDesignFileWatcher</c> 监听外部 JSON 修改（Blender → AutoCAD 回推）。
        /// </summary>
        private void InitializeRoadSubsystem()
        {
            try
            {
                // 预解析核心服务，保证 AutoCAD 启动后第一次 Resolve 的成本不落在用户命令上
                ServiceLocator.Resolve<Infrastructure.AutoCAD.Services.Road.RoadDesignRegistry>();
                ServiceLocator.Resolve<Infrastructure.AutoCAD.Services.Road.RoadJsonExportService>();

                WriteMessage("\n  ✓ 道路子系统已启动（命令收尾同步落盘，无防抖 Timer）");
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n  ⚠ 道路子系统启动警告：{ex.Message}");
            }
        }

        private void EnsureCurrentDocumentResourcesInitialized(bool force)
        {
            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            string documentName = doc.Name ?? "default";
            if (!force && _initializedDocuments.Contains(documentName))
                return;

            try
            {
                var styleService = ServiceLocator.Resolve<Domain.Interfaces.IStyleService>();
                var layerService = ServiceLocator.Resolve<Domain.Interfaces.ILayerService>();

                var vm = ViewModels.SettingsPanelViewModel.GetOrCreate(documentName, styleService);
                vm.LoadSettings();
                vm.EnsureStylesApplied();

                layerService.CreateMultipleLayers(GetRequiredLayers());
                _initializedDocuments.Add(documentName);
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n  ⚠ 文档资源初始化警告：{ex.Message}");
            }
        }

        private static (string layerName, short colorIndex)[] GetRequiredLayers()
        {
            return new[]
            {
                // ── 钢筋 ──
                ("01_hy_1钢筋_线钢筋", (short)1),
                ("01_hy_1钢筋_点钢筋", (short)5),
                ("01_hy_1钢筋_线钢筋_外部", (short)1),
                // ── 公共标注 ──
                ("00_hy_3公共_标注1_外", (short)3),
                ("00_hy_3公共_标注3_引线", (short)92),
                // ── 筏板附加配筋 ──
                ("00_hy_配筋轮廓", (short)1),
                ("00_hy_调整配筋轮廓", (short)3),
                ("00_hy_筏板附加配筋x_上", (short)1),
                ("00_hy_筏板附加配筋x_下", (short)1),
                ("00_hy_筏板附加配筋y_上", (short)3),
                ("00_hy_筏板附加配筋y_下", (short)3),
                ("00_hy_筏板附加配筋文字_x", (short)7),
                ("00_hy_筏板附加配筋文字_y", (short)7),
                ("00_hy_筏板附加配筋x_标注", (short)1),
                ("00_hy_筏板附加配筋Y_标注", (short)3),
                // ── 视口 ──
                ("00_hy_2公共_视口", (short)1),
                // ── 桩基 ──
                ("02_hy_1桩_主", (short)3),
                ("02_hy_3桩_地基内轮廓", (short)8),
                // ── 垫层 ──
                ("00_hy_垫层", (short)7),
                // ── 图框 ──
                ("00_hy_图框", (short)7),
                // ── 配筋文字分类 ──
                ("HY_H向钢筋", (short)7),
                ("HY_V向钢筋", (short)2),
                ("HY_手动配筋", (short)1),
                // ── 聚类分析 ──
                ("00_hy_BP", (short)3),
                ("00_hy_AAP", (short)1),
                ("00_hy_BAP", (short)4),
                ("00_hy_ABolt", (short)2),
                ("00_hy_SteelPlate", (short)5),
                ("00_hy_AxisCircle", (short)7),
                ("00_hy_AxisText", (short)7),
                ("00_hy_Region", (short)9),
                ("00_hy_RegionText", (short)9),
                ("00_hy_Dim_X", (short)7),
                ("00_hy_Dim_Y", (short)7),
                ("00_hy_ClusterEP", (short)8),
                ("00_hy_ClusterEEP", (short)8),
                ("00_hy_ClusterHull", (short)6),
                ("00_hy_ClusterPts", (short)34),
                // ── 道路（P1+）──
                (HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata.HyRoadLayers.AlignmentLayer,
                    HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata.HyRoadLayers.AlignmentColor),
                (HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata.HyRoadLayers.ProfileLayer,
                    HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata.HyRoadLayers.ProfileColor),
                (HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata.HyRoadLayers.CorridorLayer,
                    HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata.HyRoadLayers.CorridorColor),
                (HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata.HyRoadLayers.MarkingLayer,
                    HyCADTool.Refactored.Infrastructure.AutoCAD.Xdata.HyRoadLayers.MarkingColor),
            };
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

