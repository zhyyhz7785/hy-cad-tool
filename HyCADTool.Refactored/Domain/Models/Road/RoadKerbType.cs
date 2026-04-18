namespace HyCADTool.Refactored.Domain.Models.Road
{
    /// <summary>
    /// 路牙（缘石）类型。对应规范图集中常见的四种端部修饰：
    /// <list type="bullet">
    ///   <item><see cref="None"/>：无路牙，板块外缘直接接续下一板块。</item>
    ///   <item><see cref="Curb"/>：立缘石。形成显著高差（典型 0.15~0.20 m），机动车道与人行道之间常用。</item>
    ///   <item><see cref="Plain"/>：平石。与路面齐平或微凸，常用于绿化带边界、非机动车道边界。</item>
    ///   <item><see cref="Combined"/>：立缘 + 平石组合，规范图集中常见的"立缘背靠平石"形态。</item>
    /// </list>
    /// </summary>
    public enum RoadKerbType
    {
        None = 0,
        Curb = 1,
        Plain = 2,
        Combined = 3,
    }
}
