using System;
using System.Collections.Generic;
using HyCADTool.Features.G101.Domain.Tables;

namespace HyCADTool.Features.G16.Domain.Tables
{
    public enum Page53SteelKind
    {
        HPB300,
        HPB335,
        HRB400,
        HRB500
    }

    public enum Page53LabRowKind
    {
        SeismicGrade12,
        SeismicGrade3,
        Grade4OrNonSeismic
    }

    public sealed class Page53LabRow
    {
        public Page53SteelKind Steel { get; }
        public Page53LabRowKind RowKind { get; }
        public string SteelLabel { get; }
        public string RowLabel { get; }
        /// <summary>与 <see cref="ConcreteColumns"/> 等长；不可用为 "—"。</summary>
        public string[] Values { get; }

        public Page53LabRow(Page53SteelKind steel, Page53LabRowKind rowKind,
            string steelLabel, string rowLabel, string[] values)
        {
            Steel = steel;
            RowKind = rowKind;
            SteelLabel = steelLabel;
            RowLabel = rowLabel;
            Values = values;
        }
    }

    public sealed class Page53ZetaRow
    {
        public string Condition { get; }
        public string Value { get; }

        public Page53ZetaRow(string condition, string value)
        {
            Condition = condition;
            Value = value;
        }
    }

    /// <summary>16G101-1 第53页三表静态数据。</summary>
    public static class G16Page53Tables
    {
        public static readonly string[] ConcreteColumns =
        {
            "C20", "C25", "C30", "C35", "C40", "C45", "C50", "C55", "≥C60"
        };

        public static readonly IReadOnlyList<Page53LabRow> LabRows = new List<Page53LabRow>
        {
            new Page53LabRow(Page53SteelKind.HPB300, Page53LabRowKind.SeismicGrade12,
                "HPB300", "一、二级 labE",
                new[] { "45d", "39d", "35d", "32d", "29d", "28d", "26d", "25d", "24d" }),
            new Page53LabRow(Page53SteelKind.HPB300, Page53LabRowKind.SeismicGrade3,
                "", "三级 labE",
                new[] { "41d", "36d", "32d", "29d", "26d", "25d", "24d", "23d", "22d" }),
            new Page53LabRow(Page53SteelKind.HPB300, Page53LabRowKind.Grade4OrNonSeismic,
                "", "四级 labE / 非抗震 lab",
                new[] { "39d", "34d", "30d", "28d", "25d", "24d", "23d", "22d", "21d" }),

            new Page53LabRow(Page53SteelKind.HPB335, Page53LabRowKind.SeismicGrade12,
                "HPB335、HPBF335", "一、二级 labE",
                new[] { "44d", "38d", "33d", "31d", "29d", "26d", "—", "24d", "24d" }),
            new Page53LabRow(Page53SteelKind.HPB335, Page53LabRowKind.SeismicGrade3,
                "", "三级 labE",
                new[] { "40d", "35d", "31d", "28d", "26d", "24d", "23d", "22d", "22d" }),
            new Page53LabRow(Page53SteelKind.HPB335, Page53LabRowKind.Grade4OrNonSeismic,
                "", "四级 labE / 非抗震 lab",
                new[] { "38d", "33d", "29d", "27d", "25d", "23d", "22d", "21d", "21d" }),

            new Page53LabRow(Page53SteelKind.HRB400, Page53LabRowKind.SeismicGrade12,
                "HRB400、HRBF400、RRB400", "一、二级 labE",
                new[] { "—", "46d", "40d", "37d", "33d", "32d", "31d", "30d", "29d" }),
            new Page53LabRow(Page53SteelKind.HRB400, Page53LabRowKind.SeismicGrade3,
                "", "三级 labE",
                new[] { "—", "42d", "37d", "34d", "30d", "29d", "28d", "27d", "26d" }),
            new Page53LabRow(Page53SteelKind.HRB400, Page53LabRowKind.Grade4OrNonSeismic,
                "", "四级 labE / 非抗震 lab",
                new[] { "—", "40d", "35d", "32d", "29d", "28d", "27d", "26d", "25d" }),

            new Page53LabRow(Page53SteelKind.HRB500, Page53LabRowKind.SeismicGrade12,
                "HRB500、HRBF500", "一、二级 labE",
                new[] { "—", "55d", "49d", "45d", "41d", "39d", "37d", "36d", "35d" }),
            new Page53LabRow(Page53SteelKind.HRB500, Page53LabRowKind.SeismicGrade3,
                "", "三级 labE",
                new[] { "—", "50d", "45d", "41d", "38d", "36d", "34d", "33d", "32d" }),
            new Page53LabRow(Page53SteelKind.HRB500, Page53LabRowKind.Grade4OrNonSeismic,
                "", "四级 labE / 非抗震 lab",
                new[] { "—", "48d", "43d", "39d", "36d", "34d", "32d", "31d", "30d" }),
        };

        public static readonly string[] LaFormulaLines =
        {
            "受拉钢筋锚固长度 la、抗震锚固长度 laE",
            "非抗震：la = ζa · lab",
            "抗震：  laE = ζaE · la",
            "注：",
            "1. la 不应小于 200。",
            "2. 锚固长度修正系数 ζa 按右侧表取值；多项相乘，且不小于 0.6。",
            "3. ζaE：一级 1.15，二级 1.15，三级 1.05，四级 1.00。",
        };

        public static readonly string[] FootnoteLines =
        {
            "注：",
            "1. HPB300 筋端部应做 180° 弯钩，弯钩平直段长度不应小于 3d；作受压筋时可不做弯钩。",
            "2. 当锚固筋混凝土保护层厚度不大于 5d 时，锚固长度范围内应设横向构造钢筋；",
            "   直径不小于 d/4（d 为锚固筋最大直径）；梁、柱间距不大于 5d 且不大于 100mm，",
            "   板、墙间距不大于 10d 且不大于 100mm（d 为锚固筋最小直径）。",
        };

        public static readonly IReadOnlyList<Page53ZetaRow> ZetaRows = new List<Page53ZetaRow>
        {
            new Page53ZetaRow("带肋钢筋公称直径大于 25", "1.10"),
            new Page53ZetaRow("环氧树脂涂层带肋钢筋", "1.25"),
            new Page53ZetaRow("施工过程中易受扰动的钢筋", "1.10"),
            new Page53ZetaRow("锚固区保护层厚度 3d", "0.80"),
            new Page53ZetaRow("锚固区保护层厚度 5d", "0.70"),
        };

        public static Page53SteelKind MapRebarGrade(RebarGrade grade)
        {
            switch (grade)
            {
                case RebarGrade.HPB300: return Page53SteelKind.HPB300;
                case RebarGrade.HRB500: return Page53SteelKind.HRB500;
                default: return Page53SteelKind.HRB400;
            }
        }

        public static Page53LabRowKind MapSeismicRow(SeismicGrade grade)
        {
            switch (grade)
            {
                case SeismicGrade.Grade3: return Page53LabRowKind.SeismicGrade3;
                case SeismicGrade.Grade4: return Page53LabRowKind.Grade4OrNonSeismic;
                default: return Page53LabRowKind.SeismicGrade12;
            }
        }

        public static int ConcreteColumnIndex(G16ConcreteGrade grade)
        {
            switch (grade)
            {
                case G16ConcreteGrade.C20: return 0;
                case G16ConcreteGrade.C25: return 1;
                case G16ConcreteGrade.C30: return 2;
                case G16ConcreteGrade.C35: return 3;
                case G16ConcreteGrade.C40: return 4;
                case G16ConcreteGrade.C45: return 5;
                case G16ConcreteGrade.C50: return 6;
                case G16ConcreteGrade.C55: return 7;
                default: return 8;
            }
        }

        public static Page53LabRow FindLabRow(Page53SteelKind steel, Page53LabRowKind rowKind)
        {
            foreach (var row in LabRows)
            {
                if (row.Steel == steel && row.RowKind == rowKind)
                    return row;
            }
            return null;
        }

        public static int TryParseMultiplier(string cell, out int multiplier)
        {
            multiplier = 0;
            if (string.IsNullOrEmpty(cell) || cell == "—") return -1;
            var s = cell.Trim();
            if (s.EndsWith("d", StringComparison.OrdinalIgnoreCase))
                s = s.Substring(0, s.Length - 1);
            return int.TryParse(s, out multiplier) ? 1 : -1;
        }
    }
}
