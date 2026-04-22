using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HyCADTool.Refactored.Presentation.ViewModels.Road;

namespace HyCADTool.Refactored.Presentation.Behaviors
{
    public static class OutlinerDragDropBehavior
    {
        public static readonly DependencyProperty EnabledProperty =
            DependencyProperty.RegisterAttached(
                "Enabled",
                typeof(bool),
                typeof(OutlinerDragDropBehavior),
                new PropertyMetadata(false, OnEnabledChanged));

        private static Point _dragStartPoint;
        private static BandRowViewModel _dragSource;

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
            _dragStartPoint = e.GetPosition((IInputElement)sender);
            _dragSource = ResolveNodeFromVisual(e.OriginalSource as DependencyObject);
        }

        private static void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton != MouseButtonState.Pressed || _dragSource == null) return;
            var current = e.GetPosition((IInputElement)sender);
            if (System.Math.Abs(current.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
                && System.Math.Abs(current.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
            {
                return;
            }
            DragDrop.DoDragDrop((DependencyObject)sender, _dragSource, DragDropEffects.Move);
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

            var owner = source.Side == Domain.ValueObjects.Road.BandSide.Right
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
