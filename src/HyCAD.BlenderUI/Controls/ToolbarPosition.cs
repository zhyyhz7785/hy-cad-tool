namespace HyCAD.BlenderUI.Controls
{
    /// <summary>
    /// 3D 视口工具栏（对应 Blender T 键 Tools 区）的摆放位置。
    /// 默认 <see cref="Top"/>：视口上方横向图标条（HyMd 风格）；
    /// <see cref="Side"/>：视口左侧纵向图标条（经典 Blender 左侧 T）。
    /// </summary>
    public enum ToolbarPosition
    {
        /// <summary>上侧：内容/视口上方的横向图标条。</summary>
        Top = 0,

        /// <summary>侧面：视口左侧的纵向图标条。</summary>
        Side = 1,
    }
}
