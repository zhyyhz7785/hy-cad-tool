using System.ComponentModel;
using System.Runtime.CompilerServices;
using HyCADTool.Features.TextEdit.Services;

namespace HyCADTool.Features.TextEdit.ViewModels
{
    public sealed class HyEdDialogViewModel : INotifyPropertyChanged
    {
        private string _editableText = string.Empty;
        private double _fontSizeDip = 14;

        public HyEdDialogViewModel(HyEdEntityKind kind, string handleHex, string initialText)
        {
            Kind = kind;
            HandleHex = handleHex ?? string.Empty;
            _editableText = initialText ?? string.Empty;
        }

        /// <summary>
        /// MText/MLeader 解码后保留的"前缀控制码 + 外层 brace"，编辑提交后用于把纯文本拼回完整 MText Contents。
        /// 仅 MText/MLeader 类型设置；其它类型为 null（直接读写原始 string）。
        /// </summary>
        public MTextWrap MTextWrap { get; set; }

        public HyEdEntityKind Kind { get; }

        public string HandleHex { get; }

        public string KindCaption
        {
            get
            {
                switch (Kind)
                {
                    case HyEdEntityKind.MText: return "MText";
                    case HyEdEntityKind.DBText: return "单行文字 DBText";
                    case HyEdEntityKind.MLeader: return "多重引线 MLeader";
                    case HyEdEntityKind.Dimension: return "标注 Dimension（文字替代）";
                    default: return "未知";
                }
            }
        }

        public string EditableText
        {
            get => _editableText;
            set
            {
                if (_editableText == value)
                    return;
                _editableText = value ?? string.Empty;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// 编辑框字号（DIP）。由 <see cref="HyEdScreenLocator"/> 按屏幕行高估算后写入，
        /// 让编辑框视觉大小贴近原文字。
        /// </summary>
        public double FontSizeDip
        {
            get => _fontSizeDip;
            set
            {
                if (_fontSizeDip == value)
                    return;
                _fontSizeDip = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
