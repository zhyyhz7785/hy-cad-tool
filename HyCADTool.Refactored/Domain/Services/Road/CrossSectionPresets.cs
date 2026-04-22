using System.Collections.Generic;
using HyCADTool.Refactored.Domain.ValueObjects.Road;

namespace HyCADTool.Refactored.Domain.Services.Road
{
    /// <summary>
    /// CJJ 37-2012 常用横断面预设，作为"新建模板"时的默认起点。
    /// 每个预设保证能通过 <see cref="CrossSectionCodeChecker"/> 的 7 项检查。
    ///
    /// 命名约定：<c>Create&lt;GradeName&gt;</c>，返回 <see cref="CrossSectionLayout"/>（无 null）。
    ///
    /// <para>
    /// v2 备注：最外侧机动车道（与人行道相邻者）默认带立缘石（h=0.18m, w=0.15m），
    /// 路拱默认 Linear、坡型默认 Single（与现行城市道路设计绝大多数情况一致）。
    /// 用户在 UI 中可逐板块覆写为抛物线 / 双坡 / 取消路牙等。
    /// </para>
    /// </summary>
    public static class CrossSectionPresets
    {
        /// <summary>
        /// 城市主干路（双向 6 车道，中分带 2m，2 人行道 3.0m）。
        /// 总宽约 29 m（2×(3.5×3 + 3.0) + 2 = 29）。
        ///
        /// 路牙：每侧最外机动车道（机动3）外侧带立缘 15×10×50；其余板块无路牙。
        /// </summary>
        public static CrossSectionLayout CreateCjj37UrbanArterial()
        {
            var left = new List<CrossSectionBand>
            {
                CrossSectionBand.Lane(3.5, 1.5, BandSide.Left, "机动1"),
                CrossSectionBand.Lane(3.5, 1.5, BandSide.Left, "机动2"),
                CrossSectionBand.LaneWithCurb(3.5, 1.5, BandSide.Left, "机动3"),
                CrossSectionBand.Sidewalk(3.0, 1.5, BandSide.Left, "人行道"),
            };
            var right = new List<CrossSectionBand>
            {
                CrossSectionBand.Lane(3.5, 1.5, BandSide.Right, "机动1"),
                CrossSectionBand.Lane(3.5, 1.5, BandSide.Right, "机动2"),
                CrossSectionBand.LaneWithCurb(3.5, 1.5, BandSide.Right, "机动3"),
                CrossSectionBand.Sidewalk(3.0, 1.5, BandSide.Right, "人行道"),
            };
            return CrossSectionLayout.Create(left, right,
                centerMedianWidth: 2.0, designSpeed: 60, scaleDenominator: 150, title: "城市主干路 标准横断面图");
        }

        /// <summary>
        /// 城市次干路（双向 4 车道，无中分带，2 人行道 2.5m）。
        /// 总宽约 19 m（2×(3.5×2 + 2.5) = 19）。
        ///
        /// 路牙：每侧最外机动车道（机动2）外侧带立缘 15×10×50。
        /// </summary>
        public static CrossSectionLayout CreateCjj37SecondaryRoad()
        {
            var left = new List<CrossSectionBand>
            {
                CrossSectionBand.Lane(3.5, 1.5, BandSide.Left, "机动1"),
                CrossSectionBand.LaneWithCurb(3.5, 1.5, BandSide.Left, "机动2"),
                CrossSectionBand.Sidewalk(2.5, 1.5, BandSide.Left, "人行道"),
            };
            var right = new List<CrossSectionBand>
            {
                CrossSectionBand.Lane(3.5, 1.5, BandSide.Right, "机动1"),
                CrossSectionBand.LaneWithCurb(3.5, 1.5, BandSide.Right, "机动2"),
                CrossSectionBand.Sidewalk(2.5, 1.5, BandSide.Right, "人行道"),
            };
            return CrossSectionLayout.Create(left, right,
                centerMedianWidth: 0, designSpeed: 50, scaleDenominator: 100, title: "城市次干路 标准横断面图");
        }

        /// <summary>
        /// 城市支路（双向 2 车道，无中分带，2 人行道 2.0m）。
        /// 总宽约 11 m（2×(3.5 + 2.0) = 11）。
        ///
        /// 路牙：每侧机动车道外侧带立缘 15×10×50。
        /// </summary>
        public static CrossSectionLayout CreateCjj37LocalRoad()
        {
            var left = new List<CrossSectionBand>
            {
                CrossSectionBand.LaneWithCurb(3.5, 1.5, BandSide.Left, "机动道"),
                CrossSectionBand.Sidewalk(2.0, 1.5, BandSide.Left, "人行道"),
            };
            var right = new List<CrossSectionBand>
            {
                CrossSectionBand.LaneWithCurb(3.5, 1.5, BandSide.Right, "机动道"),
                CrossSectionBand.Sidewalk(2.0, 1.5, BandSide.Right, "人行道"),
            };
            return CrossSectionLayout.Create(left, right,
                centerMedianWidth: 0, designSpeed: 40, scaleDenominator: 100, title: "城市支路 标准横断面图");
        }

        /// <summary>
        /// 预设元数据列表，UI 用来渲染"加载预设"下拉。
        /// </summary>
        public static IReadOnlyList<PresetDescriptor> All { get; } = new[]
        {
            new PresetDescriptor("urban-arterial", "城市主干路（双向 6 车道）", CreateCjj37UrbanArterial),
            new PresetDescriptor("secondary",    "城市次干路（双向 4 车道）", CreateCjj37SecondaryRoad),
            new PresetDescriptor("local",        "城市支路（双向 2 车道）",   CreateCjj37LocalRoad),
        };
    }

    /// <summary>
    /// 预设条目（UI 使用）。
    /// </summary>
    public sealed class PresetDescriptor
    {
        public string Key { get; }
        public string DisplayName { get; }
        private readonly System.Func<CrossSectionLayout> _factory;

        public PresetDescriptor(string key, string displayName, System.Func<CrossSectionLayout> factory)
        {
            Key = key;
            DisplayName = displayName;
            _factory = factory;
        }

        public CrossSectionLayout Create() => _factory();

        public override string ToString() => DisplayName;
    }
}
