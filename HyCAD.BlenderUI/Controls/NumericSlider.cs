using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace HyCAD.BlenderUI.Controls
{
    /// <summary>
    /// Blender 风格数值滑块：
    ///  - 背景覆盖 [Min,Max] 区间，Value 按百分比填充前景条
    ///  - 两侧小三角按钮步进
    ///  - 中央文字显示 "Label: 123.4"，点击可进入编辑态（TextBox）
    ///  - 拖动主体可横向滑动调值
    /// </summary>
    [TemplatePart(Name = PART_Root, Type = typeof(Grid))]
    [TemplatePart(Name = PART_FillBar, Type = typeof(FrameworkElement))]
    [TemplatePart(Name = PART_ValueText, Type = typeof(TextBlock))]
    [TemplatePart(Name = PART_Editor, Type = typeof(TextBox))]
    [TemplatePart(Name = PART_DecButton, Type = typeof(RepeatButton))]
    [TemplatePart(Name = PART_IncButton, Type = typeof(RepeatButton))]
    public class NumericSlider : Control
    {
        private const string PART_Root = "PART_Root";
        private const string PART_FillBar = "PART_FillBar";
        private const string PART_ValueText = "PART_ValueText";
        private const string PART_Editor = "PART_Editor";
        private const string PART_DecButton = "PART_DecButton";
        private const string PART_IncButton = "PART_IncButton";

        private Grid _root;
        private FrameworkElement _fillBar;
        private TextBlock _valueText;
        private TextBox _editor;
        private RepeatButton _dec;
        private RepeatButton _inc;

        private bool _isDragging;
        private Point _dragStart;
        private double _dragStartValue;

        static NumericSlider()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(NumericSlider),
                new FrameworkPropertyMetadata(typeof(NumericSlider)));
        }

        #region DPs

        public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(
            nameof(Value), typeof(double), typeof(NumericSlider),
            new FrameworkPropertyMetadata(0d,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault | FrameworkPropertyMetadataOptions.Journal,
                OnValueChanged, CoerceValue));

        public double Value
        {
            get => (double)GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register(
            nameof(Minimum), typeof(double), typeof(NumericSlider),
            new FrameworkPropertyMetadata(0d, OnRangeChanged));

        public double Minimum
        {
            get => (double)GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register(
            nameof(Maximum), typeof(double), typeof(NumericSlider),
            new FrameworkPropertyMetadata(100d, OnRangeChanged));

        public double Maximum
        {
            get => (double)GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public static readonly DependencyProperty StepProperty = DependencyProperty.Register(
            nameof(Step), typeof(double), typeof(NumericSlider),
            new PropertyMetadata(1d));

        public double Step
        {
            get => (double)GetValue(StepProperty);
            set => SetValue(StepProperty, value);
        }

        public static readonly DependencyProperty DecimalsProperty = DependencyProperty.Register(
            nameof(Decimals), typeof(int), typeof(NumericSlider),
            new PropertyMetadata(2, (d, e) => ((NumericSlider)d).UpdateDisplay()));

        public int Decimals
        {
            get => (int)GetValue(DecimalsProperty);
            set => SetValue(DecimalsProperty, value);
        }

        public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
            nameof(Label), typeof(string), typeof(NumericSlider),
            new PropertyMetadata(string.Empty, (d, e) => ((NumericSlider)d).UpdateDisplay()));

        public string Label
        {
            get => (string)GetValue(LabelProperty);
            set => SetValue(LabelProperty, value);
        }

        public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(
            nameof(Unit), typeof(string), typeof(NumericSlider),
            new PropertyMetadata(string.Empty, (d, e) => ((NumericSlider)d).UpdateDisplay()));

        public string Unit
        {
            get => (string)GetValue(UnitProperty);
            set => SetValue(UnitProperty, value);
        }

        public static readonly DependencyProperty FillPercentProperty = DependencyProperty.Register(
            nameof(FillPercent), typeof(double), typeof(NumericSlider),
            new PropertyMetadata(0d));

        /// <summary>0..1 之间，供 Template 用宽度百分比填充</summary>
        public double FillPercent
        {
            get => (double)GetValue(FillPercentProperty);
            private set => SetValue(FillPercentProperty, value);
        }

        public static readonly DependencyProperty DisplayTextProperty = DependencyProperty.Register(
            nameof(DisplayText), typeof(string), typeof(NumericSlider),
            new PropertyMetadata(string.Empty));

        public string DisplayText
        {
            get => (string)GetValue(DisplayTextProperty);
            private set => SetValue(DisplayTextProperty, value);
        }

        #endregion

        private static object CoerceValue(DependencyObject d, object baseValue)
        {
            var ns = (NumericSlider)d;
            double v = (double)baseValue;
            if (v < ns.Minimum) v = ns.Minimum;
            if (v > ns.Maximum) v = ns.Maximum;
            return v;
        }

        private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            ((NumericSlider)d).UpdateDisplay();
        }

        private static void OnRangeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ns = (NumericSlider)d;
            ns.CoerceValue(ValueProperty);
            ns.UpdateDisplay();
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            if (_dec != null) _dec.Click -= OnDec;
            if (_inc != null) _inc.Click -= OnInc;
            if (_valueText != null) _valueText.MouseLeftButtonDown -= OnEnterEdit;
            if (_editor != null)
            {
                _editor.LostFocus -= OnEditorCommit;
                _editor.KeyDown -= OnEditorKey;
            }

            _root = GetTemplateChild(PART_Root) as Grid;
            _fillBar = GetTemplateChild(PART_FillBar) as FrameworkElement;
            _valueText = GetTemplateChild(PART_ValueText) as TextBlock;
            _editor = GetTemplateChild(PART_Editor) as TextBox;
            _dec = GetTemplateChild(PART_DecButton) as RepeatButton;
            _inc = GetTemplateChild(PART_IncButton) as RepeatButton;

            if (_dec != null) _dec.Click += OnDec;
            if (_inc != null) _inc.Click += OnInc;
            if (_valueText != null) _valueText.MouseLeftButtonDown += OnEnterEdit;
            if (_editor != null)
            {
                _editor.LostFocus += OnEditorCommit;
                _editor.KeyDown += OnEditorKey;
            }

            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            double range = Maximum - Minimum;
            FillPercent = range <= 0 ? 0 : Math.Max(0, Math.Min(1, (Value - Minimum) / range));
            string fmt = "F" + Math.Max(0, Decimals).ToString(CultureInfo.InvariantCulture);
            string v = Value.ToString(fmt, CultureInfo.InvariantCulture);
            DisplayText = string.IsNullOrEmpty(Label)
                ? (string.IsNullOrEmpty(Unit) ? v : v + " " + Unit)
                : (string.IsNullOrEmpty(Unit) ? Label + ":  " + v : Label + ":  " + v + " " + Unit);
            UpdateFillWidth();
        }

        private void UpdateFillWidth()
        {
            if (_fillBar == null || _root == null) return;
            double w = _root.ActualWidth;
            if (double.IsNaN(w) || w <= 0) return;
            _fillBar.Width = Math.Max(0, w * FillPercent);
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            UpdateFillWidth();
        }

        private void OnDec(object sender, RoutedEventArgs e) => Value -= Step;
        private void OnInc(object sender, RoutedEventArgs e) => Value += Step;

        private void OnEnterEdit(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount < 2) return;
            if (_editor == null || _valueText == null) return;
            _editor.Text = Value.ToString(CultureInfo.InvariantCulture);
            _editor.Visibility = Visibility.Visible;
            _valueText.Visibility = Visibility.Collapsed;
            _editor.Focus();
            _editor.SelectAll();
            e.Handled = true;
        }

        private void OnEditorCommit(object sender, RoutedEventArgs e)
        {
            if (_editor == null || _valueText == null) return;
            if (double.TryParse(_editor.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var nv))
                Value = nv;
            _editor.Visibility = Visibility.Collapsed;
            _valueText.Visibility = Visibility.Visible;
        }

        private void OnEditorKey(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                OnEditorCommit(sender, e);
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                _editor.Visibility = Visibility.Collapsed;
                _valueText.Visibility = Visibility.Visible;
                e.Handled = true;
            }
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (_editor != null && _editor.Visibility == Visibility.Visible) return;
            if (_root == null) return;
            _isDragging = true;
            _dragStart = e.GetPosition(_root);
            _dragStartValue = Value;
            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (!_isDragging || _root == null) return;
            var p = e.GetPosition(_root);
            double width = _root.ActualWidth;
            if (width <= 0) return;
            double dx = p.X - _dragStart.X;
            double range = Maximum - Minimum;
            double delta = dx / width * range;
            Value = _dragStartValue + delta;
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonUp(e);
            if (_isDragging)
            {
                _isDragging = false;
                ReleaseMouseCapture();
            }
        }

        protected override void OnMouseWheel(MouseWheelEventArgs e)
        {
            base.OnMouseWheel(e);
            if (e.Delta > 0) Value += Step;
            else Value -= Step;
            e.Handled = true;
        }
    }
}
