using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using HyCADTool.Shared.AutoCAD.Services.Road;

namespace HyCADTool.Presentation.ViewModels.Road
{
    /// <summary>
    /// <c>hyRoadAlnStation</c> 的 C 分支（配置）使用的 ViewModel。
    /// 把 <see cref="RoadStationLabelOptions"/> 暴露给 BlenderWindow 做双向绑定。
    /// </summary>
    public sealed class StationLabelConfigViewModel : INotifyPropertyChanged
    {
        private double _mainInterval;
        private double _subInterval;
        private double _tickLengthMain;
        private double _tickLengthSub;
        private double _textHeight;
        private double _textMargin;
        private bool _rotateTextAlongTangent;
        private bool _sideIsLeft;
        private string _statusMessage;

        public StationLabelConfigViewModel(RoadStationLabelOptions initial)
        {
            var o = initial ?? RoadStationLabelOptions.Default;
            _mainInterval = o.MainInterval;
            _subInterval = o.SubInterval;
            _tickLengthMain = o.TickLengthMain;
            _tickLengthSub = o.TickLengthSub;
            _textHeight = o.TextHeight;
            _textMargin = o.TextMargin;
            _rotateTextAlongTangent = o.RotateTextAlongTangent;
            _sideIsLeft = o.TextSide == StationTextSide.Left;

            ConfirmCommand = new RelayCommand(OnConfirm);
            CancelCommand = new RelayCommand(OnCancel);
            ResetCommand = new RelayCommand(OnReset);
        }

        public string Title => "桩号标注配置（hyRoadAlnStation / C）";

        public double MainInterval { get => _mainInterval; set => SetField(ref _mainInterval, value); }
        public double SubInterval { get => _subInterval; set => SetField(ref _subInterval, value); }
        public double TickLengthMain { get => _tickLengthMain; set => SetField(ref _tickLengthMain, value); }
        public double TickLengthSub { get => _tickLengthSub; set => SetField(ref _tickLengthSub, value); }
        public double TextHeight { get => _textHeight; set => SetField(ref _textHeight, value); }
        public double TextMargin { get => _textMargin; set => SetField(ref _textMargin, value); }
        public bool RotateTextAlongTangent { get => _rotateTextAlongTangent; set => SetField(ref _rotateTextAlongTangent, value); }
        public bool SideIsLeft { get => _sideIsLeft; set => SetField(ref _sideIsLeft, value); }
        public string StatusMessage { get => _statusMessage; set => SetField(ref _statusMessage, value); }

        public ICommand ConfirmCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ResetCommand { get; }

        public event EventHandler<bool?> CloseRequested;

        public RoadStationLabelOptions Snapshot() => new RoadStationLabelOptions
        {
            MainInterval = MainInterval,
            SubInterval = SubInterval,
            TickLengthMain = TickLengthMain,
            TickLengthSub = TickLengthSub,
            TextHeight = TextHeight,
            TextMargin = TextMargin,
            RotateTextAlongTangent = RotateTextAlongTangent,
            TextSide = SideIsLeft ? StationTextSide.Left : StationTextSide.Right,
        };

        private void OnConfirm()
        {
            try { Snapshot().Validate(); }
            catch (Exception ex) { StatusMessage = "校验失败：" + ex.Message; return; }
            CloseRequested?.Invoke(this, true);
        }

        private void OnCancel() => CloseRequested?.Invoke(this, false);

        private void OnReset()
        {
            var o = RoadStationLabelOptions.Default;
            MainInterval = o.MainInterval;
            SubInterval = o.SubInterval;
            TickLengthMain = o.TickLengthMain;
            TickLengthSub = o.TickLengthSub;
            TextHeight = o.TextHeight;
            TextMargin = o.TextMargin;
            RotateTextAlongTangent = o.RotateTextAlongTangent;
            SideIsLeft = o.TextSide == StationTextSide.Left;
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
