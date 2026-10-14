namespace HyCADTool.Shared.Drawing.Models
{
    /// <summary>
    /// 标注方位。Cluster 与 DimensionForReinforcement 共用；放 Shared，避免 base → structure。
    /// </summary>
    public enum DimensionFor
    {
        /// <summary>向左标注</summary>
        ForLeft,

        /// <summary>向右标注</summary>
        ForRight,

        /// <summary>向上标注</summary>
        ForUp,

        /// <summary>向下标注</summary>
        ForDown
    }
}
