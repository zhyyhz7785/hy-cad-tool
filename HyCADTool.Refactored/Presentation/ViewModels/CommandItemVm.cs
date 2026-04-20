using System.Reflection;
using System.Windows.Input;
using HyCADTool.ReCall;

namespace HyCADTool.Refactored.Presentation.ViewModels
{
    /// <summary>
    /// Blender 面板 / Ribbon / CUIX 菜单共用的命令项 ViewModel。
    /// 只读视图，不持有业务状态，从 <see cref="CommandListItem"/> 映射过来。
    /// </summary>
    public class CommandItemVm
    {
        private readonly CommandListItem _source;

        public CommandItemVm(CommandListItem source)
        {
            _source = source;
            ExecuteCommand = new RelayCommand(() => Commands.CommandDispatcher.Send(_source.Key));
        }

        public string Key => _source.Key;

        /// <summary>命令行右侧：正式命令名 + 道路类短别名（如 <c>hyRoadAlnStation  rSt</c>）。</summary>
        public string KeyCaption => RoadCommandShortAliases.FormatKeyWithShort(_source.Key);

        public string Category => _source.Category;
        public string DisplayName => _source.DisplayName;
        public string Tooltip => _source.Tooltip;
        public string Icon => _source.Icon;
        public int Order => _source.Order;

        /// <summary>Hy 面板道路 Tab 五区之一，来自 commands.json roadPanelGroup。用反射读 Entry，避免编译期绑定旧版 ReCall.dll（无 RoadPanelGroup 字段）导致 CS1061。</summary>
        public string RoadPanelGroup => TryGetRoadPanelGroup(_source.Entry);

        private static string TryGetRoadPanelGroup(CommandEntry entry)
        {
            if (entry == null) return null;
            var p = entry.GetType().GetProperty("RoadPanelGroup", BindingFlags.Public | BindingFlags.Instance);
            return p?.GetValue(entry) as string;
        }

        /// <summary>绑定到 WPF Button.Command。</summary>
        public ICommand ExecuteCommand { get; }

        /// <summary>搜索匹配用：命令 key（忽略大小写）拼接 DisplayName（支持按原文/拼音首字母过滤）。</summary>
        internal string MatchText
        {
            get
            {
                var k = _source.Key ?? string.Empty;
                var d = _source.DisplayName ?? string.Empty;
                var tail = RoadCommandShortAliases.TryGetShort(k, out var sh) ? "|" + sh : string.Empty;
                return (k + "|" + d + tail).ToLowerInvariant();
            }
        }
    }
}
