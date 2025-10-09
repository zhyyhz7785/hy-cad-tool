using Autofac;
using HyCADTool.Refactored.Domain.Interfaces;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Converters;

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

            // TODO: 后续添加更多服务注册
            // - IAnchorBoltRepository
            // - Application层的 UseCases
        }
    }
}

