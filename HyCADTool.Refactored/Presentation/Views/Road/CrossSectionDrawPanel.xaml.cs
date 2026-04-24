using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using AcApp = Autodesk.AutoCAD.ApplicationServices.Application;
using HyCADTool.Refactored.Domain.Models.Road;
using HyCADTool.Refactored.Domain.Services.Road;
using HyCADTool.Refactored.Domain.ValueObjects.Road;
using HyCADTool.Refactored.Infrastructure.AutoCAD.Services.Road;
using HyCADTool.Refactored.Infrastructure.Configuration;
using HyCADTool.Refactored.Presentation.ViewModels;
using HyCADTool.Refactored.Presentation.ViewModels.Road;

namespace HyCADTool.Refactored.Presentation.Views.Road
{
    /// <summary>
    /// 横断面绘制 v2 — <see cref="UserControl"/> 宿主，由 <see cref="PanelManager"/> 的 <c>PaletteSet</c> 承载。
    /// </summary>
    public partial class CrossSectionDrawPanel : UserControl
    {
        private CrossSectionDrawViewModel _vm;
        private CrossSectionFigure _currentFigure;
        private GridLength _outlinerWidth;
        private GridLength _outlinerSplitterWidth;
        private GridLength _propertyWidth;
        private GridLength _propertySplitterWidth;
        private Point2d? _commitOrigin;

        public CrossSectionDrawPanel()
        {
            InitializeComponent();
            KeyDown += OnPanelKeyDown;
        }

        /// <summary>由 <see cref="PanelManager"/> 注入；切换文档方案时会替换实例并重新订阅事件。</summary>
        public CrossSectionDrawViewModel ViewModel
        {
            get => _vm;
            set
            {
                if (ReferenceEquals(_vm, value)) return;
                DetachViewModel();
                DataContext = value;
                if (value != null)
                    AttachViewModel(value);
            }
        }

        private void AttachViewModel(CrossSectionDrawViewModel viewModel)
        {
            _vm = viewModel;
            _commitOrigin = null;

            _outlinerWidth = OutlinerColumn.Width;
            _outlinerSplitterWidth = OutlinerSplitterColumn.Width;
            _propertyWidth = PropertyColumn.Width;
            _propertySplitterWidth = PropertySplitterColumn.Width;

            viewModel.PreviewRequested += OnPreviewRequested;
            viewModel.PropertyChanged += OnVmPropertyChanged;
            viewModel.PickGeometryRequested += OnPickGeometryRequested;
            viewModel.DrawStructureLinesRequested += OnDrawStructureLinesRequested;

            viewModel.NonCompliantConfirm = summary =>
                ShowPanelMessageBox(
                    summary,
                    "存在未通过的规范项",
                    MessageBoxButton.OKCancel,
                    MessageBoxImage.Warning,
                    MessageBoxResult.Cancel) == MessageBoxResult.OK;

            _currentFigure = viewModel.LastFigure;
            ApplyPanelVisibility();
            if (SettingsPanelViewModel.Current != null)
                SettingsPanelViewModel.Current.PropertyChanged += OnSettingsPropertyChanged;
            ScheduleRedrawPreview();
        }

        private void DetachViewModel()
        {
            if (_vm == null) return;

            if (SettingsPanelViewModel.Current != null)
                SettingsPanelViewModel.Current.PropertyChanged -= OnSettingsPropertyChanged;
            _vm.PreviewRequested -= OnPreviewRequested;
            _vm.PropertyChanged -= OnVmPropertyChanged;
            _vm.PickGeometryRequested -= OnPickGeometryRequested;
            _vm.DrawStructureLinesRequested -= OnDrawStructureLinesRequested;
            _vm = null;
        }

        /// <summary>
        /// PaletteSet 内 <see cref="UserControl"/> 往往不在 WPF <see cref="Window"/> 视觉树下，
        /// <see cref="Window.GetWindow"/> 会为 null；再尝试 <see cref="Application.MainWindow"/>。
        /// </summary>
        private Window TryGetMessageBoxOwner()
        {
            var w = Window.GetWindow(this);
            if (w != null) return w;
            return System.Windows.Application.Current?.MainWindow;
        }

        private MessageBoxResult ShowPanelMessageBox(
            string message,
            string caption,
            MessageBoxButton button,
            MessageBoxImage icon,
            MessageBoxResult defaultResult = MessageBoxResult.None)
        {
            var owner = TryGetMessageBoxOwner();
            if (owner != null)
                return MessageBox.Show(owner, message, caption, button, icon, defaultResult);
            return MessageBox.Show(message, caption, button, icon, defaultResult);
        }

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

        private void OnPanelKeyDown(object sender, KeyEventArgs e)
        {
            if (_vm == null) return;

            if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
            {
                if (_vm.DrawStructureLinesCommand.CanExecute(null))
                    _vm.DrawStructureLinesCommand.Execute(null);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                try
                {
                    ServiceLocator.Resolve<PanelManager>().HideCrossSectionPanel();
                }
                catch
                {
                    // ignore
                }

                e.Handled = true;
            }
            else if (e.Key == Key.N && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                SaveAsPresetFromUi();
                e.Handled = true;
            }
            else if (e.Key == Key.L && Keyboard.Modifiers == ModifierKeys.Alt)
            {
                PresetCombo.IsDropDownOpen = true;
                PresetCombo.Focus();
                e.Handled = true;
            }
        }

        private void NewMenu_Click(object sender, RoutedEventArgs e) => SaveAsPresetFromUi();

        private void PresetCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_vm == null) return;
            if (PresetCombo.SelectedItem is PresetDescriptor p)
            {
                if (_vm.LoadPresetCommand.CanExecute(p)) _vm.LoadPresetCommand.Execute(p);
            }
        }

        private void DeletePresetButton_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;

            if (!(PresetCombo.SelectedItem is PresetDescriptor p))
            {
                ShowPanelMessageBox("请先在预设下拉中选择要删除的用户预设。", "删除预设", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var presetSvc = ResolvePresetServiceSafe();
            if (presetSvc == null)
            {
                ShowPanelMessageBox("预设服务不可用，无法删除。", "删除预设", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (presetSvc.IsBuiltInPresetKey(p.Key))
            {
                ShowPanelMessageBox("内置预设不可删除。", "删除预设", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var ok = ShowPanelMessageBox(
                $"确认删除用户预设“{p.DisplayName}”吗？\n此操作不可撤销。",
                "删除预设",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning,
                MessageBoxResult.No);
            if (ok != MessageBoxResult.Yes) return;

            bool deleted = false;
            try
            {
                deleted = presetSvc.DeleteUserPresetByDisplayName(p.DisplayName);
                if (!deleted) deleted = presetSvc.DeleteUserPreset(p.Key);
            }
            catch (Exception ex)
            {
                ShowPanelMessageBox($"删除失败：{ex.Message}", "删除预设", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!deleted)
            {
                ShowPanelMessageBox("未找到可删除的用户预设文件。", "删除预设", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            _vm.RefreshPresets();
            PresetCombo.SelectedItem = null;
            ShowPanelMessageBox("已删除用户预设。", "删除预设", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void OutlinerAddButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn?.ContextMenu == null) return;
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.IsOpen = true;
            e.Handled = true;
        }

        private void OutlinerAddMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            var item = sender as MenuItem;
            if (item?.Tag is TemplateComponentKind kind
                && _vm.InsertBandAfterSelectedCommand.CanExecute(kind))
                _vm.InsertBandAfterSelectedCommand.Execute(kind);
            e.Handled = true;
        }

        private void OutlinerInsertBeforeButton_Click(object sender, RoutedEventArgs e)
        {
            var btn = sender as Button;
            if (btn?.ContextMenu == null) return;
            btn.ContextMenu.PlacementTarget = btn;
            btn.ContextMenu.Placement = System.Windows.Controls.Primitives.PlacementMode.Bottom;
            btn.ContextMenu.IsOpen = true;
            e.Handled = true;
        }

        private void OutlinerInsertBeforeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (_vm == null) return;
            var item = sender as MenuItem;
            if (item?.Tag is TemplateComponentKind kind)
                _vm.InsertBandBeforeSelectedCommand?.Execute(kind);
            e.Handled = true;
        }

        private void OutlinerBandRow_PreviewMouseButtonSelect(object sender, MouseButtonEventArgs e)
        {
            if (_vm == null || !(sender is FrameworkElement fe)) return;
            object target = null;
            if (fe.DataContext is BandRowViewModel row) target = row;
            else if (fe.DataContext is MedianHalfRowViewModel mh) target = mh;
            if (target == null) return;
            ScheduleSelectOutlineNode(target);
        }

        private void OutlinerNode_PreviewMouseSelect(object sender, MouseButtonEventArgs e)
        {
            if (_vm == null) return;
            var fe = sender as FrameworkElement;
            if (fe == null) return;
            object target = null;
            switch (fe.DataContext)
            {
                case MedianOutlineNode m: target = m; break;
                case SideOutlineNode s: target = s; break;
            }
            if (target == null) return;
            ScheduleSelectOutlineNode(target);
        }

        private void OutlinerBandRow_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (_vm == null) return;
            var fe = sender as FrameworkElement;
            if (fe == null) return;
            if (fe.DataContext is BandRowViewModel row) _vm.SelectedOutlineNode = row;
            if (fe.ContextMenu != null) fe.ContextMenu.DataContext = _vm;
        }

        private void OutlinerTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (_vm == null) return;
            ScheduleSelectOutlineNode(e.NewValue);
        }

        // 将 SelectedOutlineNode 的赋值延后到下一帧（Background 优先级），
        // 让 TreeView 先完成高亮/展开等轻量视觉更新后，再触发右侧属性面板重建。
        // 这是减轻「点击大纲节点卡顿感」的关键：点击事件立刻返回，用户先看到选中反馈。
        private object _pendingOutlineNode;
        private bool _outlineSelectScheduled;

        private void ScheduleSelectOutlineNode(object target)
        {
            _pendingOutlineNode = target;
            if (_outlineSelectScheduled) return;
            _outlineSelectScheduled = true;
            Dispatcher.BeginInvoke(new Action(ApplyPendingOutlineSelection), DispatcherPriority.Background);
        }

        private void ApplyPendingOutlineSelection()
        {
            _outlineSelectScheduled = false;
            if (_vm == null) return;
            var target = _pendingOutlineNode;
            _pendingOutlineNode = null;
            _vm.SelectedOutlineNode = target;
        }

        private void OnPreviewRequested(object sender, CrossSectionFigure figure)
        {
            _currentFigure = figure;
            ScheduleRedrawPreview();
        }

        private void PreviewCanvas_SizeChanged(object sender, SizeChangedEventArgs e) => ScheduleRedrawPreview();

        private void OnSettingsPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsPanelViewModel.RoadCrossSectionPlanStripVerticalOffsetM))
                ScheduleRedrawPreview();
            else if (e.PropertyName == nameof(SettingsPanelViewModel.RoadCrossSectionOrientationUseWestEast))
                _vm?.Recalculate();
        }

        // RedrawPreview 合并到下一帧（Background）：
        //   SizeChanged / PreviewRequested / Settings 变化 三路常在同一帧内连续触发，
        //   Canvas.Clear + 重建数百个 UIElement 若每路都执行一次会造成拖动面板边缘 /
        //   拖滑杆时的掉帧。合并后同一帧最多只重绘一次。
        private bool _redrawScheduled;
        private void ScheduleRedrawPreview()
        {
            if (_redrawScheduled) return;
            _redrawScheduled = true;
            Dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() =>
            {
                _redrawScheduled = false;
                RedrawPreview();
            }));
        }

        private void RedrawPreview()
        {
            double planOff = SettingsPanelViewModel.Current?.RoadCrossSectionPlanStripVerticalOffsetM ?? 5.0;
            CrossSectionPreviewRenderer.Render(PreviewCanvas, _currentFigure, _vm?.ScaleDenominator, null,
                SettingsPanelViewModel.Current?.CreateDrawingSheetTitleSpec(),
                layout: _vm?.LastLayout,
                planStripVerticalOffsetM: planOff,
                onOrientationToggle: OnOrientationLabelClicked,
                drawSheetTitleBand: false,
                drawRoadWidthCornerBadge: false);
        }

        private static void OnOrientationLabelClicked()
        {
            var sp = SettingsPanelViewModel.Current;
            if (sp == null) return;
            sp.RoadCrossSectionOrientationUseWestEast = !sp.RoadCrossSectionOrientationUseWestEast;
        }

        private void OnPickGeometryRequested(object sender, BandRowViewModel row)
        {
            if (row == null) return;
            try
            {
                var result = Infrastructure.AutoCAD.Workflows.Road.RoadCsPickGeometryInteractor.PickAndExtract(row.Kind);
                if (result == null) return;
                row.Width = result.Width;
                row.CrossSlopePct = result.SlopePct;
                row.ElevationDiff = result.ElevationDiff;
                Infrastructure.AutoCAD.Workflows.Road.RoadCsPickGeometryInteractor.DistributeThickness(row, result.ThicknessCm);
                if (_vm != null) _vm.LastPickedEntityLayer = result.EntityLayer ?? string.Empty;
            }
            catch (Exception ex)
            {
                ShowPanelMessageBox("拾取失败：" + ex.Message, "从图形拾取", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OnDrawStructureLinesRequested(object sender, CrossSectionDesignerResult result)
        {
            if (result == null || _vm == null) return;

            var doc = AcApp.DocumentManager.MdiActiveDocument;
            if (doc == null) return;

            CrossSectionCommitDrawService commit;
            try
            {
                commit = ServiceLocator.Resolve<CrossSectionCommitDrawService>();
            }
            catch (Exception ex)
            {
                ShowPanelMessageBox("落盘服务不可用：" + ex.Message, "横断面出图", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (!_commitOrigin.HasValue)
            {
                var picked = PromptInsertionPoint(doc.Editor);
                if (picked == null) return;
                _commitOrigin = picked;
            }

            try
            {
                var outcome = commit.Commit(
                    doc,
                    result,
                    _commitOrigin.Value,
                    _vm.ExistingTemplateId,
                    CrossSectionDrawMode.WithStructureThickness,
                    _vm.DrawSectionStructureFills);

                if (!_vm.ExistingTemplateId.HasValue)
                    _vm.AdoptCommittedTemplateId(result.Template.Id);

                var jsonHint = string.IsNullOrEmpty(outcome.JsonPath)
                    ? "JSON 未落盘（请先保存 DWG）"
                    : $"JSON: {outcome.JsonPath}";
                _vm.SetCommitOutcomeStatus(
                    $"{(outcome.WasReplace ? "已更新" : "已新增")} 模板 {result.Template.Name}（Id={result.Template.Id:N}）· " +
                    $"擦除 {outcome.Erased} / 生成 {outcome.Created} · {jsonHint}");

                doc.Editor.WriteMessage(
                    $"\n[道路] 已 {(outcome.WasReplace ? "更新" : "新增")} 模板 {result.Template.Name}（Id={result.Template.Id:N}）。");
                doc.Editor.WriteMessage(
                    $"\n[道路] 路幅 {result.Layout.TotalWidth:F2} m，"
                    + $"左半 {result.Layout.LeftHalfWidth:F2} m / 右半 {result.Layout.RightHalfWidth:F2} m，"
                    + $"设计速度 V={result.Layout.DesignSpeed} km/h，比例 1:{result.Layout.ScaleDenominator}。");
                doc.Editor.WriteMessage($"\n[道路] 实体变更：擦除 {outcome.Erased} 个 / 生成 {outcome.Created} 个。");
                if (!string.IsNullOrEmpty(outcome.JsonPath))
                    doc.Editor.WriteMessage($"\n[道路] JSON 已同步落盘：{outcome.JsonPath}");
                else
                    doc.Editor.WriteMessage("\n[道路] 未落盘（DWG 尚未保存）。先 QSAVE / SAVEAS，再跑 hyRoadSave 即可。");
            }
            catch (Exception ex)
            {
                ShowPanelMessageBox("落盘/出图失败：" + ex.Message, "横断面出图", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private static Point2d? PromptInsertionPoint(Editor ed)
        {
            var opt = new PromptPointOptions("\n[道路] 指定横断面图插入点：")
            {
                AllowNone = false,
            };
            var res = ed.GetPoint(opt);
            if (res.Status != PromptStatus.OK)
            {
                ed.WriteMessage("\n[道路] 已取消插入点。");
                return null;
            }

            return new Point2d(res.Value.X, res.Value.Y);
        }

        private void SaveAsPresetFromUi()
        {
            if (_vm == null || _vm.LastLayout == null) return;

            var presetSvc = ResolvePresetServiceSafe();
            if (presetSvc == null)
            {
                ShowPanelMessageBox("预设服务不可用，无法保存。", "存为预设", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!PromptPresetInfo(out var presetKey, out var displayName)) return;

            try
            {
                var path = presetSvc.SaveUserPreset(presetKey, displayName, _vm.LastLayout);
                _vm.RefreshPresets();
                ShowPanelMessageBox($"已保存预设：\n{path}", "存为预设", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                ShowPanelMessageBox($"保存失败：{ex.Message}", "存为预设", MessageBoxButton.OK, MessageBoxImage.Error);
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
            var owner = TryGetMessageBoxOwner();

            var dialog = new Window
            {
                Title = "存为预设",
                Owner = owner,
                WindowStartupLocation = owner != null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,
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
