using System.Windows;

namespace HyCAD.BlenderUI.Theming
{
    /// <summary>
    /// 在宿主根元素（合并了 BlenderTheme/Metrics 的 Window 或 UserControl）上，
    /// 将缩放后的 Metric 字典插入 <see cref="ResourceDictionary.MergedDictionaries"/> 首位，
    /// 使 <c>{DynamicResource Metric_*}</c> 优先命中派生值。
    /// </summary>
    public static class BlenderMetricsOverlay
    {
        public static readonly DependencyProperty AttachProperty = DependencyProperty.RegisterAttached(
            "Attach",
            typeof(bool),
            typeof(BlenderMetricsOverlay),
            new PropertyMetadata(false, OnAttachChanged));

        public static bool GetAttach(DependencyObject obj) => (bool)obj.GetValue(AttachProperty);

        public static void SetAttach(DependencyObject obj, bool value) => obj.SetValue(AttachProperty, value);

        private static void OnAttachChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is FrameworkElement fe) || !(bool)e.NewValue) return;

            void OnLoaded(object _, RoutedEventArgs __)
            {
                fe.Loaded -= OnLoaded;
                BlenderMetricsScaleManager.EnsureOverlayFirstMerged(fe);
            }

            if (fe.IsLoaded)
                BlenderMetricsScaleManager.EnsureOverlayFirstMerged(fe);
            else
                fe.Loaded += OnLoaded;
        }
    }
}
