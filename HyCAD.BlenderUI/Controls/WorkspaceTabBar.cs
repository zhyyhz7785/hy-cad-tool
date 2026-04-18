using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace HyCAD.BlenderUI.Controls
{
    /// <summary>
    /// 顶部水平工作区标签条（对应 Blender 顶部 Layout / Modeling / Sculpting 切换）。
    /// ItemsSource 每项为 <see cref="WorkspaceTabItem"/>；支持双向绑定 SelectedIndex。
    /// </summary>
    public class WorkspaceTabBar : ItemsControl
    {
        static WorkspaceTabBar()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(WorkspaceTabBar),
                new FrameworkPropertyMetadata(typeof(WorkspaceTabBar)));
        }

        public WorkspaceTabBar()
        {
            // 冒泡捕获子项点击：转 SelectedIndex
            AddHandler(WorkspaceTabItem.MouseLeftButtonDownEvent,
                new MouseButtonEventHandler(OnItemClickCapture), true);
        }

        private void OnItemClickCapture(object sender, MouseButtonEventArgs e)
        {
            var fe = e.OriginalSource as DependencyObject;
            while (fe != null && !(fe is WorkspaceTabItem))
                fe = VisualTreeHelper.GetParent(fe);
            if (fe is WorkspaceTabItem it)
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

        protected override DependencyObject GetContainerForItemOverride() => new WorkspaceTabItem();
        protected override bool IsItemItsOwnContainerOverride(object item) => item is WorkspaceTabItem;

        public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register(
            nameof(SelectedIndex), typeof(int), typeof(WorkspaceTabBar),
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
            var bar = (WorkspaceTabBar)d;
            int count = bar.Items.Count;
            for (int i = 0; i < count; i++)
            {
                if (bar.ItemContainerGenerator.ContainerFromIndex(i) is WorkspaceTabItem it)
                    it.IsSelected = (i == bar.SelectedIndex);
            }
        }

        protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
        {
            base.PrepareContainerForItemOverride(element, item);
            if (element is WorkspaceTabItem it)
            {
                int idx = ItemContainerGenerator.IndexFromContainer(it);
                it.IsSelected = (idx == SelectedIndex);
            }
        }
    }
}
