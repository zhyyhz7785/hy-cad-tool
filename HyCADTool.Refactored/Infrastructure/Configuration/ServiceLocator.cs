using Autofac;
using System;

namespace HyCADTool.Refactored.Infrastructure.Configuration
{
    /// <summary>
    /// 服务定位器（Service Locator）
    /// 用于在 AutoCAD 命令中访问依赖注入容器
    /// 注意：这是一个反模式，但在 AutoCAD 命令的静态方法中是必要的妥协
    /// </summary>
    public static class ServiceLocator
    {
        private static IContainer _container;

        /// <summary>
        /// 获取依赖注入容器
        /// </summary>
        public static IContainer Container
        {
            get
            {
                if (_container == null)
                    throw new InvalidOperationException(
                        "Container has not been initialized. Call Initialize() first.");
                return _container;
            }
        }

        /// <summary>
        /// 初始化容器
        /// </summary>
        /// <param name="container">Autofac 容器实例</param>
        public static void Initialize(IContainer container)
        {
            if (container == null)
                throw new ArgumentNullException(nameof(container));

            _container = container;
        }

        /// <summary>
        /// 重置容器（用于测试或重新加载）
        /// </summary>
        public static void Reset()
        {
            _container?.Dispose();
            _container = null;
        }

        /// <summary>
        /// 解析服务
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        /// <returns>服务实例</returns>
        public static T Resolve<T>()
        {
            return Container.Resolve<T>();
        }

        /// <summary>
        /// 尝试解析服务（仅支持引用类型）
        /// </summary>
        /// <typeparam name="T">服务类型（必须是引用类型）</typeparam>
        /// <param name="service">解析的服务实例（如果成功）</param>
        /// <returns>解析成功返回 true</returns>
        public static bool TryResolve<T>(out T service) where T : class
        {
            return Container.TryResolve(out service);
        }
        
        /// <summary>
        /// 尝试解析服务（支持所有类型）
        /// </summary>
        /// <typeparam name="T">服务类型</typeparam>
        /// <returns>服务实例，如果解析失败返回 default(T)</returns>
        public static T TryResolve<T>()
        {
            try
            {
                return Container.Resolve<T>();
            }
            catch
            {
                return default(T);
            }
        }
    }
}

