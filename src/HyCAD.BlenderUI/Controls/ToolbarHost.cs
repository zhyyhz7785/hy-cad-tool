using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using HyCAD.BlenderUI.Preferences;

namespace HyCAD.BlenderUI.Controls
{
    /// <summary>
    /// 把 <see cref="Toolbar"/> 按 <see cref="Position"/> 停在内容上侧或左侧。
    /// 上侧时横向条在视口上方、竖向 T 收起；侧面时相反。N 侧栏 / Header 不在本控件内。
    /// 默认跟随 <see cref="BlenderUiPreferences.ToolbarPosition"/>（默认上侧，可持久化）。
    /// </summary>
    [TemplatePart(Name = PartToolbarSlot, Type = typeof(UIElement))]
    public class ToolbarHost : ContentControl
    {
        public const string PartToolbarSlot = "PART_ToolbarSlot";

        private UIElement _toolbarSlot;
        private bool _syncingPreference;

        static ToolbarHost()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(ToolbarHost),
                new FrameworkPropertyMetadata(typeof(ToolbarHost)));
        }

        public ToolbarHost()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public static readonly DependencyProperty PositionProperty = DependencyProperty.Register(
            nameof(Position), typeof(ToolbarPosition), typeof(ToolbarHost),
            new FrameworkPropertyMetadata(
                ToolbarPosition.Top,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnPositionChanged));

        /// <summary>工具栏槽位。默认上侧。</summary>
        public ToolbarPosition Position
        {
            get => (ToolbarPosition)GetValue(PositionProperty);
            set => SetValue(PositionProperty, value);
        }

        public static readonly DependencyProperty FollowsPreferenceProperty = DependencyProperty.Register(
            nameof(FollowsPreference), typeof(bool), typeof(ToolbarHost),
            new PropertyMetadata(true, OnFollowsPreferenceChanged));

        /// <summary>
        /// true（默认）：与 <see cref="BlenderUiPreferences.Current"/> 双向同步，切换会落盘。
        /// false：只用本实例 <see cref="Position"/>。
        /// </summary>
        public bool FollowsPreference
        {
            get => (bool)GetValue(FollowsPreferenceProperty);
            set => SetValue(FollowsPreferenceProperty, value);
        }

        public static readonly DependencyProperty ToolbarProperty = DependencyProperty.Register(
            nameof(Toolbar), typeof(Toolbar), typeof(ToolbarHost),
            new PropertyMetadata(null, OnToolbarChanged));

        /// <summary>要停放的工具栏。为空则不占槽。</summary>
        public Toolbar Toolbar
        {
            get => (Toolbar)GetValue(ToolbarProperty);
            set => SetValue(ToolbarProperty, value);
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();
            _toolbarSlot = GetTemplateChild(PartToolbarSlot) as UIElement;
            ApplyLayout();
        }

        private static void OnPositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ToolbarHost host)
            {
                host.ApplyLayout();
                host.WritePreferenceIfNeeded();
            }
        }

        private static void OnToolbarChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ToolbarHost host)
                host.ApplyLayout();
        }

        private static void OnFollowsPreferenceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ToolbarHost host)
                host.ReadPreferenceIfNeeded();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            BlenderUiPreferences.Current.PropertyChanged -= OnPreferenceChanged;
            BlenderUiPreferences.Current.PropertyChanged += OnPreferenceChanged;
            ReadPreferenceIfNeeded();
            ApplyLayout();
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

        private void ReadPreferenceIfNeeded()
        {
            if (!FollowsPreference || _syncingPreference) return;
            var next = BlenderUiPreferences.Current.ToolbarPosition;
            if (Position == next) return;
            _syncingPreference = true;
            try { Position = next; }
            finally { _syncingPreference = false; }
        }

        private void WritePreferenceIfNeeded()
        {
            if (!FollowsPreference || _syncingPreference) return;
            if (BlenderUiPreferences.Current.ToolbarPosition == Position) return;
            _syncingPreference = true;
            try { BlenderUiPreferences.Current.ToolbarPosition = Position; }
            finally { _syncingPreference = false; }
        }

        private void ApplyLayout()
        {
            if (_toolbarSlot != null)
            {
                DockPanel.SetDock(_toolbarSlot,
                    Position == ToolbarPosition.Side ? Dock.Left : Dock.Top);
            }

            if (Toolbar != null && Toolbar.Position != Position)
                Toolbar.Position = Position;
        }
    }
}
