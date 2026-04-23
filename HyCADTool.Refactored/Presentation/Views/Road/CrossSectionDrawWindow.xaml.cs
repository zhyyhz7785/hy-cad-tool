using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Input;
using HyCAD.BlenderUI.Controls;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Workflows.Road;
using HyCADTool.Refactored.Infrastructure.Configuration;
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
            viewModel.PickGeometryRequested += OnPickGeometryRequested;

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
                viewModel.PickGeometryRequested -= OnPickGeometryRequested;
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
                SaveAsPresetFromUi();
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
            SaveAsPresetFromUi();
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

        private void OutlinerAddButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null || btn.ContextMenu == null) return;
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.IsOpen = true;
            e.Handled = true;
        }

        private void OutlinerAddMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            var item = sender as MenuItem;
            if (item == null) return;
            if (!(item.Tag is TemplateComponentKind)) return;
            var kind = (TemplateComponentKind)item.Tag;
            if (_vm.InsertBandAfterSelectedCommand.CanExecute(kind))
                _vm.InsertBandAfterSelectedCommand.Execute(kind);
            e.Handled = true;
        }

        private void OutlinerInsertBeforeButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn == null || btn.ContextMenu == null) return;
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu.IsOpen = true;
            e.Handled = true;
        }

        private void OutlinerInsertBeforeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            var item = sender as MenuItem;
            if (item == null) return;
            if (!(item.Tag is TemplateComponentKind)) return;
            var kind = (TemplateComponentKind)item.Tag;
            if (_vm.InsertBandBeforeSelectedCommand != null) _vm.InsertBandBeforeSelectedCommand.Execute(kind);
            e.Handled = true;
        }

        private void OutlinerBandRow_PreviewMouseButtonSelect(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_vm == null) return;
            if (sender is FrameworkElement fe)
            {
                if (fe.DataContext is BandRowViewModel row)
                    _vm.SelectedOutlineNode = row;
                else if (fe.DataContext is MedianHalfRowViewModel mh)
                    _vm.SelectedOutlineNode = mh;
            }
        }

        private void OutlinerNode_PreviewMouseSelect(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_vm == null) return;
            var fe = sender as FrameworkElement;
            if (fe == null) return;
            switch (fe.DataContext)
            {
                case MedianOutlineNode m:
                    _vm.SelectedOutlineNode = m;
                    break;
                case SideOutlineNode s:
                    _vm.SelectedOutlineNode = s;
                    break;
            }
        }

        private void OutlinerBandRow_ContextMenuOpening(object sender, System.Windows.Controls.ContextMenuEventArgs e)
        {
            if (_vm == null) return;
            var fe = sender as FrameworkElement;
            if (fe == null) return;
            if (fe.DataContext is BandRowViewModel row) _vm.SelectedOutlineNode = row;
            if (fe.ContextMenu != null) fe.ContextMenu.DataContext = _vm;
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

        private void OnPickGeometryRequested(object sender, BandRowViewModel row)
        {
            if (row == null) return;
            Dispatcher.BeginInvoke(new Action(() => Hide()), DispatcherPriority.Background);
            try
            {
                var result = RoadCsPickGeometryInteractor.PickAndExtract(row.Kind);
                if (result == null) return;
                row.Width = result.Width;
                row.CrossSlopePct = result.SlopePct;
                row.ElevationDiff = result.ElevationDiff;
                RoadCsPickGeometryInteractor.DistributeThickness(row, result.ThicknessCm);
                if (_vm != null) _vm.LastPickedEntityLayer = result.EntityLayer ?? string.Empty;
            }
            finally
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    Show();
                    Activate();
                    Focus();
                }), DispatcherPriority.Background);
            }
        }

        private void SaveAsPresetFromUi()
        {
            if (_vm == null || _vm.LastLayout == null) return;

            var presetSvc = ResolvePresetServiceSafe();
            if (presetSvc == null)
            {
                MessageBox.Show(this, "预设服务不可用，无法保存。", "存为预设", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!PromptPresetInfo(out var presetKey, out var displayName)) return;

            try
            {
                var path = presetSvc.SaveUserPreset(presetKey, displayName, _vm.LastLayout);
                _vm.RefreshPresets();
                MessageBox.Show(this, $"已保存预设：\n{path}", "存为预设", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"保存失败：{ex.Message}", "存为预设", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static CrossSectionPresetService ResolvePresetServiceSafe()
        {
            try { return ServiceLocator.Resolve<CrossSectionPresetService>(); }
            catch { return null; }
        }

        private bool PromptPresetInfo(out string presetKey, out string displayName)
        {
            presetKey = null;
            displayName = null;
            string chosenKey = null;
            string chosenDisplay = null;

            var initName = string.IsNullOrWhiteSpace(_vm?.Title) ? "我的方案" : _vm.Title.Trim();
            var initKey = $"preset-{DateTime.Now:yyyyMMdd-HHmmss}";

            var dialog = new Window
            {
                Title = "存为预设",
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Width = 420,
                Height = 188,
                MinWidth = 420,
                MinHeight = 188,
                ResizeMode = ResizeMode.NoResize,
                WindowStyle = WindowStyle.SingleBorderWindow,
                ShowInTaskbar = false
            };

            var root = new Grid { Margin = new Thickness(14, 12, 14, 12) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(84) });
            root.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var keyLabel = new TextBlock { Text = "预设 Key", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 10, 4) };
            Grid.SetRow(keyLabel, 0);
            Grid.SetColumn(keyLabel, 0);
            root.Children.Add(keyLabel);

            var keyBox = new TextBox { Text = initKey, Margin = new Thickness(0, 0, 0, 8) };
            Grid.SetRow(keyBox, 0);
            Grid.SetColumn(keyBox, 1);
            root.Children.Add(keyBox);

            var nameLabel = new TextBlock { Text = "显示名", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 2, 10, 4) };
            Grid.SetRow(nameLabel, 1);
            Grid.SetColumn(nameLabel, 0);
            root.Children.Add(nameLabel);

            var nameBox = new TextBox { Text = initName, Margin = new Thickness(0, 0, 0, 10) };
            Grid.SetRow(nameBox, 1);
            Grid.SetColumn(nameBox, 1);
            root.Children.Add(nameBox);

            var hint = new TextBlock
            {
                Text = "说明：Key 用于文件名，建议英文/数字/短横线。",
                Opacity = 0.72,
                Margin = new Thickness(0, 0, 0, 12)
            };
            Grid.SetRow(hint, 2);
            Grid.SetColumn(hint, 1);
            root.Children.Add(hint);

            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var okBtn = new Button { Content = "保存", MinWidth = 76, IsDefault = true, Margin = new Thickness(0, 0, 8, 0) };
            var cancelBtn = new Button { Content = "取消", MinWidth = 76, IsCancel = true };

            okBtn.Click += (_, __) =>
            {
                var key = (keyBox.Text ?? string.Empty).Trim();
                var name = (nameBox.Text ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(key))
                {
                    MessageBox.Show(dialog, "请输入预设 Key。", "存为预设", MessageBoxButton.OK, MessageBoxImage.Warning);
                    keyBox.Focus();
                    return;
                }

                if (string.IsNullOrWhiteSpace(name)) name = key;
                chosenKey = key;
                chosenDisplay = name;
                dialog.DialogResult = true;
                dialog.Close();
            };
            cancelBtn.Click += (_, __) =>
            {
                dialog.DialogResult = false;
                dialog.Close();
            };

            buttons.Children.Add(okBtn);
            buttons.Children.Add(cancelBtn);
            Grid.SetRow(buttons, 3);
            Grid.SetColumn(buttons, 1);
            root.Children.Add(buttons);

            dialog.Content = root;
            dialog.Loaded += (_, __) =>
            {
                keyBox.Focus();
                keyBox.SelectAll();
            };

            if (dialog.ShowDialog() != true) return false;
            presetKey = chosenKey;
            displayName = chosenDisplay;
            return !string.IsNullOrWhiteSpace(presetKey);
        }
    }
}
