using HyCADTool.MarkdownEditor.ViewModels;
using HyCADTool.TextLayout;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace HyCADTool.MarkdownEditor.Views.Controls
{
    public partial class SettingsPanelControl : UserControl
    {
        private readonly List<CheckBox> _orderedChecks = new List<CheckBox>();
        private readonly List<ComboBox> _styleCombos = new List<ComboBox>();
        private readonly List<TextBox> _indentBoxes = new List<TextBox>();
        private bool _syncing;

        public SettingsPanelControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            BuildLevelRows();
            SyncFromViewModel();
            if (DataContext is EditorViewModel vm)
                vm.PropertyChanged += (_, args) =>
                {
                    if (args.PropertyName == nameof(EditorViewModel.MultilevelList))
                        SyncFromViewModel();
                };

            ChkForceAllOrdered.Checked += (_, __) => PushToViewModel();
            ChkForceAllOrdered.Unchecked += (_, __) => PushToViewModel();
            CmbUnorderedFrom.SelectionChanged += (_, __) => PushToViewModel();
        }

        private void BuildLevelRows()
        {
            var panel = new StackPanel();

            var styleItems = new[]
            {
                new KeyValuePair<string, ListNumberStyle>("1, 2, 3", ListNumberStyle.Decimal),
                new KeyValuePair<string, ListNumberStyle>("a, b, c", ListNumberStyle.LowerLetter),
                new KeyValuePair<string, ListNumberStyle>("A, B, C", ListNumberStyle.UpperLetter),
                new KeyValuePair<string, ListNumberStyle>("i, ii, iii", ListNumberStyle.LowerRoman),
                new KeyValuePair<string, ListNumberStyle>("I, II, III", ListNumberStyle.UpperRoman),
                new KeyValuePair<string, ListNumberStyle>("符号", ListNumberStyle.Bullet),
            };

            for (int i = 0; i < MultilevelListConfig.MaxLevels; i++)
            {
                var row = new Grid { Margin = new Thickness(0, 0, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });

                var label = new TextBlock
                {
                    Text = i.ToString(),
                    VerticalAlignment = VerticalAlignment.Center,
                    Foreground = (System.Windows.Media.Brush)FindResource("ThemeTextPrimaryBrush"),
                    FontSize = 11
                };
                Grid.SetColumn(label, 0);
                row.Children.Add(label);

                var chk = new CheckBox { VerticalAlignment = VerticalAlignment.Center };
                chk.Checked += (_, __) => PushToViewModel();
                chk.Unchecked += (_, __) => PushToViewModel();
                Grid.SetColumn(chk, 1);
                row.Children.Add(chk);
                _orderedChecks.Add(chk);

                var combo = new ComboBox
                {
                    ItemsSource = styleItems,
                    DisplayMemberPath = "Key",
                    SelectedValuePath = "Value",
                    VerticalAlignment = VerticalAlignment.Center,
                    FontSize = 11
                };
                combo.SetResourceReference(ComboBox.StyleProperty, "DarkComboBox");
                combo.SelectionChanged += (_, __) => PushToViewModel();
                Grid.SetColumn(combo, 2);
                row.Children.Add(combo);
                _styleCombos.Add(combo);

                var txt = new TextBox
                {
                    Width = 50,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Text = (i * 2).ToString()
                };
                txt.SetResourceReference(TextBox.StyleProperty, "ParamInput");
                txt.LostFocus += (_, __) => PushToViewModel();
                Grid.SetColumn(txt, 3);
                row.Children.Add(txt);
                _indentBoxes.Add(txt);

                panel.Children.Add(row);
            }

            LevelRows.ItemsSource = null;
            var container = (Panel)LevelRows.Parent;
            int idx = 0;
            for (int j = 0; j < container.Children.Count; j++)
            {
                if (container.Children[j] == LevelRows)
                {
                    idx = j;
                    break;
                }
            }
            container.Children.RemoveAt(idx);
            container.Children.Insert(idx, panel);
        }

        private void SyncFromViewModel()
        {
            if (_syncing) return;
            _syncing = true;
            try
            {
                var vm = DataContext as EditorViewModel;
                var mlc = vm?.MultilevelList ?? MultilevelListConfig.CreateDefault();

                ChkForceAllOrdered.IsChecked = mlc.ForceAllOrdered;

                foreach (ComboBoxItem item in CmbUnorderedFrom.Items)
                {
                    if (item.Tag is string tag && int.TryParse(tag, out int val) && val == mlc.UnorderedFromLevel)
                    {
                        CmbUnorderedFrom.SelectedItem = item;
                        break;
                    }
                }

                for (int i = 0; i < MultilevelListConfig.MaxLevels && i < _orderedChecks.Count; i++)
                {
                    var level = mlc.GetLevel(i);
                    _orderedChecks[i].IsChecked = level.IsOrdered;
                    _styleCombos[i].SelectedValue = level.NumberStyle;
                    _indentBoxes[i].Text = level.IndentChars.ToString("0.##", CultureInfo.InvariantCulture);
                }
            }
            finally
            {
                _syncing = false;
            }
        }

        private void PushToViewModel()
        {
            if (_syncing) return;
            var vm = DataContext as EditorViewModel;
            if (vm == null) return;

            var mlc = vm.EnsureMultilevelList();

            mlc.ForceAllOrdered = ChkForceAllOrdered.IsChecked == true;

            if (CmbUnorderedFrom.SelectedItem is ComboBoxItem selItem && selItem.Tag is string tagStr)
                mlc.UnorderedFromLevel = int.TryParse(tagStr, out int v) ? v : -1;

            for (int i = 0; i < MultilevelListConfig.MaxLevels && i < _orderedChecks.Count; i++)
            {
                var level = mlc.GetLevel(i);
                level.IsOrdered = _orderedChecks[i].IsChecked == true;
                if (_styleCombos[i].SelectedValue is ListNumberStyle ns)
                    level.NumberStyle = ns;
                if (double.TryParse(_indentBoxes[i].Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double indent))
                    level.IndentChars = Math.Max(0, indent);
            }

            vm.NotifyMultilevelListChanged();
        }

        private void BtnApplyStep_Click(object sender, RoutedEventArgs e)
        {
            if (!double.TryParse(TxtIndentStep.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double step))
                step = 2;
            step = Math.Max(0, step);

            for (int i = 0; i < _indentBoxes.Count; i++)
                _indentBoxes[i].Text = (i * step).ToString("0.##", CultureInfo.InvariantCulture);

            PushToViewModel();
        }
    }
}
