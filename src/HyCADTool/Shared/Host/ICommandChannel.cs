using System;

namespace HyCADTool.Shared.Host
{
    /// <summary>
    /// 面板把动作排进 AutoCAD 命令上下文（PendingCommand → _HyExec）。
    /// Features 不再直接调用 SettingsPanelViewModel.EnqueuePendingCommand。
    /// </summary>
    public interface ICommandChannel
    {
        void EnqueuePendingCommand(Action command, string commandKey);
        void ClearPendingCommand();
        void CommitFocusedTextBoxValue();
    }
}
