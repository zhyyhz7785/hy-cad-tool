using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace HyCADTool.Shell.ViewModels
{
    /// <summary>
    /// 规则查询条件行（对标 ArcGIS Pro 子句构建器单行）。
    /// </summary>
    public sealed class RuleConditionItem : INotifyPropertyChanged
    {
        private string _fieldDisplay;
        private string _propertyName;
        private string _operator;
        private string _value;
        private string _connector = "与";
        private bool _isLast = true;

        public string FieldDisplay
        {
            get => _fieldDisplay;
            set { _fieldDisplay = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayText)); }
        }

        public string PropertyName
        {
            get => _propertyName;
            set { _propertyName = value; OnPropertyChanged(); }
        }

        public string Operator
        {
            get => _operator;
            set { _operator = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayText)); }
        }

        public string Value
        {
            get => _value;
            set { _value = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayText)); }
        }

        /// <summary>与下一行的连接符；最后一行不显示。</summary>
        public string Connector
        {
            get => _connector;
            set { _connector = value; OnPropertyChanged(); }
        }

        public bool IsLast
        {
            get => _isLast;
            set { _isLast = value; OnPropertyChanged(); }
        }

        public string DisplayText => $"{FieldDisplay} {Operator} {Value}";

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
