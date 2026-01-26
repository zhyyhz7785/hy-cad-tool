using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.ApplicationServices;
using Autofac;
using HyCADTool.Refactored.Infrastructure.Configuration;
using System;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;

[assembly: CommandClass(typeof(HyCADTool.Refactored.Presentation.PluginInitializer))]

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

                // 初始化样式和图层（现在由配置服务处理）
                // InitializeStylesAndLayers();

                WriteMessage("\n✓ 样式和图层已初始化");

                // 注册文档事件（可选）
                RegisterDocumentEvents();

                WriteMessage("\n========================================");
                WriteMessage("\n✓ HyCADTool.Refactored 插件初始化完成！");
                WriteMessage("\n========================================");
                WriteMessage("\n提示：输入命令查看可用功能");
                WriteMessage("\n");
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
        /// 初始化样式和图层（已由 ConfigurationService 处理，保留此方法以防需要）
        /// </summary>
        private void InitializeStylesAndLayers()
        {
            try
            {
                var styleService = ServiceLocator.Resolve<Domain.Interfaces.IStyleService>();
                var layerService = ServiceLocator.Resolve<Domain.Interfaces.ILayerService>();

                // 创建默认文本样式
                var textStyleConfig = Domain.ValueObjects.Configuration.Global.TextStyleConfig.CreateDefault(
                    "HyCAD_Standard", scale: 1.0);
                styleService.CreateTextStyle(
                    textStyleConfig.Name, 
                    textStyleConfig.FontFileName, 
                    textStyleConfig.BigFontFileName, 
                    textStyleConfig.TextSize, 
                    textStyleConfig.XScale);

                // 创建默认标注样式
                var dimStyleConfig = Domain.ValueObjects.Configuration.Global.DimensionStyleConfig.CreateDefault(
                    "HyCAD_Dim", "HyCAD_Standard", scale: 1.0);
                styleService.CreateDimensionStyle(
                    dimStyleConfig.Name, 
                    dimStyleConfig.TextStyleName, 
                    scale: 1.0);

                // 创建常用图层
                CreateDefaultLayers(layerService);

                WriteMessage("\n  - 已创建文本样式: HyCAD_Standard");
                WriteMessage("\n  - 已创建标注样式: HyCAD_Dim");
                WriteMessage("\n  - 已创建默认图层");
            }
            catch (System.Exception ex)
            {
                WriteMessage($"\n  ⚠ 样式/图层初始化警告：{ex.Message}");
            }
        }

        /// <summary>
        /// 创建默认图层
        /// </summary>
        private void CreateDefaultLayers(Domain.Interfaces.ILayerService layerService)
        {
            // 配筋图层
            if (!layerService.LayerExists("00_Hy_配筋"))
                layerService.CreateLayer("00_Hy_配筋", 1); // 红色

            // 标注图层
            if (!layerService.LayerExists("00_Hy_标注"))
                layerService.CreateLayer("00_Hy_标注", 3); // 绿色

            // 轴线图层
            if (!layerService.LayerExists("00_Hy_轴线"))
                layerService.CreateLayer("00_Hy_轴线", 5); // 蓝色
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

