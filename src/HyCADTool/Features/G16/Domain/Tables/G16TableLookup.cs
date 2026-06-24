using System;
using HyCADTool.Features.G101.Domain.Tables;

namespace HyCADTool.Features.G16.Domain.Tables
{
    /// <summary>
    /// 16G101-1 查表服务。数值来源：
    /// 第11~12页（保护层）、第53页（lab/labE/la/ζ_a）、第61页（搭接）、第16页（弯钩）。
    /// </summary>
    public static class G16TableLookup
    {
        public static readonly AtlasRef RefCover = new AtlasRef("16G101-1", "11", "保护层最小厚度");
        public static readonly AtlasRef RefLab = new AtlasRef("16G101-1", "53", "受拉钢筋基本锚固长度 lab/labE");
        public static readonly AtlasRef RefLa = new AtlasRef("16G101-1", "53", "受拉钢筋锚固长度 la/laE");
        public static readonly AtlasRef RefLl = new AtlasRef("16G101-1", "61", "纵向受拉钢筋搭接长度 ll");
        public static readonly AtlasRef RefHook = new AtlasRef("16G101-1", "16", "箍筋弯钩/拉结筋");

        public static int GetLabMultiplier(G16GlobalSettings s)
            => ReadMultiplier(s, Page53LabRowKind.Grade4OrNonSeismic, "lab");

        public static int GetLabEMultiplier(G16GlobalSettings s)
        {
            if (s.SeismicGrade == SeismicGrade.Grade4)
                return GetLabMultiplier(s);
            return ReadMultiplier(s, G16Page53Tables.MapSeismicRow(s.SeismicGrade), "labE");
        }

        public static double GetLab(G16GlobalSettings s) => GetLabMultiplier(s) * s.RebarDiameter;

        public static double GetLabE(G16GlobalSettings s) => GetLabEMultiplier(s) * s.RebarDiameter;

        public static double GetLa(G16GlobalSettings s, double zetaA = 1.0)
            => Math.Max(200, GetLab(s) * zetaA);

        public static double GetZetaAe(SeismicGrade grade)
        {
            switch (grade)
            {
                case SeismicGrade.Grade1:
                case SeismicGrade.Grade2: return 1.15;
                case SeismicGrade.Grade3: return 1.05;
                default: return 1.00;
            }
        }

        /// <summary>抗震锚固长度 laE：四级同 la；一~三级取表 labE×d（与第53页表1交叉值一致）。</summary>
        public static double GetLaE(G16GlobalSettings s, double zetaA = 1.0)
        {
            if (s.SeismicGrade == SeismicGrade.Grade4)
                return GetLa(s, zetaA);
            return GetLabE(s);
        }

        public static int GetCoverThickness(EnvironmentClass env, int memberColumn = 2)
        {
            var table = new System.Collections.Generic.Dictionary<EnvironmentClass, int[]>
            {
                { EnvironmentClass.ClassI,    new[] { 15, 20, 25 } },
                { EnvironmentClass.ClassIIa,  new[] { 20, 25, 30 } },
                { EnvironmentClass.ClassIIb,  new[] { 25, 35, 40 } },
                { EnvironmentClass.ClassIIIa, new[] { 30, 40, 40 } },
                { EnvironmentClass.ClassIIIb, new[] { 40, 50, 50 } },
            };
            var row = table[env];
            var idx = Math.Max(0, Math.Min(2, memberColumn));
            return row[idx];
        }

        public static double GetBendDiameter(RebarGrade grade, int diameter)
        {
            switch (grade)
            {
                case RebarGrade.HPB300: return 2.5 * diameter;
                case RebarGrade.HRB400: return 4.0 * diameter;
                case RebarGrade.HRB500: return diameter < 25 ? 6.0 * diameter : 7.0 * diameter;
                default: return 4.0 * diameter;
            }
        }

        public static double GetStirrupHookLength(int diameter)
            => diameter <= 6 ? Math.Max(75, 10 * diameter) : Math.Max(75, 12 * diameter);

        public static string FormatLookup(string name, double valueMm, AtlasRef reference, G16GlobalSettings s)
            => $"{name} = {valueMm:F0}mm（d={s.RebarDiameter}，{reference.Display}）";

        private static int ReadMultiplier(G16GlobalSettings s, Page53LabRowKind rowKind, string label)
        {
            var steel = G16Page53Tables.MapRebarGrade(s.RebarGrade);
            var row = G16Page53Tables.FindLabRow(steel, rowKind);
            if (row == null)
                throw new InvalidOperationException($"第53页未找到 {steel} / {rowKind} 行。");

            var colIdx = G16Page53Tables.ConcreteColumnIndex(s.ConcreteGrade);
            var cell = row.Values[colIdx];
            if (G16Page53Tables.TryParseMultiplier(cell, out var mult) != 1)
            {
                throw new InvalidOperationException(
                    $"第53页 {steel} {label} 在 {s.ConcreteGrade} 无可用值（单元格={cell}）。" +
                    "HRB400/HRB500 的 C20 列不适用，请选用 C25 及以上砼等级。");
            }
            return mult;
        }
    }
}
