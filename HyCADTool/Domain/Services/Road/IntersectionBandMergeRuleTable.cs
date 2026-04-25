using System;
using System.Collections.Generic;
using System.Linq;
using HyCADTool.Domain.Models.Road;

namespace HyCADTool.Domain.Services.Road
{
    /// <summary>
    /// 交叉口条带合并规则表（M9.1）—— <see cref="IntersectionBandMergeRule"/> 集合 + 默认策略查询。
    ///
    /// <para><b>5 × 5 默认规则</b></para>
    /// 覆盖 5 种条带类型：绿化带 / 人行道 / 非机动车道 / 中央分隔带 / 机动车道。
    /// 优先级：机动车道(5) &gt; 中央分隔带(4) &gt; 非机动车道(3) &gt; 人行道(2) &gt; 绿化带(1)。
    ///
    /// <para><b>查询策略</b></para>
    /// <list type="number">
    ///   <item>先在 <see cref="_overrides"/>（JSON / 用户自定义）里按 (Self, Opposite) 精确匹配。</item>
    ///   <item>未命中 → 走 <see cref="DefaultStrategy"/>：相同类型走 <c>ConnectWithArc</c>，
    ///         不同类型按优先级决定 Keep。同类机动车道走 <c>BlendByLength</c>。</item>
    /// </list>
    /// </summary>
    public sealed class IntersectionBandMergeRuleTable
    {
        private readonly Dictionary<(TemplateComponentKind, TemplateComponentKind), IntersectionBandMergeRule> _overrides
            = new Dictionary<(TemplateComponentKind, TemplateComponentKind), IntersectionBandMergeRule>();

        /// <summary>默认优先级（越大越高）。</summary>
        public static readonly IReadOnlyDictionary<TemplateComponentKind, int> DefaultPriority =
            new Dictionary<TemplateComponentKind, int>
            {
                { TemplateComponentKind.GreenStrip,   1 },
                { TemplateComponentKind.Sidewalk,     2 },
                { TemplateComponentKind.NonMotorized, 3 },
                { TemplateComponentKind.MedianStrip,  4 },
                { TemplateComponentKind.Pavement,     5 },
                { TemplateComponentKind.Kerb,         0 },
                { TemplateComponentKind.Shoulder,     2 },
            };

        public IntersectionBandMergeRuleTable() { }

        public IntersectionBandMergeRuleTable(IEnumerable<IntersectionBandMergeRule> rules)
        {
            if (rules == null) return;
            foreach (var r in rules) AddOrUpdate(r);
        }

        /// <summary>当前规则条目总数（含 overrides）。</summary>
        public int Count => _overrides.Count;

        /// <summary>遍历所有 override 条目。</summary>
        public IEnumerable<IntersectionBandMergeRule> Overrides => _overrides.Values;

        /// <summary>新增 / 覆盖一条规则（按 Self+Opposite 唯一性）。</summary>
        public void AddOrUpdate(IntersectionBandMergeRule rule)
        {
            if (rule == null) return;
            var key = ((TemplateComponentKind)rule.SelfKind, (TemplateComponentKind)rule.OppositeKind);
            _overrides[key] = rule;
        }

        /// <summary>移除 (Self,Opposite) 对应的 override。</summary>
        public bool Remove(TemplateComponentKind self, TemplateComponentKind opposite)
        {
            return _overrides.Remove((self, opposite));
        }

        /// <summary>清空所有 override；默认规则仍可通过 <see cref="Resolve"/> 获取。</summary>
        public void Clear() => _overrides.Clear();

        /// <summary>
        /// 查询 (Self,Opposite) 对应的合并策略 + 优先级；
        /// 有 override 则返回 override，否则返回默认规则。
        /// </summary>
        public IntersectionBandMergeRule Resolve(TemplateComponentKind self, TemplateComponentKind opposite)
        {
            if (_overrides.TryGetValue((self, opposite), out var o)) return o;
            return DefaultRule(self, opposite);
        }

        /// <summary>计算默认规则。</summary>
        public static IntersectionBandMergeRule DefaultRule(TemplateComponentKind self, TemplateComponentKind opposite)
        {
            int selfP = DefaultPriority.TryGetValue(self, out var sp) ? sp : 0;
            int oppP = DefaultPriority.TryGetValue(opposite, out var op) ? op : 0;

            BandMergeStrategy strategy;
            if (self == opposite)
            {
                // 同类相遇：机动车道 × 机动车道 → BlendByLength（中线对接）；其他同类走圆弧过渡。
                strategy = (self == TemplateComponentKind.Pavement || self == TemplateComponentKind.MedianStrip)
                    ? BandMergeStrategy.BlendByLength
                    : BandMergeStrategy.ConnectWithArc;
            }
            else if (selfP > oppP)
            {
                strategy = BandMergeStrategy.KeepSelf;
            }
            else if (selfP < oppP)
            {
                strategy = BandMergeStrategy.KeepOpposite;
            }
            else
            {
                // 同优先级但类型不同（例如 Sidewalk × Shoulder 都是 2）→ 默认圆弧连接
                strategy = BandMergeStrategy.ConnectWithArc;
            }

            return new IntersectionBandMergeRule
            {
                SelfKind = (int)self,
                OppositeKind = (int)opposite,
                SelfPriority = selfP,
                Strategy = strategy,
            };
        }

        /// <summary>
        /// 对一组相邻臂（按每个臂的条带类型序列）批量查询合并策略。
        ///
        /// <para>用于 M9.3 集成：在 <c>hyRoadIntersection</c> 执行前，由命令层调 <see cref="IntersectionDesigner.ComputeFromAlignments"/>
        /// 得到交叉口，再调本方法对每条 Leg 外侧条带类型查表，得到"每对相邻臂要按什么策略合并"，
        /// 目前仅作日志输出给用户查看；真实几何合并由 M10 的 CorridorSweepService 接入。</para>
        /// </summary>
        public IReadOnlyList<(int LegIndexA, int LegIndexB, BandMergeStrategy Strategy)> ResolvePairs(
            IReadOnlyList<TemplateComponentKind> legBandKinds)
        {
            var result = new List<(int, int, BandMergeStrategy)>();
            if (legBandKinds == null || legBandKinds.Count < 2) return result;
            for (int i = 0; i < legBandKinds.Count; i++)
            {
                int j = (i + 1) % legBandKinds.Count;
                var rule = Resolve(legBandKinds[i], legBandKinds[j]);
                result.Add((i, j, rule.Strategy));
            }
            return result;
        }

        /// <summary>列出 5×5 默认规则矩阵，便于 UI 展示。</summary>
        public static IReadOnlyList<IntersectionBandMergeRule> DefaultFiveByFive()
        {
            var kinds = new[]
            {
                TemplateComponentKind.GreenStrip,
                TemplateComponentKind.Sidewalk,
                TemplateComponentKind.NonMotorized,
                TemplateComponentKind.MedianStrip,
                TemplateComponentKind.Pavement,
            };
            var list = new List<IntersectionBandMergeRule>(kinds.Length * kinds.Length);
            foreach (var s in kinds)
                foreach (var o in kinds)
                    list.Add(DefaultRule(s, o));
            return list;
        }
    }
}
