using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace HyCAD.BlenderUI.Controls
{
    /// <summary>
    /// 左侧垂直图标 Tab 栏：ItemsSource 每项为 IconTabItem；
    /// 支持双向绑定 SelectedIndex / SelectedItem；宽度固定由 Metric_IconBarWidth 决定。
    /// </summary>
    public class IconTabBar : ItemsControl
    {
        static IconTabBar()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(IconTabBar),
                new FrameworkPropertyMetadata(typeof(IconTabBar)));
        }

        public IconTabBar()
        {
            AddHandler(IconTabItem.MouseLeftButtonDownEvent,
                new MouseButtonEventHandler(OnItemClickCapture), true);
        }

        private void OnItemClickCapture(object sender, MouseButtonEventArgs e)
        {
            var fe = e.OriginalSource as DependencyObject;
            while (fe != null && !(fe is IconTabItem))
                fe = System.Windows.Media.VisualTreeHelper.GetParent(fe);
            if (fe is IconTabItem it)
            {
                int idx = ItemContainerGenerator.IndexFromContainer(it);
                if (idx < 0)
                {
                    IList list = ItemsSource as IList ?? Items;
                    idx = list != null ? list.IndexOf(it) : -1;
                }
                if (idx >= 0) SelectedIndex = idx;
            }
        }

        protected override DependencyObject GetContainerForItemOverride() => new IconTabItem();
        protected override bool IsItemItsOwnContainerOverride(object item) => item is IconTabItem;

        public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
            nameof(SelectedIndex), typeof(int), typeof(IconTabBar),
            new FrameworkPropertyMetadata(-1,
                FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                OnSelectedIndexChanged));

        public int SelectedIndex
        {
            get => (int)GetValue(SelectedIndexProperty);
            set => SetValue(SelectedIndexProperty, value);
        }

        private static void OnSelectedIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var bar = (IconTabBar)d;
            int count = bar.Items.Count;
            for (int i = 0; i < count; i++)
            {
                if (bar.ItemContainerGenerator.ContainerFromIndex(i) is IconTabItem it)
                    it.IsSelected = (i == bar.SelectedIndex);
            }
        }

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);
            if (element is IconTabItem it)
            {
                int idx = ItemContainerGenerator.IndexFromContainer(it);
                it.IsSelected = (idx == SelectedIndex);
            }
        }
    }
}
