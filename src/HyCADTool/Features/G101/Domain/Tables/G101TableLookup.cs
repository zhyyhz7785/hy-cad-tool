using System;
using System.Collections.Generic;

namespace HyCADTool.Features.G101.Domain.Tables
{
    /// <summary>
    /// 22G101 查表服务。数值来源：
    /// 22G101-1 P2-1（保护层）、P2-2（lab/labE/弯弧 D）、P2-3（la/laE）、P2-5/2-6（ll/llE）。
    /// </summary>
    public static class G101TableLookup
    {
        public static readonly AtlasRef RefCover = new AtlasRef("22G101-1", "2-1", "保护层最小厚度");
        public static readonly AtlasRef RefLab = new AtlasRef("22G101-1", "2-2", "受拉钢筋基本锚固长度 lab/labE");
        public static readonly AtlasRef RefLa = new AtlasRef("22G101-1", "2-3", "受拉钢筋锚固长度 la/laE");
        public static readonly AtlasRef RefLl = new AtlasRef("22G101-1", "2-5", "纵向受拉钢筋搭接长度 ll");
        public static readonly AtlasRef RefLlE = new AtlasRef("22G101-1", "2-6", "纵向受拉钢筋抗震搭接长度 llE");
        public static readonly AtlasRef RefHook = new AtlasRef("22G101-1", "2-7", "箍筋弯钩/拉结筋");

        private static readonly ConcreteGrade[] Grades =
        {
            ConcreteGrade.C25, ConcreteGrade.C30, ConcreteGrade.C35, ConcreteGrade.C40,
            ConcreteGrade.C45, ConcreteGrade.C50, ConcreteGrade.C55, ConcreteGrade.C60Plus
        };

        // 22G101-1 P2-2 受拉钢筋基本锚固长度 lab（倍数 d）
        private static readonly Dictionary<RebarGrade, int[]> LabTable = new Dictionary<RebarGrade, int[]>
        {
            { RebarGrade.HPB300,  new[] { 34, 30, 28, 25, 24, 23, 23, 23 } },
            { RebarGrade.HRB400,  new[] { 40, 35, 32, 29, 27, 25, 25, 24 } },
            { RebarGrade.HRB500,  new[] { 48, 43, 39, 36, 34, 32, 31, 30 } },
        };

        // 22G101-1 P2-2 抗震 labE — 一、二级
        private static readonly Dictionary<RebarGrade, int[]> LabEGrade12 = new Dictionary<RebarGrade, int[]>
        {
            { RebarGrade.HPB300,  new[] { 39, 35, 32, 29, 28, 26, 25, 24 } },
            { RebarGrade.HRB400,  new[] { 46, 40, 37, 33, 32, 30, 30, 29 } },
            { RebarGrade.HRB500,  new[] { 55, 49, 45, 41, 39, 37, 36, 35 } },
        };

        // 22G101-1 P2-2 抗震 labE — 三级
        private static readonly Dictionary<RebarGrade, int[]> LabEGrade3 = new Dictionary<RebarGrade, int[]>
        {
            { RebarGrade.HPB300,  new[] { 36, 32, 29, 26, 25, 24, 23, 23 } },
            { RebarGrade.HRB400,  new[] { 42, 37, 34, 30, 29, 27, 27, 26 } },
            { RebarGrade.HRB500,  new[] { 50, 45, 41, 38, 36, 34, 33, 32 } },
        };

        // 22G101-1 P2-5 搭接长度修正系数 ζ_l
        private static readonly Dictionary<SplicePercent, double> ZetaL = new Dictionary<SplicePercent, double>
        {
            { SplicePercent.P0, 1.2 },
            { SplicePercent.P25, 1.4 },
            { SplicePercent.P50, 1.6 },
            { SplicePercent.P100, 2.0 },
        };

        // 22G101-1 P2-6 抗震搭接修正系数 ζ_lE（100% 时 1.6）
        private static readonly Dictionary<SplicePercent, double> ZetaLE = new Dictionary<SplicePercent, double>
        {
            { SplicePercent.P0, 1.2 },
            { SplicePercent.P25, 1.4 },
            { SplicePercent.P50, 1.6 },
            { SplicePercent.P100, 1.6 },
        };

        public static int GetLabMultiplier(G101GlobalSettings s)
            => LabTable[s.RebarGrade][GradeIndex(s.ConcreteGrade)];

        public static int GetLabEMultiplier(G101GlobalSettings s)
        {
            if (s.SeismicGrade == SeismicGrade.Grade4)
                return GetLabMultiplier(s);

            var table = s.SeismicGrade == SeismicGrade.Grade3 ? LabEGrade3 : LabEGrade12;
            return table[s.RebarGrade][GradeIndex(s.ConcreteGrade)];
        }

        /// <summary>lab 长度（mm）。</summary>
        public static double GetLab(G101GlobalSettings s)
            => GetLabMultiplier(s) * s.RebarDiameter;

        /// <summary>labE 长度（mm）。</summary>
        public static double GetLabE(G101GlobalSettings s)
            => GetLabEMultiplier(s) * s.RebarDiameter;

        /// <summary>la = lab（基本情形，无额外修正系数）。</summary>
        public static double GetLa(G101GlobalSettings s) => GetLab(s);

        /// <summary>laE（22G101-1 P2-3，四级抗震 laE=la）。</summary>
        public static double GetLaE(G101GlobalSettings s) => GetLabE(s);

        public static double GetLl(G101GlobalSettings s, SplicePercent pct = SplicePercent.P50)
            => Math.Max(300, ZetaL[pct] * GetLa(s));

        public static double GetLlE(G101GlobalSettings s, SplicePercent pct = SplicePercent.P50)
            => Math.Max(300, ZetaLE[pct] * GetLaE(s));

        /// <summary>22G101-1 P2-1 保护层（mm）：[板/墙/基础] 列索引 0/1/2。</summary>
        public static int GetCoverThickness(EnvironmentClass env, int memberColumn = 2)
        {
            var table = new Dictionary<EnvironmentClass, int[]>
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

        /// <summary>22G101-1 P2-2 钢筋弯折弯弧内直径 D（mm）。</summary>
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

        /// <summary>22G101-1 P2-7 135° 箍筋弯钩平直段（mm）。</summary>
        public static double GetStirrupHookLength(int diameter)
            => diameter <= 6 ? Math.Max(75, 10 * diameter) : Math.Max(75, 12 * diameter);

        public static string FormatLookup(string name, double valueMm, AtlasRef reference, G101GlobalSettings s)
            => $"{name} = {valueMm:F0}mm（d={s.RebarDiameter}，{reference.Display}）";

        private static int GradeIndex(ConcreteGrade g)
        {
            for (int i = 0; i < Grades.Length; i++)
                if (Grades[i] == g) return i;
            return 1;
        }
    }
}
