namespace HyCADTool.Domain.Models.Road
{
    /// <summary>
    /// 交叉口相遇时两条条带的合并策略（M9）。
    /// </summary>
    public enum BandMergeStrategy
    {
        /// <summary>保留自身条带，对方被裁掉。</summary>
        KeepSelf = 0,

        /// <summary>保留对方条带，自身被裁掉。</summary>
        KeepOpposite = 1,

        /// <summary>按长度加权混合（机动车道相遇 = 中线两侧等分）。</summary>
        BlendByLength = 2,

        /// <summary>用圆弧连接两条带（默认策略，用于转角 / 同类条带相遇）。</summary>
        ConnectWithArc = 3,
    }

    /// <summary>
    /// 交叉口条带合并规则（M9）。
    ///
    /// <para>每条规则描述"自身条带类型 × 对方条带类型 → 合并策略 + 自身优先级"。
    /// 用于在 <c>IntersectionDesigner</c> 生成交叉口几何时查表决策。</para>
    ///
    /// <para><b>M6 阶段</b></para>
    /// 本类仅作 JSON Schema v1.2.0 字段预留；完整规则库与 5×5 默认表由 M9 落实。
    /// </para>
    /// </summary>
    public sealed class IntersectionBandMergeRule
    {
        /// <summary>自身条带类型（<see cref="TemplateComponentKind"/>，用 int 存储以保持 JSON 向后兼容）。</summary>
        public int SelfKind { get; set; }

        /// <summary>对方条带类型。</summary>
        public int OppositeKind { get; set; }

        /// <summary>
        /// 自身优先级（数字越大优先级越高；绿化带 1 / 人行道 2 / 非机动车道 3 / 中央分隔带 4 / 机动车道 5）。
        /// </summary>
        public int SelfPriority { get; set; }

        /// <summary>合并策略。</summary>
        public BandMergeStrategy Strategy { get; set; } = BandMergeStrategy.ConnectWithArc;

        public override string ToString()
            => $"MergeRule[Self={(TemplateComponentKind)SelfKind}(p{SelfPriority}) × Opp={(TemplateComponentKind)OppositeKind} → {Strategy}]";
    }
}
