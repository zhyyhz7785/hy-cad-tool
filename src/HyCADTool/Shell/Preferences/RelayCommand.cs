using System;
using System.Windows.Input;

namespace HyCADTool.Presentation.ViewModels
{
    /// <summary>
    /// 简单的 ICommand 实现，用于 MVVM 模式的命令绑定。
    ///
    /// <para>CanExecuteChanged 设计说明（2026-04-21 修）：</para>
    /// <list type="bullet">
    ///   <item>历史上本类的 <c>CanExecuteChanged</c> 是空 stub（注释写"不注册，避免 WPF CommandManager 依赖"），
    ///         代价是 <c>Button.IsEnabled</c> 只在首次绑定时评估一次，
    ///         后续 ViewModel 属性变化（如 <c>SelectedPi</c>）不会触发重评估 —— 路线工作台底栏「应用 / 撤销」
    ///         在切换 PI 后仍保持灰色即源于此。</item>
    ///   <item>修复策略：保留「不依赖 <c>CommandManager.RequerySuggested</c> 自动轮询」的原意，
    ///         改为「暴露手动事件 + <see cref="RaiseCanExecuteChanged"/>」，
    ///         让 ViewModel 在相关属性 setter 里显式刷新。不回退到 CommandManager，避免 AutoCAD 宿主下
    ///         PaletteSet / ShowModelessWindow 时序里 CommandManager 的异步轮询噪声。</item>
    /// </list>
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool> _canExecute;

        public RelayCommand(Action execute, Func<bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute ?? (() => true);
        }

        public bool CanExecute(object parameter) => _canExecute();

        public void Execute(object parameter) => _execute();

        public event EventHandler CanExecuteChanged;

        /// <summary>
        /// 通知绑定方（典型是 WPF <see cref="System.Windows.Controls.Button"/>）重新评估 <see cref="CanExecute"/>。
        /// ViewModel 在会影响按钮启用条件的属性 setter 里调用。
        /// </summary>
        public void RaiseCanExecuteChanged()
            => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 带参数的 RelayCommand，<typeparamref name="T"/> 来自 XAML 的 CommandParameter。
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> _execute;
        private readonly Predicate<T> _canExecute;

        public RelayCommand(Action<T> execute, Predicate<T> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            if (_canExecute == null) return true;
            return _canExecute(Cast(parameter));
        }

        public void Execute(object parameter) => _execute(Cast(parameter));

        private static T Cast(object p)
        {
            if (p == null && !typeof(T).IsValueType) return default;
            if (p is T t) return t;
            return default;
        }

        public event EventHandler CanExecuteChanged;

        /// <summary>手动触发 <see cref="CanExecuteChanged"/>，用法同 <see cref="RelayCommand.RaiseCanExecuteChanged"/>。</summary>
        public void RaiseCanExecuteChanged()
            => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
    }
}
