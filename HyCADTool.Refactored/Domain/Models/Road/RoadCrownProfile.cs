namespace HyCADTool.Refactored.Domain.Models.Road
{
    /// <summary>
    /// 路拱形式。决定 <see cref="HyCADTool.Refactored.Domain.ValueObjects.Road.CrossSectionBand"/>
    /// 内部从内侧到外侧的纵向曲线类型。
    /// <list type="bullet">
    ///   <item><see cref="Linear"/>：直线型。内→外按横坡线性下降，仅 2 个端点，最常用。</item>
    ///   <item><see cref="Parabolic"/>：抛物线。在内→外之间按二次抛物插值若干点，路拱顶在板块中部，老旧道路或排水设计要求时使用。</item>
    ///   <item><see cref="Folded"/>：折线型。内→外分两段折线，适配双坡或路拱顶不在中点的情况。</item>
    /// </list>
    /// </summary>
    public enum RoadCrownProfile
    {
        Linear = 0,
        Parabolic = 1,
        Folded = 2,
    }
}
