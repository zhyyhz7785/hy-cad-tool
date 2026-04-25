using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HyCADTool.Domain.ValueObjects.Configuration.User
{
    /// <summary>
    /// 用户可编辑的一条图层定义（语义 ID + 显示/落图用名 + 颜色与可选线型线宽等）。
    /// </summary>
    public sealed class LayerDefinitionItem : INotifyPropertyChanged
    {
        private string _semanticId = "";
        private string _name = "";
        private short _aciColor = 7;
        private string _linetypeName = "Continuous";
        /// <summary>-1 表示 ByLayer（与 AutoCAD LineWeight.ByLine 一致）。</summary>
        private int _lineWeightRaw = -1;
        private bool _isPlottable = true;
        private bool _isLocked;

        public string SemanticId
        {
            get => _semanticId;
            set { if (_semanticId == value) return; _semanticId = value ?? ""; OnPropertyChanged(); }
        }

        public string Name
        {
            get => _name;
            set { if (_name == value) return; _name = value ?? ""; OnPropertyChanged(); }
        }

        public short AciColor
        {
            get => _aciColor;
            set { if (_aciColor == value) return; _aciColor = value; OnPropertyChanged(); }
        }

        public string LinetypeName
        {
            get => _linetypeName;
            set { if (_linetypeName == value) return; _linetypeName = string.IsNullOrWhiteSpace(value) ? "Continuous" : value.Trim(); OnPropertyChanged(); }
        }

        public int LineWeightRaw
        {
            get => _lineWeightRaw;
            set { if (_lineWeightRaw == value) return; _lineWeightRaw = value; OnPropertyChanged(); }
        }

        public bool IsPlottable
        {
            get => _isPlottable;
            set { if (_isPlottable == value) return; _isPlottable = value; OnPropertyChanged(); }
        }

        public bool IsLocked
        {
            get => _isLocked;
            set { if (_isLocked == value) return; _isLocked = value; OnPropertyChanged(); }
        }

        public LayerDefinitionItem Clone()
        {
            return new LayerDefinitionItem
            {
                SemanticId = SemanticId,
                Name = Name,
                AciColor = AciColor,
                LinetypeName = LinetypeName,
                LineWeightRaw = LineWeightRaw,
                IsPlottable = IsPlottable,
                IsLocked = IsLocked
            };
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
