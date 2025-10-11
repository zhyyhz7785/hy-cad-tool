using Autofac;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Domain.Services;
using HyCADTool.Refactored.Application.UseCases.OverKill;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services;
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

            // 注册配置服务（单例）
            builder.RegisterType<ConfigurationService>()
                .As<IConfigurationService>()
                .SingleInstance();

            // ===== 阶段 5.0: UI 基础设施 =====
            
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
            builder.RegisterType<HyCADTool.Refactored.Presentation.Views.ReinPanel>()
                .AsSelf()
                .InstancePerDependency();

            builder.RegisterType<HyCADTool.Refactored.Presentation.Views.FilterPanel>()
                .AsSelf()
                .InstancePerDependency();

            // ViewModel 注册（按需创建）
            builder.RegisterType<HyCADTool.Refactored.Presentation.ViewModels.FilterPanelViewModel>()
                .AsSelf()
                .InstancePerDependency();

            builder.RegisterType<HyCADTool.Refactored.Presentation.ViewModels.BaseReinPanelViewModel>()
                .AsSelf()
                .InstancePerDependency();

            // 阶段 5.0.4: BaseReinPanel 及其服务
            builder.RegisterType<HyCADTool.Refactored.Presentation.Views.BaseReinPanel>()
                .AsSelf()
                .InstancePerDependency();

            builder.RegisterType<HyCADTool.Refactored.Application.Services.BaseReinforcementService>()
                .As<IBaseReinforcementService>()
                .SingleInstance();

            // 阶段 5.0.5: PilePanel（迁移自原项目）
            builder.RegisterType<HyCADTool.Refactored.Presentation.ViewModels.PilePanelViewModel>()
                .AsSelf()
                .InstancePerDependency();

            builder.RegisterType<HyCADTool.Refactored.Presentation.Views.PilePanel>()
                .AsSelf()
                .InstancePerDependency();

            // 阶段 5.0.5: ClusterPanel（迁移自原项目）
            builder.RegisterType<HyCADTool.Refactored.Presentation.ViewModels.ClusterPanelViewModel>()
                .AsSelf()
                .InstancePerDependency();

            builder.RegisterType<HyCADTool.Refactored.Presentation.Views.ClusterPanel>()
                .AsSelf()
                .InstancePerDependency();

            // 原项目接口适配器（用于 PilePanel 和 ClusterPanel 兼容性）
            builder.RegisterType<HyCADTool.Refactored.Infrastructure.AutoCAD.Adapters.CadServiceAdapter>()
                .As<HyCADTool.Interfaces.ICadService>()
                .SingleInstance();

            builder.RegisterType<HyCADTool.Refactored.Infrastructure.AutoCAD.Adapters.AreaFactoryAdapter>()
                .As<HyCADTool.Interfaces.IAreaFactory>()
                .SingleInstance();

            builder.RegisterType<HyCADTool.Refactored.Infrastructure.AutoCAD.Adapters.ConfigServiceAdapter>()
                .As<HyCADTool.Interfaces.IConfigService>()
                .SingleInstance();

            // 阶段 5.0.6: ReinPanel（钢筋面板）
            builder.RegisterType<HyCADTool.Refactored.Presentation.ViewModels.ReinPanelViewModel>()
                .AsSelf()
                .InstancePerDependency();

            builder.RegisterType<HyCADTool.Refactored.Presentation.Views.ReinPanel>()
                .AsSelf()
                .InstancePerDependency();

            builder.RegisterType<HyCADTool.Refactored.Infrastructure.AutoCAD.Adapters.ReinServiceAdapter>()
                .As<HyCADTool.Refactored.Domain.Services.IReinService>()
                .SingleInstance();

            // ===== 阶段 4: OverKill 功能 =====
            
            // 领域服务
            builder.RegisterType<LineOverKillService>()
                .AsSelf()
                .SingleInstance();
            
            // 应用用例
            builder.RegisterType<OverKillUseCase>()
                .AsSelf()
                .InstancePerDependency();
            
            // 仓储
            builder.RegisterType<LineRepository>()
                .As<ILineRepository>()
                .SingleInstance();

            // TODO: 后续添加更多服务注册
        }
    }
}

