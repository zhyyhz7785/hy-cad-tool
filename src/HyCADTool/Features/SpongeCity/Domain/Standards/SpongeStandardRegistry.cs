using System;
using System.Collections.Generic;

namespace HyCADTool.Features.SpongeCity.Domain.Standards
{
    /// <summary>
    /// 规范注册表。面板下拉、计算、计算书、Excel 都从这里取。
    ///
    /// 新增一套规范（不必改计算引擎）：
    /// 1. 准备一个 <see cref="SpongeStandard"/>（或子类）。填：
    ///    Surfaces（代码、名称、图层 HY-海绵-S*、默认 ψ、默认面积）、
    ///    Facilities（η、单位调蓄、面积来源代码、容积类图层 HY-海绵-F5…F8）、
    ///    Rounding（-1 = 不舍入）、
    ///    SunkenGreen / PerviousPavement（分子分母代码、阈值、分母为 0 是否通过）、
    ///    GreenRatio、PsiOutflow、MultiplySsByAlpha、UseHardeningAllocation、
    ///    Citation / WorkbookTitle / PassVerdict / FailVerdict。
    /// 2. 在本类静态构造函数加一行 <c>Register(YourStandard.Create());</c>。
    ///    同一 Id 再 Register 会替换旧项。
    /// 3. 若配建分档或校核无法用规则表表达，覆盖
    ///    <see cref="SpongeStandard.AllocationVolume"/> 或 <see cref="SpongeStandard.BuildChecks"/>。
    /// 透水铺装率分母只放人行/广场铺装代码。DB42 目前目录里没有单独的不透水人行道，
    /// 分母只有 S5（S5&gt;0 时比率为 1；S5=0 时「满足」）。把新代码加进
    /// <see cref="AreaRatioRule.DenominatorCodes"/> 即计入分母。不要把 S3、S4 放进去。
    /// </summary>
    public static class SpongeStandardRegistry
    {
        private static readonly Dictionary<string, ISpongeStandard> Map =
            new Dictionary<string, ISpongeStandard>(StringComparer.OrdinalIgnoreCase);
        private static readonly List<ISpongeStandard> Order = new List<ISpongeStandard>();

        static SpongeStandardRegistry()
        {
            Register(Db13SpongeStandard.Create());
            Register(Db42SpongeStandard.Create());
        }

        public static void Register(ISpongeStandard standard)
        {
            if (standard == null || string.IsNullOrWhiteSpace(standard.Id))
                throw new ArgumentException("standard.Id 不能为空", nameof(standard));

            if (Map.ContainsKey(standard.Id))
            {
                Map[standard.Id] = standard;
                for (int i = 0; i < Order.Count; i++)
                {
                    if (string.Equals(Order[i].Id, standard.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        Order[i] = standard;
                        return;
                    }
                }
            }

            Map[standard.Id] = standard;
            Order.Add(standard);
        }

        public static IReadOnlyList<ISpongeStandard> All => Order;

        public static ISpongeStandard GetOrDefault(string id)
        {
            if (!string.IsNullOrEmpty(id) && Map.TryGetValue(id, out var found))
                return found;
            return Map[SpongeStandardIds.Db13];
        }

        public static int IndexOf(string id)
        {
            for (int i = 0; i < Order.Count; i++)
            {
                if (string.Equals(Order[i].Id, id, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return 0;
        }
    }
}
