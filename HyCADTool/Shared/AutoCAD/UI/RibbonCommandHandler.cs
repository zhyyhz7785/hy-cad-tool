using System.Windows.Input;
using HyCADTool.Presentation.Commands;

namespace HyCADTool.Shared.AutoCAD.UI
{
    /// <summary>
    /// Ribbon 按钮的 <see cref="ICommand"/> 适配器：转发到统一分发器 <see cref="CommandDispatcher"/>。
    /// AutoCAD Ribbon 要求每个 RibbonButton 都关联一个 ICommand；我们把 commands.json 里的 key
    /// 作为构造参数记下来，点击时通过 SendStringToExecute 走正常 AutoCAD 命令流程。
    /// </summary>
    public sealed class RibbonCommandHandler : ICommand
    {
        private readonly string _key;

        public RibbonCommandHandler(string key) { _key = key; }

        public bool CanExecute(object parameter) => !string.IsNullOrEmpty(_key);

        public void Execute(object parameter) => CommandDispatcher.Send(_key);

        public event System.EventHandler CanExecuteChanged
        {
            add { }
            remove { }
        }
    }
}
