using Autofac;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Domain.Services;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Interfaces;

using HyCADTool.Refactored.Domain.Services.MathAlgorithms;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Interfaces;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Converters;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Repositories;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Selection;
using HyCADTool.Refactored.Presentation;
using HyCADTool.Refactored.Presentation.Commands;
using HyCADTool.Refactored.Domain.Services.GeometryAlgorithms;
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

            // ===== 统一面板和 ViewModel 注册 =====

            // HyToolPanel（统一工具面板）
            builder.RegisterType<HyCADTool.Refactored.Presentation.Views.HyToolPanel>()
                .AsSelf()
                .InstancePerDependency();

            // ViewModel 注册
            builder.RegisterType<HyCADTool.Refactored.Presentation.ViewModels.SettingsPanelViewModel>()
                .AsSelf()
                .InstancePerDependency();
            builder.RegisterType<HyCADTool.Refactored.Presentation.ViewModels.FilterPanelViewModel>()
                .AsSelf()
                .InstancePerDependency();
            builder.RegisterType<HyCADTool.Refactored.Presentation.ViewModels.BaseReinPanelViewModel>()
                .AsSelf()
                .InstancePerDependency();
            builder.RegisterType<HyCADTool.Refactored.Presentation.ViewModels.PilePanelViewModel>()
                .AsSelf()
                .InstancePerDependency();
            builder.RegisterType<HyCADTool.Refactored.Presentation.ViewModels.ClusterPanelViewModel>()
                .AsSelf()
                .InstancePerDependency();

            // 子面板注册（作为 HyToolPanel 内嵌内容）
            builder.RegisterType<HyCADTool.Refactored.Presentation.Views.BaseReinPanel>()
                .AsSelf()
                .InstancePerDependency();
            builder.RegisterType<HyCADTool.Refactored.Presentation.Views.PilePanel>()
                .AsSelf()
                .InstancePerDependency();
            builder.RegisterType<HyCADTool.Refactored.Presentation.Views.ClusterPanel>()
                .AsSelf()
                .InstancePerDependency();
            builder.RegisterType<HyCADTool.Refactored.Presentation.Views.FilterPanel>()
                .AsSelf()
                .InstancePerDependency();

            // ===== 钢筋服务 =====

            // 基础配筋服务（单例）
            builder.RegisterType<HyCADTool.Refactored.Infrastructure.AutoCAD.Services.BaseReinforcementService>()
                .As<IBaseReinforcementService>()
                .SingleInstance();

            // 钢筋服务（单例）
            builder.RegisterType<ReinService>()
                .As<IReinService>()
                .SingleInstance();

            // 多段线偏移服务（单例）
            builder.RegisterType<AutoCadPolygonOffsetService>()
                .As<IPolygonOffsetService>()
                .SingleInstance();

            // 射线-多段线交点服务（单例）
            builder.RegisterType<AutoCadIntersectionService>()
                .As<ILineIntersectionService>()
                .SingleInstance();

            // ===== 阶段 4: OverKill 功能 =====
            
            // 领域服务
            builder.RegisterType<LineOverKillService>()
                .AsSelf()
                .SingleInstance();
            
            builder.RegisterType<CurveBreakService>()
                .AsSelf()
                .SingleInstance();
            
            builder.RegisterType<CurveIntersectionService>()
                .AsSelf()
                .SingleInstance();
            
            builder.RegisterType<CurveSegmentService>()
                .AsSelf()
                .SingleInstance();
            
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
            
            // === Phase 2.2: 选择服务重构 ===
            // 注册过滤器管理服务
            builder.RegisterType<FilterManagerService>().As<IFilterManagerService>().SingleInstance();
            
            // 注册高级选择服务
            builder.RegisterType<AdvancedSelectionService>().As<IAdvancedSelectionService>().SingleInstance();

            // === 阶段 10: 聚类与边界框服务 ===
            // 聚类算法服务
            builder.RegisterType<ClusteringService>().As<IClusteringService>().SingleInstance();

            // === 阶段 11: DCEL 构建服务 ===
            // DCEL 双连接边表构建服务
            builder.RegisterType<DCELBuilderService>().As<IDCELBuilderService>().SingleInstance();
            
            // DCEL 曲线线段提取服务
            builder.RegisterType<CurveSegmentExtractor>().As<ICurveSegmentExtractor>().SingleInstance();
            
            // DCEL 渲染服务
            builder.RegisterType<DCELRenderer>().As<IDCELRenderer>().SingleInstance();
            
            // DCEL 命令
            builder.RegisterType<HyCADTool.Refactored.Presentation.Commands.DCELCommand>()
                .AsSelf()
                .InstancePerDependency();

            // === 阶段 7: OverKill 命令 ===
            // OverKill 命令（线段清理）
            builder.RegisterType<HyCADTool.Refactored.Presentation.Commands.OverKillCommand>()
                .AsSelf()
                .InstancePerDependency();
            
            // === 阶段 12: HY3 三维建模服务 ===
            // 三维几何构建器
            builder.RegisterType<Geometry3DBuilder>()
                .As<IGeometry3DBuilder>()
                .SingleInstance();

            // 3D 实体构建器
            builder.RegisterType<Solid3DBuilder>()
                .As<ISolid3DBuilder>()
                .SingleInstance();

            // 图层管理器
            builder.RegisterType<LayerManager>()
                .As<ILayerManager>()
                .SingleInstance();
            
            // === 阶段 13: HYBC/HYOV 重构服务 ===
            builder.RegisterType<HyCADTool.Refactored.Infrastructure.AutoCAD.Services.MarkerLayerService>()
                .AsSelf()
                .SingleInstance();
            
            builder.RegisterGeneric(typeof(HyCADTool.Refactored.Domain.Services.Geometry.SpatialIndexService<>))
                .AsSelf()
                .SingleInstance();

            // === 地理空间服务 ===
            builder.RegisterType<HyCADTool.Refactored.Domain.Services.GeospatialService>()
                .As<HyCADTool.Refactored.Domain.Interfaces.IGeospatialService>()
                .SingleInstance();

            // === 阶段 14: 桩布置与 Voronoi 优化服务 ===
            builder.RegisterType<VoronoiOptimizationService>()
                .AsSelf()
                .SingleInstance();

            // 桩布置计算服务（Domain）
            builder.RegisterType<PileLayoutService>()
                .AsSelf()
                .SingleInstance();

            // 桩绘制服务（Infrastructure）
            builder.RegisterType<HyCADTool.Refactored.Infrastructure.AutoCAD.Services.PileDrawingService>()
                .AsSelf()
                .SingleInstance();

        }
    }
}

