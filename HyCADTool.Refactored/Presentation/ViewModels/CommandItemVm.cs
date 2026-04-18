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
        public string Category => _source.Category;
        public string DisplayName => _source.DisplayName;
        public string Tooltip => _source.Tooltip;
        public string Icon => _source.Icon;
        public int Order => _source.Order;

        /// <summary>绑定到 WPF Button.Command。</summary>
        public ICommand ExecuteCommand { get; }

        /// <summary>搜索匹配用：命令 key（忽略大小写）拼接 DisplayName（支持按原文/拼音首字母过滤）。</summary>
        internal string MatchText => ((_source.Key ?? string.Empty) + "|" + (_source.DisplayName ?? string.Empty)).ToLowerInvariant();
    }
}
