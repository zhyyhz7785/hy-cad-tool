using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using HyCAD.BlenderUI.Preferences;

namespace HyCAD.BlenderUI.Controls
{
    /// <summary>
    /// 可见的上侧 / 侧面切换。默认写 <see cref="BlenderUiPreferences.ToolbarPosition"/>，运行时立即生效并持久化。
    /// </summary>
    [TemplatePart(Name = PartTop, Type = typeof(ToggleButton))]
    [TemplatePart(Name = PartSide, Type = typeof(ToggleButton))]
    public class ToolbarPositionSwitch : Control
    {
        public const string PartTop = "PART_Top";
        public const string PartSide = "PART_Side";

        private ToggleButton _top;
        private ToggleButton _side;
        private bool _syncing;

        static ToolbarPositionSwitch()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(ToolbarPositionSwitch),
                new FrameworkPropertyMetadata(typeof(ToolbarPositionSwitch)));
        }

        public ToolbarPositionSwitch()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
            nameof(Position), typeof(ToolbarPosition), typeof(ToolbarPositionSwitch),
            new FrameworkPropertyMetadata(
                ToolbarPosition.Top,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnPositionChanged));

        public ToolbarPosition Position
        {
            get => (ToolbarPosition)GetValue(PositionProperty);
            set => SetValue(PositionProperty, value);
        }

        public static readonly DependencyProperty FollowsPreferenceProperty = DependencyProperty.Register(
            nameof(FollowsPreference), typeof(bool), typeof(ToolbarPositionSwitch),
            new PropertyMetadata(true, OnFollowsPreferenceChanged));

        public bool FollowsPreference
        {
            get => (bool)GetValue(FollowsPreferenceProperty);
            set => SetValue(FollowsPreferenceProperty, value);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            Unhook(_top);
            Unhook(_side);
            _top = GetTemplateChild(PartTop) as ToggleButton;
            _side = GetTemplateChild(PartSide) as ToggleButton;
            Hook(_top);
            Hook(_side);
            SyncButtons();
        }

        private static void OnPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ToolbarPositionSwitch sw)
            {
                sw.SyncButtons();
                sw.WritePreferenceIfNeeded();
            }
        }

        private static void OnFollowsPreferenceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ToolbarPositionSwitch sw)
                sw.ReadPreferenceIfNeeded();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            BlenderUiPreferences.Current.PropertyChanged -= OnPreferenceChanged;
            BlenderUiPreferences.Current.PropertyChanged += OnPreferenceChanged;
            ReadPreferenceIfNeeded();
            SyncButtons();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            BlenderUiPreferences.Current.PropertyChanged -= OnPreferenceChanged;
        }

        private void OnPreferenceChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != null &&
                e.PropertyName != nameof(BlenderUiPreferences.ToolbarPosition))
                return;
            ReadPreferenceIfNeeded();
        }

        private void Hook(ToggleButton btn)
        {
            if (btn == null) return;
            btn.Checked += OnButtonChecked;
            btn.Unchecked += OnButtonUnchecked;
        }

        private void Unhook(ToggleButton btn)
        {
            if (btn == null) return;
            btn.Checked -= OnButtonChecked;
            btn.Unchecked -= OnButtonUnchecked;
        }

        private void OnButtonChecked(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            if (ReferenceEquals(sender, _side))
                Position = ToolbarPosition.Side;
            else
                Position = ToolbarPosition.Top;
        }

        private void OnButtonUnchecked(object sender, RoutedEventArgs e)
        {
            if (_syncing) return;
            // 互斥：不允许两个都关，回弹到当前 Position。
            SyncButtons();
        }

        private void SyncButtons()
        {
            if (_top == null && _side == null) return;
            _syncing = true;
            try
            {
                bool side = Position == ToolbarPosition.Side;
                if (_top != null) _top.IsChecked = !side;
                if (_side != null) _side.IsChecked = side;
            }
            finally { _syncing = false; }
        }

        private void ReadPreferenceIfNeeded()
        {
            if (!FollowsPreference || _syncing) return;
            var next = BlenderUiPreferences.Current.ToolbarPosition;
            if (Position == next) return;
            _syncing = true;
            try { Position = next; }
            finally { _syncing = false; }
            SyncButtons();
        }

        private void WritePreferenceIfNeeded()
        {
            if (!FollowsPreference || _syncing) return;
            if (BlenderUiPreferences.Current.ToolbarPosition == Position) return;
            _syncing = true;
            try { BlenderUiPreferences.Current.ToolbarPosition = Position; }
            finally { _syncing = false; }
        }
    }
}
