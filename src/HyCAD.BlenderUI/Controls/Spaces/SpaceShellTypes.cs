using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;
using Blend = HyCAD.BlenderUI.Controls;

namespace HyCAD.BlenderUI.Controls.Spaces
{
    internal static class SpaceShellChrome
    {
        internal static UIElement Placeholder(string header, string description)
        {
            var dock = new DockPanel();
            var eh = new Blend.EditorHeader();
            var title = new TextBlock { Text = header };
            title.SetResourceReference(TextBlock.ForegroundProperty, "Brush_TextPrimary");
            eh.LeftContent = title;
            DockPanel.SetDock(eh, Dock.Top);
            dock.Children.Add(eh);

            var tb = new TextBlock
            {
                Text = description,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Top
            };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "Brush_TextSecondary");
            var border = new Border { Padding = new Thickness(8), Child = tb };
            border.SetResourceReference(Border.BackgroundProperty, "Brush_RegionBack");
            dock.Children.Add(border);
            return dock;
        }
    }

    /// <summary>3D 视口壳：空 Canvas，供宿主注入 DirectX/OpenGL。</summary>
    public sealed class View3DSpace : UserControl
    {
        public View3DSpace()
        {
            SpaceShellTheme.MergeDefault(this);
            var dock = new DockPanel();
            var eh = new Blend.EditorHeader();
            var title = new TextBlock { Text = "View3D" };
            title.SetResourceReference(TextBlock.ForegroundProperty, "Brush_TextPrimary");
            eh.LeftContent = title;
            eh.RightContent = new Blend.ToolbarPositionSwitch();
            DockPanel.SetDock(eh, Dock.Top);
            dock.Children.Add(eh);

            var canvas = new Canvas { MinHeight = 120 };
            canvas.SetResourceReference(Canvas.BackgroundProperty, "Brush_RegionBack");
            var hint = new TextBlock
            {
                Text = "Viewport — 宿主可注入 3D 视图（PaletteSet 下无 D3DImage 自绘约束仍适用）",
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 360
            };
            hint.SetResourceReference(TextBlock.ForegroundProperty, "Brush_TextSecondary");
            Canvas.SetLeft(hint, 8);
            Canvas.SetTop(hint, 8);
            canvas.Children.Add(hint);

            var host = new Blend.ToolbarHost { Toolbar = View3DToolbarFactory.Create() };
            host.Content = canvas;
            dock.Children.Add(host);
            Content = dock;
        }
    }

    public sealed class OutlinerSpace : UserControl
    {
        public OutlinerSpace()
        {
            SpaceShellTheme.MergeDefault(this);
            var dock = new DockPanel();
            var eh = new Blend.EditorHeader();
            var title = new TextBlock { Text = "Outliner" };
            title.SetResourceReference(TextBlock.ForegroundProperty, "Brush_TextPrimary");
            eh.LeftContent = title;
            DockPanel.SetDock(eh, Dock.Top);
            dock.Children.Add(eh);

            var tv = new TreeView();
            var scene = new TreeViewItem { Header = "Scene Collection", IsExpanded = true };
            var col = new TreeViewItem { Header = "Collection", IsExpanded = true };
            col.Items.Add(new TreeViewItem { Header = "Cube" });
            col.Items.Add(new TreeViewItem { Header = "Light" });
            scene.Items.Add(col);
            tv.Items.Add(scene);
            dock.Children.Add(tv);

            Loaded += (_, __) =>
            {
                if (tv.Style == null && tv.TryFindResource("BlenderOutliner") is Style st)
                    tv.Style = st;
            };

            Content = dock;
        }
    }

    public sealed class PropertiesSpace : UserControl
    {
        public PropertiesSpace()
        {
            SpaceShellTheme.MergeDefault(this);
            var pe = new Blend.PropertyEditor
            {
                Title = "Properties",
                ShowSearch = true,
                ShowIconBar = true
            };
            pe.Content = new TextBlock
            {
                Text = "属性面板壳 — IconTabBar + 滚动区（MVP 占位）",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(8)
            };
            ((TextBlock)pe.Content).SetResourceReference(TextBlock.ForegroundProperty, "Brush_TextSecondary");
            Content = pe;
        }
    }

    public sealed class GraphEditorSpace : UserControl
    {
        public GraphEditorSpace()
        {
            SpaceShellTheme.MergeDefault(this);
            Content = SpaceShellChrome.Placeholder("Graph Editor", "曲线编辑器壳（MVP）。");
        }
    }

    public sealed class DopeSheetSpace : UserControl
    {
        public DopeSheetSpace()
        {
            SpaceShellTheme.MergeDefault(this);
            Content = SpaceShellChrome.Placeholder("Dope Sheet", "时间轴关键帧表壳（MVP）。");
        }
    }

    public sealed class NlaEditorSpace : UserControl
    {
        public NlaEditorSpace()
        {
            SpaceShellTheme.MergeDefault(this);
            Content = SpaceShellChrome.Placeholder("NLA Editor", "非线性动画壳（MVP）。");
        }
    }

    public sealed class ImageEditorSpace : UserControl
    {
        public ImageEditorSpace()
        {
            SpaceShellTheme.MergeDefault(this);
            Content = SpaceShellChrome.Placeholder("Image Editor", "图像/UV 壳（MVP）。");
        }
    }

    public sealed class NodeEditorSpace : UserControl
    {
        public NodeEditorSpace()
        {
            SpaceShellTheme.MergeDefault(this);
            Content = SpaceShellChrome.Placeholder("Node Editor",
                "节点编辑器壳（MVP）。可在此放置 ItemsControl + 自定义 NodeSocket。");
        }
    }

    public sealed class SequencerSpace : UserControl
    {
        public SequencerSpace()
        {
            SpaceShellTheme.MergeDefault(this);
            Content = SpaceShellChrome.Placeholder("Video Sequencer", "视频序列编辑器壳（MVP）。");
        }
    }

    public sealed class MovieClipSpace : UserControl
    {
        public MovieClipSpace()
        {
            SpaceShellTheme.MergeDefault(this);
            Content = SpaceShellChrome.Placeholder("Movie Clip Editor", "影片剪辑壳（MVP）。");
        }
    }

    public sealed class TextEditorSpace : UserControl
    {
        public TextEditorSpace()
        {
            SpaceShellTheme.MergeDefault(this);
            var dock = new DockPanel();
            var eh = new Blend.EditorHeader();
            var title = new TextBlock { Text = "Text Editor" };
            title.SetResourceReference(TextBlock.ForegroundProperty, "Brush_TextPrimary");
            eh.LeftContent = title;
            DockPanel.SetDock(eh, Dock.Top);
            dock.Children.Add(eh);
            var box = new TextBox
            {
                Text = "# Python / 文本脚本占位",
                AcceptsReturn = true,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                MinHeight = 120
            };
            box.SetResourceReference(TextBox.StyleProperty, "BlenderTextBox");
            Loaded += (_, __) =>
            {
                if (box.Style == null && box.TryFindResource("BlenderTextBox") is Style st)
                    box.Style = st;
            };
            dock.Children.Add(box);
            Content = dock;
        }
    }

    public sealed class InfoSpace : UserControl
    {
        public InfoSpace()
        {
            SpaceShellTheme.MergeDefault(this);
            Content = SpaceShellChrome.Placeholder("Info", "操作报告 / 信息日志壳（MVP）。");
        }
    }

    public sealed class ConsoleSpace : UserControl
    {
        public ConsoleSpace()
        {
            SpaceShellTheme.MergeDefault(this);
            Content = SpaceShellChrome.Placeholder("Python Console", "交互控制台壳（MVP）。");
        }
    }

    public sealed class FileBrowserSpace : UserControl
    {
        public FileBrowserSpace()
        {
            SpaceShellTheme.MergeDefault(this);
            var dock = new DockPanel();
            var eh = new Blend.EditorHeader();
            var title = new TextBlock { Text = "File Browser" };
            title.SetResourceReference(TextBlock.ForegroundProperty, "Brush_TextPrimary");
            eh.LeftContent = title;
            DockPanel.SetDock(eh, Dock.Top);
            dock.Children.Add(eh);
            var lb = new ListBox { MinHeight = 100 };
            lb.Items.Add("..");
            lb.Items.Add("Documents");
            lb.Items.Add("blend");
            dock.Children.Add(lb);
            Loaded += (_, __) =>
            {
                if (lb.Style == null && lb.TryFindResource("BlenderListBox") is Style st)
                    lb.Style = st;
            };
            Content = dock;
        }
    }

    public sealed class PreferencesSpace : UserControl
    {
        public PreferencesSpace()
        {
            SpaceShellTheme.MergeDefault(this);
            var dock = new DockPanel();
            var eh = new Blend.EditorHeader();
            var title = new TextBlock { Text = "Preferences" };
            title.SetResourceReference(TextBlock.ForegroundProperty, "Brush_TextPrimary");
            eh.LeftContent = title;
            DockPanel.SetDock(eh, Dock.Top);
            dock.Children.Add(eh);

            var body = new StackPanel { Margin = new Thickness(8) };
            var label = new TextBlock
            {
                Text = "视口工具栏位置（默认上侧；写入 hy.blender.toolbarPos）",
                Margin = new Thickness(0, 0, 0, 8),
                TextWrapping = TextWrapping.Wrap
            };
            label.SetResourceReference(TextBlock.ForegroundProperty, "Brush_TextSecondary");
            body.Children.Add(label);
            body.Children.Add(new Blend.ToolbarPositionSwitch());
            dock.Children.Add(body);
            Content = dock;
        }
    }

    public sealed class SpreadsheetSpace : UserControl
    {
        public SpreadsheetSpace()
        {
            SpaceShellTheme.MergeDefault(this);
            Content = SpaceShellChrome.Placeholder("Spreadsheet", "属性表格壳（MVP）。");
        }
    }

    public sealed class TopBarSpace : UserControl
    {
        public TopBarSpace()
        {
            SpaceShellTheme.MergeDefault(this);
            var bar = new Blend.EditorHeader { Height = 28 };
            bar.LeftContent = new TextBlock { Text = "Top Bar — 菜单 / 工作区（全局条壳）" };
            ((TextBlock)bar.LeftContent).SetResourceReference(TextBlock.ForegroundProperty, "Brush_TextPrimary");
            Content = bar;
        }
    }

    public sealed class StatusBarSpace : UserControl
    {
        public StatusBarSpace()
        {
            SpaceShellTheme.MergeDefault(this);
            var tb = new TextBlock { Text = "Status — 统计 / 进度（全局条壳）" };
            tb.SetResourceReference(TextBlock.ForegroundProperty, "Brush_TextSecondary");
            var bar = new Blend.StatusBar { LeftContent = tb };
            Content = bar;
        }
    }

    public sealed class TimelineSpace : UserControl
    {
        public TimelineSpace()
        {
            SpaceShellTheme.MergeDefault(this);
            Content = SpaceShellChrome.Placeholder("Timeline", "时间线壳（MVP）。");
        }
    }

    internal static class View3DToolbarFactory
    {
        internal static Blend.Toolbar Create()
        {
            var tb = new Blend.Toolbar();
            tb.Items.Add(IconToggle("选择",
                "M 2,1 L 2,11 L 5,8 L 7,12 L 9,11 L 7,7 L 11,7 Z", isFill: true, isChecked: true));
            tb.Items.Add(IconToggle("移动",
                "M 8,1 L 6,3 L 7,3 L 7,7 L 3,7 L 3,6 L 1,8 L 3,10 L 3,9 L 7,9 L 7,13 L 6,13 L 8,15 L 10,13 L 9,13 L 9,9 L 13,9 L 13,10 L 15,8 L 13,6 L 13,7 L 9,7 L 9,3 L 10,3 Z",
                isFill: true));
            tb.Items.Add(IconToggle("旋转",
                "M 8,2 A 6,6 0 1 1 2,8 L 0,8 L 3,11 L 6,8 L 4,8 A 4,4 0 1 0 8,4 Z",
                isFill: true));
            tb.Items.Add(IconToggle("缩放",
                "M 1,1 L 7,1 L 7,3 L 3,3 L 3,7 L 1,7 Z M 9,9 L 13,9 L 13,13 L 9,13 Z",
                isFill: true));
            return tb;
        }

        private static ToggleButton IconToggle(string tip, string data, bool isFill, bool isChecked = false)
        {
            var path = new Path
            {
                Width = 16,
                Height = 16,
                Data = Geometry.Parse(data),
                Stretch = Stretch.Uniform
            };
            if (isFill)
                path.SetResourceReference(Shape.FillProperty, "Brush_TextPrimary");
            else
            {
                path.Fill = Brushes.Transparent;
                path.StrokeThickness = 1.6;
                path.SetResourceReference(Shape.StrokeProperty, "Brush_TextPrimary");
            }

            var btn = new ToggleButton { ToolTip = tip, IsChecked = isChecked, Content = path };
            btn.SetResourceReference(FrameworkElement.StyleProperty, "BlenderToolbarIconButton");
            return btn;
        }
    }
}
