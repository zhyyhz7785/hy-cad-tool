using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HyCAD.BlenderUI.Controls.Widgets
{
    /// <summary>UI_BTYPE_SEARCH_MENU：带过滤的搜索框。</summary>
    public class BlenderSearchMenu : Control
    {
        private TextBox _text;
        private ListBox _list;

        static BlenderSearchMenu()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BlenderSearchMenu),
                new FrameworkPropertyMetadata(typeof(BlenderSearchMenu)));
        }

        public static readonly DependencyProperty ItemsProperty = DependencyProperty.Register(
            nameof(Items), typeof(ObservableCollection<string>), typeof(BlenderSearchMenu),
            new PropertyMetadata(null));

        public ObservableCollection<string> Items
        {
            get => (ObservableCollection<string>)GetValue(ItemsProperty);
            set => SetValue(ItemsProperty, value);
        }

        public static readonly DependencyProperty SelectedItemProperty = DependencyProperty.Register(
            nameof(SelectedItem), typeof(string), typeof(BlenderSearchMenu),
            new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

        public string SelectedItem
        {
            get => (string)GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public static readonly DependencyProperty FilterTextProperty = DependencyProperty.Register(
            nameof(FilterText), typeof(string), typeof(BlenderSearchMenu),
            new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnFilterChanged));

        public string FilterText
        {
            get => (string)GetValue(FilterTextProperty);
            set => SetValue(FilterTextProperty, value);
        }

        private static void OnFilterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BlenderSearchMenu m) m.RefreshFilter();
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (Items == null)
                Items = new ObservableCollection<string> { "alpha", "bravo", "charlie", "delta" };
            _text = GetTemplateChild("PART_Text") as TextBox;
            _list = GetTemplateChild("PART_List") as ListBox;
            if (_text != null)
            {
                _text.TextChanged -= OnTextChanged;
                _text.TextChanged += OnTextChanged;
            }
            if (_list != null)
            {
                _list.SelectionChanged -= OnSelectionChanged;
                _list.SelectionChanged += OnSelectionChanged;
            }
            RefreshFilter();
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_text != null)
                FilterText = _text.Text;
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_list?.SelectedItem is string s)
                SelectedItem = s;
        }

        private void RefreshFilter()
        {
            if (_list == null || Items == null) return;
            var q = (FilterText ?? string.Empty).Trim().ToLowerInvariant();
            _list.ItemsSource = string.IsNullOrEmpty(q)
                ? Items
                : new ObservableCollection<string>(Items.Where(x => x.ToLowerInvariant().Contains(q)));
        }
    }
}
