using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Presentation.ViewModels;

namespace HyCADTool.Presentation.ViewModels.Road
{
    /// <summary>
    /// 人行横道绘制参数（空隙 / 宽度 / 停止线距 / 条纹距）——
    /// 供 hyRoad / hyRoadIntersectionCrosswalk 在落图前弹窗编辑。
    /// </summary>
    public sealed class CrosswalkDrawParamsViewModel : INotifyPropertyChanged
    {
        private double _gapWidth;
        private double _crosswalkWidth;
        private double _stopLineDistance;
        private double _stripeSpacing;
        private string _statusMessage;

        public CrosswalkDrawParamsViewModel(
            double gapWidth,
            double crosswalkWidth,
            double stopLineDistance,
            double stripeSpacing)
        {
            _gapWidth = gapWidth;
            _crosswalkWidth = crosswalkWidth;
            _stopLineDistance = stopLineDistance;
            _stripeSpacing = stripeSpacing;

            ConfirmCommand = new RelayCommand(OnConfirm);
            CancelCommand = new RelayCommand(OnCancel);
            ResetCommand = new RelayCommand(OnReset);
        }

        public string Title => "人行横道参数";

        public double GapWidth
        {
            get => _gapWidth;
            set => SetField(ref _gapWidth, value);
        }

        public double CrosswalkWidth
        {
            get => _crosswalkWidth;
            set => SetField(ref _crosswalkWidth, value);
        }

        public double StopLineDistance
        {
            get => _stopLineDistance;
            set => SetField(ref _stopLineDistance, value);
        }

        public double StripeSpacing
        {
            get => _stripeSpacing;
            set => SetField(ref _stripeSpacing, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetField(ref _statusMessage, value);
        }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ResetCommand { get; }

        public event EventHandler<bool?> CloseRequested;

        private void OnConfirm()
        {
            if (GapWidth < 0 || CrosswalkWidth < 0 || StopLineDistance < 0)
            {
                StatusMessage = "空隙、横道宽度、停止线距不能为负。";
                return;
            }

            if (StripeSpacing < 1e-3)
            {
                StatusMessage = "条纹中心距须大于 0。";
                return;
            }

            CloseRequested?.Invoke(this, true);
        }

        private void OnCancel() => CloseRequested?.Invoke(this, false);

        private void OnReset()
        {
            GapWidth = Crosswalk.DefaultGapWidth;
            CrosswalkWidth = Crosswalk.DefaultWidth;
            StopLineDistance = Crosswalk.DefaultStopLineDistance;
            StripeSpacing = Crosswalk.DefaultStripeSpacing;
            StatusMessage = "已恢复国标默认";
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
