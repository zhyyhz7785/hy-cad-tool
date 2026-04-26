using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace HyCAD.BlenderUI.Controls.Primitives
{
    /// <summary>
    /// Blender 风格搜索框：左侧放大镜图标 + 中间输入 + 右侧清除按钮（有输入时出现）。
    /// </summary>
    [TemplatePart(Name = PART_TextBox, Type = typeof(TextBox))]
    [TemplatePart(Name = PART_ClearButton, Type = typeof(Button))]
    public class SearchBox : Control
    {
        private const string PART_TextBox = "PART_TextBox";
        private const string PART_ClearButton = "PART_ClearButton";

        private TextBox _textBox;
        private Button _clearButton;

        static SearchBox()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(SearchBox),
                new FrameworkPropertyMetadata(typeof(SearchBox)));
        }

        public static readonly DependencyProperty TextProperty = DependencyProperty.Register(
            nameof(Text), typeof(string), typeof(SearchBox),
            new FrameworkPropertyMetadata(string.Empty,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.Journal,
                (d, e) => ((SearchBox)d).UpdateClearVisibility()));

        public string Text
        {
            get => (string)GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(
            nameof(Placeholder), typeof(string), typeof(SearchBox),
            new PropertyMetadata("搜索..."));

        public string Placeholder
        {
            get => (string)GetValue(PlaceholderProperty);
            set => SetValue(PlaceholderProperty, value);
        }

        public static readonly DependencyProperty HasTextProperty = DependencyProperty.Register(
            nameof(HasText), typeof(bool), typeof(SearchBox),
            new PropertyMetadata(false));

        public bool HasText
        {
            get => (bool)GetValue(HasTextProperty);
            private set => SetValue(HasTextProperty, value);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            if (_clearButton != null) _clearButton.Click -= OnClearClick;
            _textBox = GetTemplateChild(PART_TextBox) as TextBox;
            _clearButton = GetTemplateChild(PART_ClearButton) as Button;
            if (_clearButton != null) _clearButton.Click += OnClearClick;
            UpdateClearVisibility();
        }

        private void OnClearClick(object sender, RoutedEventArgs e)
        {
            Text = string.Empty;
            _textBox?.Focus();
        }

        private void UpdateClearVisibility()
        {
            HasText = !string.IsNullOrEmpty(Text);
        }

        protected override void OnGotFocus(RoutedEventArgs e)
        {
            base.OnGotFocus(e);
            _textBox?.Focus();
        }
    }
}
