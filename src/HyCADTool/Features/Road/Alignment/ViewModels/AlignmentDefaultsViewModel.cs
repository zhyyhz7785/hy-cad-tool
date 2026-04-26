using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyCADTool.Domain.ValueObjects.Road;

using HyCADTool.Presentation.ViewModels;
namespace HyCADTool.Features.Road.PlanAlignment.ViewModels
{
    /// <summary>
    /// <c>hyRoadAlnDefaults</c> 命令对应的 ViewModel。
    ///
    /// 承担：把 <see cref="AlignmentDefaults"/> 暴露给 BlenderWindow 做 4 个 NumericSlider 双绑，
    /// 并通过 <see cref="CloseRequested"/> 通知窗口关闭；窗口关闭后由命令层根据 DialogResult
    /// 决定是否写回 <see cref="SettingsPanelViewModel.ApplyAlignmentDefaults"/> 并持久化。
    /// </summary>
    public sealed class AlignmentDefaultsViewModel : INotifyPropertyChanged
    {
        private double _defaultRadius;
        private double _defaultSpiralIn;
        private double _defaultSpiralOut;
        private double _defaultStartStation;
        private string _statusMessage;

        public AlignmentDefaultsViewModel(AlignmentDefaults initial)
        {
            var d = initial ?? new AlignmentDefaults();
            _defaultRadius = d.DefaultRadius;
            _defaultSpiralIn = d.DefaultSpiralIn;
            _defaultSpiralOut = d.DefaultSpiralOut;
            _defaultStartStation = d.DefaultStartStation;

            ConfirmCommand = new RelayCommand(OnConfirm);
            CancelCommand = new RelayCommand(OnCancel);
            ResetCommand = new RelayCommand(OnReset);
        }

        public string Title => "平面线位默认值（hyRoadAlnDefaults）";

        public double DefaultRadius
        {
            get => _defaultRadius;
            set => SetField(ref _defaultRadius, value);
        }

        public double DefaultSpiralIn
        {
            get => _defaultSpiralIn;
            set => SetField(ref _defaultSpiralIn, value);
        }

        public double DefaultSpiralOut
        {
            get => _defaultSpiralOut;
            set => SetField(ref _defaultSpiralOut, value);
        }

        public double DefaultStartStation
        {
            get => _defaultStartStation;
            set => SetField(ref _defaultStartStation, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetField(ref _statusMessage, value);
        }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ResetCommand { get; }

        /// <summary>窗口关闭事件：true=应用，false/null=取消。</summary>
        public event EventHandler<bool?> CloseRequested;

        /// <summary>把当前 UI 值快照为 Domain VO（窗口关闭后命令层读）。</summary>
        public AlignmentDefaults Snapshot() => new AlignmentDefaults
        {
            DefaultRadius = DefaultRadius,
            DefaultSpiralIn = DefaultSpiralIn,
            DefaultSpiralOut = DefaultSpiralOut,
            DefaultStartStation = DefaultStartStation,
        };

        private void OnConfirm()
        {
            try
            {
                Snapshot().Validate();
            }
            catch (Exception ex)
            {
                StatusMessage = "校验失败：" + ex.Message;
                return;
            }
            CloseRequested?.Invoke(this, true);
        }

        private void OnCancel() => CloseRequested?.Invoke(this, false);

        private void OnReset()
        {
            var d = new AlignmentDefaults();
            DefaultRadius = d.DefaultRadius;
            DefaultSpiralIn = d.DefaultSpiralIn;
            DefaultSpiralOut = d.DefaultSpiralOut;
            DefaultStartStation = d.DefaultStartStation;
            StatusMessage = "已恢复出厂默认";
        }

        private bool SetField<T>(ref T field, T value, [CallerMemberName] string name = null)
        {
            if (Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(name);
            return true;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        private void OnPropertyChanged([CallerMemberName] string name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
