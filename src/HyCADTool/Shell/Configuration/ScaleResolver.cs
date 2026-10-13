using HyCADTool.Shared.Host;

namespace HyCADTool.Shared.Configuration
{
    /// <summary>
    /// 比例与钢筋参数回退值的单一真值来源。
    /// </summary>
    public static class ScaleResolver
    {
        public const double DefaultScale = 50.0;
        public const double DefaultAnchorageLength = 500.0;

        public static double GetScale() => CadHost.Settings?.Scale ?? DefaultScale;

        public static double GetAnchorageLength() =>
            CadHost.Settings?.AnchorageLength ?? DefaultAnchorageLength;
    }
}
