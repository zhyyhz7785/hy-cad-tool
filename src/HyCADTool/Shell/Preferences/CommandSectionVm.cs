using System.Collections.ObjectModel;

namespace HyCADTool.Shell.ViewModels
{
    /// <summary>Hy 面板「道路」Tab 下单个折叠分区（工程 / 路线 / …）。</summary>
    public class CommandSectionVm
    {
        public string Header { get; set; }
        public bool IsExpanded { get; set; }
        public ObservableCollection<CommandItemVm> Items { get; } = new ObservableCollection<CommandItemVm>();
    }
}
