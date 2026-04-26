using System.Windows;
using HyCAD.BlenderUI.Theming;

namespace HyCAD.BlenderUI.Themes
{
    /// <summary>
    /// Themes/Colors.xaml 的代码后置类型（v3 — Mutable Brush Facade）。
    ///
    /// 唯一职责：构造时调用 <see cref="BlenderThemeManager.PopulateAndRegister"/>，
    /// 让管理器在本字典上为每个 Brush_* key 创建 *unfrozen* SolidColorBrush 实例。
    ///
    /// 切换主题时，管理器遍历所有 host 中已写入的 brush 实例，仅修改其 .Color 属性 ——
    /// 由于 SolidColorBrush.Color 是 DependencyProperty，DP 变化通知会自动传播给所有
    /// DynamicResource 引用方，无需依赖 ResourceDictionary 自身的 ResourcesChanged。
    ///
    /// 这是参考 Blender 主题切换思想的 WPF 等价：所有 UI 引用同一组可变颜色对象，
    /// 改对象属性即驱动重绘。
    /// </summary>
    public partial class ColorsHost : ResourceDictionary
    {
        public ColorsHost()
        {
            InitializeComponent();
            BlenderThemeManager.PopulateAndRegister(this);
        }
    }
}
