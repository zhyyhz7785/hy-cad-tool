using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HyCAD.BlenderUI.Controls;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using HyCADTool.Refactored.Presentation.ViewModels.Road;

namespace HyCADTool.Refactored.Presentation.Views.Road
{
    /// <summary>
    /// "横断面绘制" v2 窗口（BlenderWindow 子类）。
    ///
    /// <para>
    /// 与 v1 的 <see cref="CrossSectionDesignerWindow"/> 相比：
    /// <list type="bullet">
    ///   <item>采用 BlenderUI 的 workbench 拓扑（顶 EditorHeader / 左 Outliner / 中 Toolbar+Canvas / 右 PropertyEditor / 底 StatusBar）；</item>
    ///   <item>左右侧栏可独立折叠；条带选择走 ListBox（Outliner）而非 DataGrid；</item>
    ///   <item>属性面板按"选中条带 / 外侧路牙 / 内侧路牙 / 全局 / 桩号 / 规范检查"分组，覆盖 Stage 1 新增的 v2 字段；</item>
    ///   <item>预览渲染走共享 <see cref="CrossSectionPreviewRenderer"/>，与旧窗口一致。</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// 数据流：DataContext = <see cref="CrossSectionDrawViewModel"/>（继承 <see cref="CrossSectionDesignerViewModel"/>），
    /// 业务命令 / 预览数据全在 VM 里，本窗口只负责 UI 拓扑、键盘快捷键、Canvas 绘制委托与 MessageBox 注入。
    /// </para>
    /// </summary>
    public partial class CrossSectionDrawWindow : BlenderWindow
    {
        private CrossSectionDrawViewModel _vm;
        private CrossSectionFigure _currentFigure;
        private GridLength _outlinerWidth;
        private GridLength _outlinerSplitterWidth;
        private GridLength _propertyWidth;
        private GridLength _propertySplitterWidth;

        public CrossSectionDrawWindow()
        {
            InitializeComponent();
            KeyDown += OnWindowKeyDown;
            CloseClicked += OnCloseClicked;
        }

        public CrossSectionDrawWindow(CrossSectionDrawViewModel viewModel) : this()
        {
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));
            _vm = viewModel;
            DataContext = viewModel;

            // 缓存初始侧栏宽度，便于折叠/展开时还原
            _outlinerWidth = OutlinerColumn.Width;
            _outlinerSplitterWidth = OutlinerSplitterColumn.Width;
            _propertyWidth = PropertyColumn.Width;
            _propertySplitterWidth = PropertySplitterColumn.Width;

            viewModel.PreviewRequested += OnPreviewRequested;
            viewModel.CloseRequested += OnCloseRequested;
            viewModel.PropertyChanged += OnVmPropertyChanged;

            viewModel.NonCompliantConfirm = summary => MessageBox.Show(
                this,
                summary,
                "存在未通过的规范项",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Warning,
                MessageBoxResult.Cancel) == MessageBoxResult.OK;

            Loaded += (_, __) =>
            {
                _currentFigure = viewModel.LastFigure;
                ApplyPanelVisibility();
                RedrawPreview();
            };

            Closed += (_, __) =>
            {
                viewModel.PreviewRequested -= OnPreviewRequested;
                viewModel.CloseRequested -= OnCloseRequested;
                viewModel.PropertyChanged -= OnVmPropertyChanged;
                CloseClicked -= OnCloseClicked;
            };
        }

        // =========================================================================
        //  关闭
        // =========================================================================

        private void OnCloseRequested(object sender, bool? dialogResult)
        {
            RequestClose(dialogResult);
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            if (_vm != null && _vm.CancelCommand.CanExecute(null))
            {
                _vm.CancelCommand.Execute(null);
                e.Handled = true;
            }
        }

        // =========================================================================
        //  侧栏折叠 / 展开
        // =========================================================================

        private void OnVmPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_vm == null) return;

            if (e.PropertyName == nameof(CrossSectionDrawViewModel.IsOutlinerVisible)
                || e.PropertyName == nameof(CrossSectionDrawViewModel.IsPropertyPaneVisible))
            {
                ApplyPanelVisibility();
            }
        }

        private void ApplyPanelVisibility()
        {
            if (_vm == null) return;

            if (_vm.IsOutlinerVisible)
            {
                OutlinerColumn.Width = _outlinerWidth;
                OutlinerSplitterColumn.Width = _outlinerSplitterWidth;
                OutlinerPanel.Visibility = Visibility.Visible;
            }
            else
            {
                OutlinerColumn.Width = new GridLength(0);
                OutlinerSplitterColumn.Width = new GridLength(0);
                OutlinerPanel.Visibility = Visibility.Collapsed;
            }

            if (_vm.IsPropertyPaneVisible)
            {
                PropertyColumn.Width = _propertyWidth;
                PropertySplitterColumn.Width = _propertySplitterWidth;
                PropertyPanel.Visibility = Visibility.Visible;
            }
            else
            {
                PropertyColumn.Width = new GridLength(0);
                PropertySplitterColumn.Width = new GridLength(0);
                PropertyPanel.Visibility = Visibility.Collapsed;
            }
        }

        // =========================================================================
        //  键盘快捷键
        // =========================================================================

        private void OnWindowKeyDown(object sender, KeyEventArgs e)
        {
            if (_vm == null) return;

            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (_vm.ConfirmCommand.CanExecute(null)) _vm.ConfirmCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                if (_vm.CancelCommand.CanExecute(null)) _vm.CancelCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                _vm.LoadLayout(Domain.Services.Road.CrossSectionPresets.CreateCjj37UrbanArterial());
                e.Handled = true;
            }
            else if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                PresetCombo.IsDropDownOpen = true;
                PresetCombo.Focus();
                e.Handled = true;
            }
            else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (_vm.CopySelectedBandCommand.CanExecute(null)) _vm.CopySelectedBandCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (_vm.PasteBandCommand.CanExecute(null)) _vm.PasteBandCommand.Execute(null);
                e.Handled = true;
            }
        }

        // =========================================================================
        //  顶部菜单按钮
        // =========================================================================

        private void NewMenu_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            _vm.LoadLayout(Domain.Services.Road.CrossSectionPresets.CreateCjj37UrbanArterial());
        }

        private void LoadPresetMenu_Click(object sender, RoutedEventArgs e)
        {
            PresetCombo.IsDropDownOpen = true;
            PresetCombo.Focus();
        }

        private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null) return;
            if (PresetCombo.SelectedItem is Domain.Services.Road.PresetDescriptor p)
            {
                if (_vm.LoadPresetCommand.CanExecute(p)) _vm.LoadPresetCommand.Execute(p);
                PresetCombo.SelectedItem = null;
            }
        }

        // =========================================================================
        //  Outliner 选中 -> VM.SelectedOutlineNode（三路互斥 / TreeView 原生回调）
        // =========================================================================

        /// <summary>
        /// TreeView 选中切换时回调。直接把新节点交给 VM.SelectedOutlineNode，
        /// VM 内部统一分发到 SelectedBand / SelectedMedianNode / SelectedSideNode 并同步 IsSelected。
        /// </summary>
        private void OutlinerTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_vm == null) return;
            _vm.SelectedOutlineNode = e.NewValue;
        }

        // =========================================================================
        //  预览
        // =========================================================================

        private void OnPreviewRequested(object sender, CrossSectionFigure figure)
        {
            _currentFigure = figure;
            RedrawPreview();
        }

        private void PreviewCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            RedrawPreview();
        }

        private void RedrawPreview()
        {
            CrossSectionPreviewRenderer.Render(PreviewCanvas, _currentFigure, _vm?.ScaleDenominator);
        }
    }
}
