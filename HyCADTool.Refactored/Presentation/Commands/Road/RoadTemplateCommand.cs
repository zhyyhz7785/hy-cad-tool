using System;

namespace HyCADTool.Refactored.Presentation.Commands.Road
{
    /// <summary>
    /// M3 v1：标准横断面图设计 / 出图命令（<c>hyRoadT</c>），v2 已被 <see cref="RoadCrossSectionDrawCommand"/>（<c>hyRoadCs</c>）取代。
    ///
    /// <para>
    /// 该类整体保留只为：
    /// <list type="number">
    ///   <item>兼容历史 commands.json / .cuix / 自动化脚本里硬编码的类型字符串
    ///         <c>HyCADTool.Refactored.Presentation.Commands.Road.RoadTemplateCommand</c>；</item>
    ///   <item>兼容外部 .cs 入口里 <c>new RoadTemplateCommand().Execute()</c> 的直接调用。</item>
    /// </list>
    /// 实际 UI / 业务全部委托到 <see cref="RoadCrossSectionDrawCommand"/>。下一轮发版可整体删除。
    /// </para>
    /// </summary>
    [Obsolete("v2 已使用 RoadCrossSectionDrawCommand（hyRoadCs）替代；本类仅作转发壳兼容历史入口，下一轮发版将整体下线。新代码请直接 new RoadCrossSectionDrawCommand().Execute()。", false)]
    public sealed class RoadTemplateCommand
    {
        public void Execute()
        {
            // 转发：保持同一窗口 / 同一交互链路，避免出现"两套 hyRoadT 行为"。
            new RoadCrossSectionDrawCommand().Execute();
        }
    }
}
