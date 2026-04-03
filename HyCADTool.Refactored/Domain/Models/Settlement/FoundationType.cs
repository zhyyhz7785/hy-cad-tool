namespace HyCADTool.Refactored.Domain.Models.Settlement
{
    /// <summary>
    /// 基础类型枚举（决定沉降计算方法）
    /// </summary>
    public enum FoundationType
    {
        /// <summary>天然基础 — GB 50007 §5.3.5</summary>
        Natural = 0,

        /// <summary>复合地基 — JGJ 79 §7.1.7（Es 乘以 ξ = fspk/fak）</summary>
        Composite = 1,

        /// <summary>桩基础 — JGJ 94 §5.5.6（等效深基础法，计算面下移至桩端）</summary>
        Pile = 2
    }
}
