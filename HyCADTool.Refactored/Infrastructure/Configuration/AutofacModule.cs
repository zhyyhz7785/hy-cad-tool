using Autofac;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Domain.Services;
using HyCADTool.Refactored.Domain.Services.GeometryAlgorithms;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Interfaces;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Converters;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Repositories;
using HyCADTool.Refactored.Presentation;
using System;

namespace HyCADTool.Refactored.Infrastructure.Configuration
{
    /// <summary>
    /// Autofac 依赖注入模块配置
    /// 注册所有服务和实现的映射关系
    /// </summary>
    public class AutofacModule : Module
    {
        protected override void Load(ContainerBuilder builder)
        {
            // 注册几何转换器（单例）
            builder.RegisterType<GeometryConverter>()
                .As<IGeometryConverter>()
                .SingleInstance();

            // 注册几何服务（单例）
            builder.RegisterType<AutoCadGeometryService>()
                .As<IGeometryService>()
                .SingleInstance();

            // 注册图层服务（单例）
            builder.RegisterType<LayerService>()
                .As<ILayerService>()
                .SingleInstance();

            // 注册样式服务（单例）
            builder.RegisterType<StyleService>()
                .As<IStyleService>()
                .SingleInstance();

            // 注册输入服务（单例）
            builder.RegisterType<InputService>()
                .As<IInputService>()
                .SingleInstance();

            // ===== 阶段 1: 配置层服务 =====
            
            // 注册全局配置服务（单例）
            builder.RegisterType<GlobalConfigurationService>()
                .As<IGlobalConfigService>()
                .SingleInstance();

            // 注册模块配置服务（单例）
            builder.RegisterType<ModuleConfigurationService>()
                .As<IModuleConfigService>()
                .SingleInstance();

            // 注册配置服务总协调（单例）
            builder.RegisterType<ConfigurationService>()
                .As<IConfigurationService>()
                .SingleInstance();

            // ===== 阶段 2: 服务层 =====
            
            // 绘制服务（单例）
            builder.RegisterType<DrawingService>()
                .As<IDrawingService>()
                .SingleInstance();

            // 编辑器服务（单例）
            builder.RegisterType<EditorService>()
                .As<IEditorService>()
                .SingleInstance();

            // 数据库服务（单例）
            builder.RegisterType<DatabaseService>()
                .As<IDatabaseService>()
                .SingleInstance();

            // ===== 阶段 5.0: UI 基础设施 =====
            
            // 选择过滤服务（单例）
            builder.RegisterType<HyCADTool.Refactored.Infrastructure.AutoCAD.Selection.SelectionFilterService>()
                .As<ISelectionFilterService>()
                .SingleInstance();

            // 选择服务（单例）
            builder.RegisterType<SelectionService>()
                .As<ISelectionService>()
                .SingleInstance();

            // 面板管理器（单例）
            // PanelManager 接受 IComponentContext 作为构造参数
            builder.RegisterType<PanelManager>()
                .AsSelf()
                .SingleInstance();

            // 面板注册（按需创建，非单例）
            // TODO 阶段2后恢复: 暂时排除依赖原项目的面板注册
            // builder.RegisterType<HyCADTool.Refactored.Presentation.Views.ReinPanel>()
            //     .AsSelf()
            //     .InstancePerDependency();

            builder.RegisterType<HyCADTool.Refactored.Presentation.Views.FilterPanel>()
                .AsSelf()
                .InstancePerDependency();

            // ViewModel 注册（按需创建）
            builder.RegisterType<HyCADTool.Refactored.Presentation.ViewModels.FilterPanelViewModel>()
                .AsSelf()
                .InstancePerDependency();

            // TODO 阶段2后恢复: 暂时排除依赖原项目的 ViewModel 和服务
            // builder.RegisterType<HyCADTool.Refactored.Presentation.ViewModels.BaseReinPanelViewModel>()
            //     .AsSelf()
            //     .InstancePerDependency();

            // builder.RegisterType<HyCADTool.Refactored.Presentation.Views.BaseReinPanel>()
            //     .AsSelf()
            //     .InstancePerDependency();

            // builder.RegisterType<HyCADTool.Refactored.Application.Services.BaseReinforcementService>()
            //     .As<IBaseReinforcementService>()
            //     .SingleInstance();

            // builder.RegisterType<HyCADTool.Refactored.Presentation.ViewModels.PilePanelViewModel>()
            //     .AsSelf()
            //     .InstancePerDependency();

            // builder.RegisterType<HyCADTool.Refactored.Presentation.Views.PilePanel>()
            //     .AsSelf()
            //     .InstancePerDependency();

            // builder.RegisterType<HyCADTool.Refactored.Presentation.ViewModels.ClusterPanelViewModel>()
            //     .AsSelf()
            //     .InstancePerDependency();

            // builder.RegisterType<HyCADTool.Refactored.Presentation.Views.ClusterPanel>()
            //     .AsSelf()
            //     .InstancePerDependency();

            // ===== 阶段 4: OverKill 功能 =====
            
            // 领域服务
            builder.RegisterType<LineOverKillService>()
                .AsSelf()
                .SingleInstance();
            
            // TODO: Application层删除后暂时注释掉
            // // 应用用例
            // builder.RegisterType<OverKillUseCase>()
            //     .AsSelf()
            //     .InstancePerDependency();
            
            // 仓储
            builder.RegisterType<LineRepository>()
                .As<ILineRepository>()
                .SingleInstance();

            // === Phase 2: 几何算法服务注册 ===
            // 注册 Domain 层几何算法服务
            builder.RegisterType<LineAlgorithmService>().As<ILineAlgorithmService>().SingleInstance();
            builder.RegisterType<PolygonAlgorithmService>().As<IPolygonAlgorithmService>().SingleInstance();
            builder.RegisterType<PointAlgorithmService>().As<IPointAlgorithmService>().SingleInstance();
            builder.RegisterType<GeometryConverterService>().As<IGeometryConverterService>().SingleInstance();
            
            // TODO: 后续添加更多服务注册
        }
    }
}

