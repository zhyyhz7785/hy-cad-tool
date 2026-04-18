using System.Windows;
using HyCAD.BlenderUI.Theming;

namespace HyCAD.BlenderUI.Themes
{
    /// <summary>
    /// Themes/Colors.xaml 的代码后置类型。
    ///
    /// 唯一职责：构造时把自己登记给 <see cref="BlenderThemeManager"/>，让主题切换时能
    /// 反向找到所有"实际承载调色板"的 ResourceDictionary 实例并替换其 MergedDictionaries。
    ///
    /// 选择 ColorsHost 而不是 ThemeAwareDictionary 子类的原因：
    /// 通过 &lt;ResourceDictionary Source="…/Colors.xaml"/&gt; 加载 Source 时 WPF 不要求根节点
    /// 是子类型，但若是 ResourceDictionary 自身则可保留与 BlenderTheme.xaml 既有 Merge 写法
    /// 100% 兼容（无需调用方改 xmlns 前缀），迁移成本最低。
    /// </summary>
    public partial class ColorsHost : ResourceDictionary
    {
        public ColorsHost()
        {
            InitializeComponent();
            // #region agent log
            HyCAD.BlenderUI.Theming._DbgLog.W(
                "A", "Colors.xaml.cs:ColorsHost.ctor",
                "ColorsHost ctor entered",
                "{\"hash\":" + this.GetHashCode() +
                ",\"mergedCount\":" + MergedDictionaries.Count + "}");
            // #endregion
            BlenderThemeManager.RegisterPaletteHost(this);
        }
    }
}
