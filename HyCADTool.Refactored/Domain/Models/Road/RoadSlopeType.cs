namespace HyCADTool.Refactored.Domain.Models.Road
{
    /// <summary>
    /// 板块坡型。
    /// <list type="bullet">
    ///   <item><see cref="Single"/>：单坡。整个板块按同一横坡方向（默认外低）。城市道路绝大多数。</item>
    ///   <item><see cref="Double"/>：双坡。板块从中央分两侧降，常用于无分隔带的对称机动车道。</item>
    /// </list>
    /// </summary>
    public enum RoadSlopeType
    {
        Single = 0,
        Double = 1,
    }
}
