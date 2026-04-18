using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Windows.Input;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Road;

namespace HyCADTool.Refactored.Presentation.ViewModels.Road
{
    /// <summary>
    /// "横断绘制"窗口（v2 BlenderUI 风格四区布局）的 ViewModel。
    ///
    /// <para>职责（在 <see cref="CrossSectionDesignerViewModel"/> 之上扩展）：</para>
    /// <list type="bullet">
    ///   <item>底部 StatusBar：实时汇总左/中分带/右板块数 + 总宽 + 规范通过率。</item>
    ///   <item>左 Outliner / 右 PropertyEditor 折叠状态（<see cref="IsOutlinerVisible"/> /
    ///       <see cref="IsPropertyPaneVisible"/>）。</item>
    ///   <item>"复制选中条带"/"粘贴条带"命令（提升复用工作流效率）。</item>
    ///   <item><see cref="DrawCommand"/>：与 ConfirmCommand 等价（语义命名）。</item>
    /// </list>
    ///
    /// <para>预览 / 重算 / 命令路由 / 镜像 / 规范检查 全部沿用基类，本类不重复实现。</para>
    /// </summary>
    public sealed class CrossSectionDrawViewModel : CrossSectionDesignerViewModel
    {
        public CrossSectionDrawViewModel(CrossSectionLayout initialLayout = null, Guid? existingTemplateId = null)
            : base(initialLayout, existingTemplateId)
        {
            CopySelectedBandCommand = new RelayCommand(ExecuteCopySelected, () => SelectedBand != null);
            PasteBandCommand = new RelayCommand(ExecutePasteToSelectedSide, () => _bandClipboard != null);
            ToggleOutlinerCommand = new RelayCommand(() => IsOutlinerVisible = !IsOutlinerVisible);
            TogglePropertyPaneCommand = new RelayCommand(() => IsPropertyPaneVisible = !IsPropertyPaneVisible);
            DrawCommand = ConfirmCommand;

            PreviewRequested += (_, figure) => UpdateStatus(figure);
            PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(SelectedBand))
                {
                    OnPropertyChanged(nameof(SelectedSideLabel));
                }
            };

            UpdateStatus(LastFigure);
        }

        // ============================== UI 折叠状态 ==============================

        private bool _isOutlinerVisible = true;
        public bool IsOutlinerVisible
        {
            get => _isOutlinerVisible;
            set => SetProperty(ref _isOutlinerVisible, value);
        }

        private bool _isPropertyPaneVisible = true;
        public bool IsPropertyPaneVisible
        {
            get => _isPropertyPaneVisible;
            set => SetProperty(ref _isPropertyPaneVisible, value);
        }

        // ============================== 状态文本 ==============================

        private string _statusMessage = string.Empty;
        /// <summary>底部状态栏文本：板块数 / 总宽 / 规范通过状况。</summary>
        public string StatusMessage
        {
            get => _statusMessage;
            private set => SetProperty(ref _statusMessage, value ?? string.Empty);
        }

        /// <summary>UI 显示用：当前选中条带所在侧别的中文标签。</summary>
        public string SelectedSideLabel
        {
            get
            {
                if (SelectedBand == null) return "未选中";
                switch (SelectedBand.Side)
                {
                    case BandSide.Left: return "左半";
                    case BandSide.Right: return "右半";
                    case BandSide.Center: return "中央";
                    default: return SelectedBand.Side.ToString();
                }
            }
        }

        // ============================== 复制 / 粘贴条带 ==============================

        private CrossSectionBand? _bandClipboard;

        /// <summary>UI 绑定（按钮 IsEnabled）：剪贴板是否有可粘贴条带。</summary>
        public bool HasClipboard => _bandClipboard.HasValue;

        public ICommand CopySelectedBandCommand { get; }
        public ICommand PasteBandCommand { get; }
        public ICommand ToggleOutlinerCommand { get; }
        public ICommand TogglePropertyPaneCommand { get; }

        /// <summary>语义化别名：与 <see cref="CrossSectionDesignerViewModel.ConfirmCommand"/> 同一命令实例。</summary>
        public ICommand DrawCommand { get; }

        private void ExecuteCopySelected()
        {
            if (SelectedBand == null) return;
            _bandClipboard = SelectedBand.ToBand();
            OnPropertyChanged(nameof(HasClipboard));
        }

        private void ExecutePasteToSelectedSide()
        {
            if (_bandClipboard == null) return;
            var band = _bandClipboard.Value;
            // 粘贴到当前选中条带所在侧；未选中时默认左半。
            BandSide targetSide;
            if (SelectedBand?.Side == BandSide.Right) targetSide = BandSide.Right;
            else if (SelectedBand?.Side == BandSide.Left) targetSide = BandSide.Left;
            else targetSide = BandSide.Left;

            var collection = targetSide == BandSide.Right ? RightBands : LeftBands;
            var newRow = new BandRowViewModel(band.WithSide(targetSide));
            collection.Add(newRow);
            SelectedBand = newRow;
        }

        // ============================== 状态计算 ==============================

        private void UpdateStatus(CrossSectionFigure figure)
        {
            var sb = new StringBuilder();
            sb.Append("L=").Append(LeftBands.Count)
              .Append(" / 中=").Append(CenterMedianWidth > 0 ? "1" : "0")
              .Append(" / R=").Append(RightBands.Count);
            sb.Append("  总宽 ").Append(TotalWidth.ToString("F2", CultureInfo.InvariantCulture)).Append(" m");

            int passed = CheckItems.Count(i => i.Passed);
            int total = CheckItems.Count;
            sb.Append("  规范 ").Append(passed).Append("/").Append(total);
            sb.Append(AllChecksPassed ? "（全部通过）" : "（存在未通过项）");

            if (figure != null)
            {
                sb.Append("  顶点 ").Append(figure.Vertices.Count);
                sb.Append("  面板 ").Append(figure.Panels.Count);
            }

            StatusMessage = sb.ToString();
        }
    }
}
