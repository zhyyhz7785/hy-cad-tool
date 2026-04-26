using System.Windows;
using System.Windows.Controls;
using HyCAD.BlenderUI.Controls.Spaces;
using HyCAD.BlenderUI.Screen.Areas;

namespace HyCAD.BlenderUI.Controls.ScreenView
{
    /// <summary>单个 Area：显示 SpaceType 与占位内容。</summary>
    public class BlenderArea : ContentControl
    {
        static BlenderArea()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BlenderArea),
                new FrameworkPropertyMetadata(typeof(BlenderArea)));
        }

        public static readonly DependencyProperty SpaceTypeProperty = DependencyProperty.Register(
            nameof(SpaceType), typeof(SpaceTypeId), typeof(BlenderArea),
            new PropertyMetadata(SpaceTypeId.View3D, OnSpaceTypeChanged));

        private static void OnSpaceTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is BlenderArea area)
                area.SyncSpaceShell();
        }

        public SpaceTypeId SpaceType
        {
            get => (SpaceTypeId)GetValue(SpaceTypeProperty);
            set => SetValue(SpaceTypeProperty, value);
        }

        public BlenderArea()
        {
            Loaded += (_, __) => SyncSpaceShell();
            SyncSpaceShell();
        }

        private void SyncSpaceShell()
        {
            Content = SpaceShellFactory.Create(SpaceType);
        }
    }
}
