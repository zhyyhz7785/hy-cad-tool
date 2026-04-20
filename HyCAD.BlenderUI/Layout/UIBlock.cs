using System.Windows.Controls;

namespace HyCAD.BlenderUI.Layout
{
    /// <summary>uiBlock：布局入口（阶段 3）。</summary>
    public sealed class UIBlock
    {
        public StackPanel RootPanel { get; } = new StackPanel();

        public UILayout Root { get; }

        public UIBlock()
        {
            Root = new UILayout(RootPanel);
        }
    }
}
