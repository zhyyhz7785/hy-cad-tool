using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HyCAD.BlenderUI.Controls;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using HyCADTool.Refactored.Presentation.ViewModels.Road;

namespace HyCADTool.Refactored.Presentation.Views.Road
{
    /// <summary>
    /// "标准横断面图设计器"窗口（BlenderWindow 子类）—— v1 旧 UI，仍由 <c>hyRoadT</c> 历史入口使用。
    ///
    /// <para>
    /// 注意：v2 已新建 <see cref="CrossSectionDrawWindow"/>（BlenderUI workbench 风格）替代本窗口；
    /// 渲染细节统一抽到 <see cref="CrossSectionPreviewRenderer"/>，旧窗口保留只为兼容 hyRoadT 旧入口与既有截图。
    /// </para>
    ///
    /// 职责：
    /// <list type="bullet">
    ///   <item>把 <see cref="CrossSectionDesignerViewModel"/> 作为 DataContext。</item>
    ///   <item>订阅 <see cref="CrossSectionDesignerViewModel.PreviewRequested"/>，将 <see cref="CrossSectionFigure"/>
    ///   委托给 <see cref="CrossSectionPreviewRenderer"/> 投影到 WPF <see cref="Canvas"/> 上做实时预览。</item>
    ///   <item>把 DataGrid 选中行同步到 VM.SelectedBand。</item>
    ///   <item>注入不涉及 WPF 的 <see cref="MessageBox"/> 二次确认回调。</item>
    ///   <item>响应标题栏 X 按钮（<see cref="BlenderWindow.CloseClicked"/>）走 CancelCommand。</item>
    /// </list>
    ///
    /// 最终的 AutoCAD 出图由命令层订阅 <see cref="CrossSectionDesignerViewModel.Confirmed"/> 完成，
    /// 本窗口不直接引用 AutoCAD API。
    /// </summary>
    [Obsolete("v2 已使用 CrossSectionDrawWindow（BlenderUI workbench 风格）替代；仅保留以兼容 hyRoadT 历史入口与既有截图，Stage 3 完成后将整体下线。新代码请直接使用 CrossSectionDrawWindow。", false)]
    public partial class CrossSectionDesignerWindow : BlenderWindow
    {
        private CrossSectionDesignerViewModel _vm;
        private CrossSectionFigure _currentFigure;

        public CrossSectionDesignerWindow()
        {
            InitializeComponent();
            KeyDown += OnWindowKeyDown;
            CloseClicked += OnCloseClicked;
        }

        public CrossSectionDesignerWindow(CrossSectionDesignerViewModel viewModel) : this()
        {
            if (viewModel == null) throw new ArgumentNullException(nameof(viewModel));
            _vm = viewModel;
            DataContext = viewModel;

            viewModel.PreviewRequested += OnPreviewRequested;
            viewModel.CloseRequested += OnCloseRequested;

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
                RedrawPreview();
            };

            Closed += (_, __) =>
            {
                viewModel.PreviewRequested -= OnPreviewRequested;
                viewModel.CloseRequested -= OnCloseRequested;
                CloseClicked -= OnCloseClicked;
            };
        }

        // =========================================================================
        //  关闭
        // =========================================================================

        private void OnCloseRequested(object sender, bool? dialogResult)
        {
            // 走 BlenderWindow 提供的安全关闭：兼容 ShowModalWindow 与 ShowDialog 两条路径
            RequestClose(dialogResult);
        }

        private void OnCloseClicked(object sender, RoutedEventArgs e)
        {
            // 标题栏 X：走 CancelCommand 让 VM 决定后续；CommandCanExecute=false 时让 BlenderWindow 兜底关闭
            if (_vm != null && _vm.CancelCommand.CanExecute(null))
            {
                _vm.CancelCommand.Execute(null);
                e.Handled = true;
            }
        }

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
        }

        // =========================================================================
        //  预设
        // =========================================================================

        private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null) return;
            if (PresetCombo.SelectedItem is Domain.Services.Road.PresetDescriptor p)
            {
                if (_vm.LoadPresetCommand.CanExecute(p)) _vm.LoadPresetCommand.Execute(p);
                // 选中后立即让下拉失焦，避免每次打开窗口都高亮
                PresetCombo.SelectedItem = null;
            }
        }

        // =========================================================================
        //  DataGrid 选中 -> VM.SelectedBand
        // =========================================================================

        private void AnyGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null) return;
            if (sender is DataGrid grid)
            {
                if (grid != LeftGrid) LeftGrid.UnselectAll();
                if (grid != RightGrid) RightGrid.UnselectAll();

                if (grid.SelectedItem is BandRowViewModel row)
                {
                    _vm.SelectedBand = row;
                }
                else
                {
                    _vm.SelectedBand = null;
                }
            }
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
