using System;
using System.Windows;
using System.Windows.Controls;
using HyCAD.BlenderUI.Screen.Areas;

namespace HyCAD.BlenderUI.Controls.ScreenView
{
    /// <summary>根据 <see cref="AreaTreeModel"/> 递归生成 Grid（阶段 4b）。</summary>
    public class BlenderScreen : ContentControl
    {
        public static readonly DependencyProperty ModelProperty = DependencyProperty.Register(
            nameof(Model), typeof(AreaTreeModel), typeof(BlenderScreen),
            new PropertyMetadata(null, OnModelChanged));

        public AreaTreeModel Model
        {
            get => (AreaTreeModel)GetValue(ModelProperty);
            set => SetValue(ModelProperty, value);
        }

        static BlenderScreen()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BlenderScreen),
                new FrameworkPropertyMetadata(typeof(BlenderScreen)));
        }

        private static void OnModelChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BlenderScreen s)
            {
                if (e.OldValue is AreaTreeModel oldM)
                    oldM.Changed -= s.Rebuild;
                if (e.NewValue is AreaTreeModel newM)
                    newM.Changed += s.Rebuild;
                s.Rebuild(s, EventArgs.Empty);
            }
        }

        private void Rebuild(object sender, EventArgs e)
        {
            if (Model?.Root == null)
            {
                Content = null;
                return;
            }
            Content = Build(Model.Root);
        }

        private static UIElement Build(AreaTreeNode node)
        {
            switch (node)
            {
                case AreaLeaf leaf:
                    return new BlenderArea { SpaceType = leaf.SpaceType };
                case AreaSplit sp:
                    var grid = new Grid();
                    if (sp.IsHorizontal)
                    {
                        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(sp.SplitFactor, GridUnitType.Star) });
                        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1 - sp.SplitFactor, GridUnitType.Star) });
                        var left = Build(sp.Left);
                        var right = Build(sp.Right);
                        Grid.SetColumn(left, 0);
                        Grid.SetColumn(right, 1);
                        grid.Children.Add(left);
                        grid.Children.Add(right);
                    }
                    else
                    {
                        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(sp.SplitFactor, GridUnitType.Star) });
                        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1 - sp.SplitFactor, GridUnitType.Star) });
                        var top = Build(sp.Left);
                        var bottom = Build(sp.Right);
                        Grid.SetRow(top, 0);
                        Grid.SetRow(bottom, 1);
                        grid.Children.Add(top);
                        grid.Children.Add(bottom);
                    }
                    return grid;
                default:
                    return new TextBlock { Text = "?" };
            }
        }
    }
}
