using System;
using System.Windows.Input;

namespace HyCADTool.Refactored.Presentation.ViewModels
{
    /// <summary>
    /// 简单的 ICommand 实现，用于 MVVM 模式的命令绑定
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

        public event EventHandler CanExecuteChanged
        {
            add { } // 不注册，避免 WPF CommandManager 依赖
            remove { }
        }
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

        public event EventHandler CanExecuteChanged
        {
            add { }
            remove { }
        }
    }
}

