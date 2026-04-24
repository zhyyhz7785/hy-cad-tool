using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using HyCADTool.Refactored.Domain.ValueObjects.Road;

namespace HyCADTool.Refactored.Presentation.ViewModels.Road
{
    /// <summary>
    /// 绑定 <see cref="LayerFillSettings"/> 到 rCs 属性栏「平面图 / 坡面图」子面板。
    /// </summary>
    public sealed class LayerFillViewModel : INotifyPropertyChanged
    {
        private readonly LayerFillSettings _model;

        public LayerFillViewModel(LayerFillSettings model)
        {
            _model = model ?? LayerFillSettings.Empty();
        }

        internal LayerFillSettings GetModel() => _model;

        public bool PatternEnabled
        {
            get => _model.PatternEnabled;
            set
            {
                if (_model.PatternEnabled == value) return;
                _model.PatternEnabled = value;
                OnPropertyChanged();
            }
        }

        public string PatternName
        {
            get => _model.PatternName ?? string.Empty;
            set
            {
                var v = value ?? string.Empty;
                if (_model.PatternName == v) return;
                _model.PatternName = v;
                OnPropertyChanged();
            }
        }

        public double PatternScale
        {
            get => _model.PatternScale;
            set
            {
                if (Math.Abs(_model.PatternScale - value) < 1e-9) return;
                _model.PatternScale = value;
                OnPropertyChanged();
            }
        }

        public double PatternAngle
        {
            get => _model.PatternAngle;
            set
            {
                if (Math.Abs(_model.PatternAngle - value) < 1e-9) return;
                _model.PatternAngle = value;
                OnPropertyChanged();
            }
        }

        public short ColorIndex
        {
            get => _model.ColorIndex;
            set
            {
                if (_model.ColorIndex == value) return;
                _model.ColorIndex = value;
                OnPropertyChanged();
            }
        }

        public bool BlockEnabled
        {
            get => _model.BlockEnabled;
            set
            {
                if (_model.BlockEnabled == value) return;
                _model.BlockEnabled = value;
                OnPropertyChanged();
            }
        }

        public string BlockName
        {
            get => _model.BlockName ?? string.Empty;
            set
            {
                var v = value ?? string.Empty;
                if (_model.BlockName == v) return;
                _model.BlockName = v;
                OnPropertyChanged();
            }
        }

        public double BlockScale
        {
            get => _model.BlockScale;
            set
            {
                if (Math.Abs(_model.BlockScale - value) < 1e-9) return;
                _model.BlockScale = value;
                OnPropertyChanged();
            }
        }

        public double BlockRotation
        {
            get => _model.BlockRotation;
            set
            {
                if (Math.Abs(_model.BlockRotation - value) < 1e-9) return;
                _model.BlockRotation = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
