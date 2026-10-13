using System;
using HyCADTool.Shared.Core;

namespace HyCADTool.Shared.Host
{
    /// <summary>
    /// Features 访问宿主的唯一静态入口。Shell 在插件初始化时 <see cref="Bind"/>。
    /// 业务类型工厂以 object 委托登记，避免 Shared 命名空间依赖 Features。
    /// </summary>
    public static class CadHost
    {
        [ProcessWide("插件初始化绑定一次的宿主设置面")]
        public static IHostSettings Settings { get; private set; }

        [ProcessWide("插件初始化绑定一次的命令通道")]
        public static ICommandChannel Commands { get; private set; }

        [ProcessWide("插件初始化绑定一次的面板入口")]
        public static IHostPanels Panels { get; private set; }

        [ProcessWide("插件初始化登记钢筋参数工厂")]
        public static Func<object> NewReinParameters { get; private set; }

        [ProcessWide("插件初始化登记路线默认值工厂")]
        public static Func<object> NewAlignmentDefaults { get; private set; }

        [ProcessWide("插件初始化登记桩号标注工厂")]
        public static Func<object> NewStationLabelOptions { get; private set; }

        [ProcessWide("插件初始化登记路线默认值写回")]
        public static Action<object> ApplyAlignmentDefaultsAction { get; private set; }

        [ProcessWide("插件初始化登记桩号标注写回")]
        public static Action<object> ApplyStationLabelOptionsAction { get; private set; }

        public static void Bind(
            IHostSettings settings,
            ICommandChannel commands,
            IHostPanels panels,
            Func<object> newReinParameters = null,
            Func<object> newAlignmentDefaults = null,
            Func<object> newStationLabelOptions = null,
            Action<object> applyAlignmentDefaults = null,
            Action<object> applyStationLabelOptions = null)
        {
            Settings = settings;
            Commands = commands;
            if (panels != null)
                Panels = panels;
            if (newReinParameters != null)
                NewReinParameters = newReinParameters;
            if (newAlignmentDefaults != null)
                NewAlignmentDefaults = newAlignmentDefaults;
            if (newStationLabelOptions != null)
                NewStationLabelOptions = newStationLabelOptions;
            if (applyAlignmentDefaults != null)
                ApplyAlignmentDefaultsAction = applyAlignmentDefaults;
            if (applyStationLabelOptions != null)
                ApplyStationLabelOptionsAction = applyStationLabelOptions;
        }

        public static T CreateOrDefault<T>(Func<object> factory, T fallback) where T : class
        {
            return (factory != null ? factory() as T : null) ?? fallback;
        }

        public static T Create<T>(Func<object> factory) where T : class
        {
            return factory != null ? factory() as T : null;
        }
    }
}
