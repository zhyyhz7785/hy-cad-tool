using HyCADTool.Shell.ViewModels;

namespace HyCADTool.Shell.Configuration
{
    /// <summary>
    /// 比例与钢筋参数回退值的单一真值来源。
    /// </summary>
    public static class ScaleResolver
    {
        public const double DefaultScale = 40.0;
        public const double DefaultAnchorageLength = 500.0;

        public static double GetScale() => SettingsPanelViewModel.Current?.Scale ?? DefaultScale;

        public static double GetAnchorageLength() =>
            SettingsPanelViewModel.Current?.AnchorageLength ?? DefaultAnchorageLength;
    }
}
