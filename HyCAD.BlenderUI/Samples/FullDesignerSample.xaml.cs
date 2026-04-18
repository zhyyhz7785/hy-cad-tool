using HyCAD.BlenderUI.Controls;

namespace HyCAD.BlenderUI.Samples
{
    /// <summary>
    /// FullDesignerSample：完整 Editor 体系示范（顶 EditorHeader + 左 Toolbar + 中 Viewport + 右 NPanel + 底 StatusBar），
    /// 含 3 个 BlenderSplitter（横向 1 + 纵向 2）。
    /// </summary>
    public partial class FullDesignerSample : BlenderWindow
    {
        public FullDesignerSample()
        {
            InitializeComponent();
        }
    }
}
