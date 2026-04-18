namespace HyCADTool.Refactored.Domain.Models.Road
{
    /// <summary>
    /// 板块路面结构（铺装类型）。决定填色与未来分层结构线绘制。
    /// <list type="bullet">
    ///   <item><see cref="None"/>：未指定，按板块 <see cref="TemplateComponentKind"/> 默认上色。</item>
    ///   <item><see cref="PavementSurface"/>：机动车道路面铺装（沥青/水泥）。</item>
    ///   <item><see cref="SidewalkPaving"/>：人行道铺装（透水砖/花岗岩）。</item>
    ///   <item><see cref="NonMotorPaving"/>：非机动车道铺装（彩色沥青）。</item>
    ///   <item><see cref="GreenSoil"/>：绿化覆土。</item>
    /// </list>
    /// </summary>
    public enum RoadSurfaceLayer
    {
        None = 0,
        PavementSurface = 1,
        SidewalkPaving = 2,
        NonMotorPaving = 3,
        GreenSoil = 4,
    }
}
