using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HyCADTool.Domain.ValueObjects.Road;
using HyCADTool.Features.Road.CrossSection.ViewModels;

namespace HyCADTool.Features.Road.CrossSection.Behaviors
{
    public static class OutlinerDragDropBehavior
    {
        public static readonly DependencyProperty EnabledProperty =
            DependencyProperty.RegisterAttached(
                "Enabled",
                typeof(bool),
                typeof(OutlinerDragDropBehavior),
                new PropertyMetadata(false, OnEnabledChanged));

        private static readonly DependencyProperty DragStartPointProperty =
            DependencyProperty.RegisterAttached(
                "DragStartPoint",
                typeof(Point),
                typeof(OutlinerDragDropBehavior),
                new PropertyMetadata(default(Point)));

        private static readonly DependencyProperty DragSourceProperty =
            DependencyProperty.RegisterAttached(
                "DragSource",
                typeof(BandRowViewModel),
                typeof(OutlinerDragDropBehavior),
                new PropertyMetadata(null));

        public static void SetEnabled(DependencyObject element, bool value) => element.SetValue(EnabledProperty, value);
        public static bool GetEnabled(DependencyObject element) => (bool)element.GetValue(EnabledProperty);

        private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var tree = d as TreeView;
            if (tree == null) return;
            if ((bool)e.NewValue)
            {
                tree.PreviewMouseLeftButtonDown += OnPreviewMouseLeftButtonDown;
                tree.MouseMove += OnMouseMove;
                tree.Drop += OnDrop;
            }
            else
            {
                tree.PreviewMouseLeftButtonDown -= OnPreviewMouseLeftButtonDown;
                tree.MouseMove -= OnMouseMove;
                tree.Drop -= OnDrop;
            }
        }

        private static void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var tree = sender as DependencyObject;
            if (tree == null) return;
            tree.SetValue(DragStartPointProperty, e.GetPosition((IInputElement)sender));
            tree.SetValue(DragSourceProperty, ResolveNodeFromVisual(e.OriginalSource as DependencyObject));
        }

        private static void OnMouseMove(object sender, MouseEventArgs e)
        {
            var tree = sender as DependencyObject;
            if (tree == null) return;
            var dragSource = tree.GetValue(DragSourceProperty) as BandRowViewModel;
            if (e.LeftButton != MouseButtonState.Pressed || dragSource == null) return;
            var start = (Point)tree.GetValue(DragStartPointProperty);
            var current = e.GetPosition((IInputElement)sender);
            if (System.Math.Abs(current.X - start.X) < SystemParameters.MinimumHorizontalDragDistance
                && System.Math.Abs(current.Y - start.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }
            DragDrop.DoDragDrop(tree, dragSource, DragDropEffects.Move);
        }

        private static void OnDrop(object sender, DragEventArgs e)
        {
            if (!e.Data.GetDataPresent(typeof(BandRowViewModel))) return;
            var tree = sender as TreeView;
            if (tree == null) return;
            var source = e.Data.GetData(typeof(BandRowViewModel)) as BandRowViewModel;
            var target = ResolveNodeFromVisual(e.OriginalSource as DependencyObject);
            if (source == null || target == null) return;
            if (ReferenceEquals(source, target)) return;
            if (source.Side != target.Side) return;

            var owner = source.Side == BandSide.Right
                ? FindOwnerCollection(tree, true)
                : FindOwnerCollection(tree, false);
            if (owner == null) return;

            var oldIndex = owner.IndexOf(source);
            var newIndex = owner.IndexOf(target);
            if (oldIndex < 0 || newIndex < 0 || oldIndex == newIndex) return;
            owner.Move(oldIndex, newIndex);
        }

        private static System.Collections.ObjectModel.ObservableCollection<BandRowViewModel> FindOwnerCollection(TreeView tree, bool right)
        {
            var vm = tree.DataContext as CrossSectionDrawViewModel;
            if (vm == null) return null;
            return right ? vm.RightBands : vm.LeftBands;
        }

        private static BandRowViewModel ResolveNodeFromVisual(DependencyObject obj)
        {
            while (obj != null)
            {
                if (obj is FrameworkElement fe && fe.DataContext is BandRowViewModel row) return row;
                obj = System.Windows.Media.VisualTreeHelper.GetParent(obj);
            }
            return null;
        }
    }
}
