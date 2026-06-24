using System;
using System.Collections.Generic;
using HyCADTool.Features.G101.Domain.Tables;

namespace HyCADTool.Features.G16.Domain.Tables
{
    /// <summary>
    /// 16G101-1 查表服务。数值来源：
    /// 第11~12页（保护层）、第57~58页（lab/labE）、第59~60页（la/laE）、第61页（搭接）、第16页（弯钩）。
    /// </summary>
    public static class G16TableLookup
    {
        public static readonly AtlasRef RefCover = new AtlasRef("16G101-1", "11", "保护层最小厚度");
        public static readonly AtlasRef RefLab = new AtlasRef("16G101-1", "57", "受拉钢筋基本锚固长度 lab/labE");
        public static readonly AtlasRef RefLa = new AtlasRef("16G101-1", "59", "受拉钢筋锚固长度 la/laE");
        public static readonly AtlasRef RefLl = new AtlasRef("16G101-1", "61", "纵向受拉钢筋搭接长度 ll");
        public static readonly AtlasRef RefHook = new AtlasRef("16G101-1", "16", "箍筋弯钩/拉结筋");

        private static readonly ConcreteGrade[] Grades =
        {
            ConcreteGrade.C25, ConcreteGrade.C30, ConcreteGrade.C35, ConcreteGrade.C40,
            ConcreteGrade.C45, ConcreteGrade.C50, ConcreteGrade.C55, ConcreteGrade.C60Plus
        };

        // 16G101-1 第58页 受拉钢筋基本锚固长度 lab（倍数 d）
        private static readonly Dictionary<RebarGrade, int[]> LabTable = new Dictionary<RebarGrade, int[]>
        {
            { RebarGrade.HPB300,  new[] { 34, 30, 28, 25, 24, 23, 23, 23 } },
            { RebarGrade.HRB400,  new[] { 39, 35, 32, 29, 28, 26, 25, 24 } },
            { RebarGrade.HRB500,  new[] { 46, 42, 38, 35, 33, 31, 30, 29 } },
        };

        private static readonly Dictionary<RebarGrade, int[]> LabEGrade12 = new Dictionary<RebarGrade, int[]>
        {
            { RebarGrade.HPB300,  new[] { 39, 35, 32, 29, 28, 26, 25, 24 } },
            { RebarGrade.HRB400,  new[] { 45, 40, 37, 33, 32, 30, 29, 28 } },
            { RebarGrade.HRB500,  new[] { 53, 48, 44, 40, 38, 36, 35, 34 } },
        };

        private static readonly Dictionary<RebarGrade, int[]> LabEGrade3 = new Dictionary<RebarGrade, int[]>
        {
            { RebarGrade.HPB300,  new[] { 36, 32, 29, 26, 25, 24, 23, 23 } },
            { RebarGrade.HRB400,  new[] { 41, 37, 34, 30, 29, 27, 26, 25 } },
            { RebarGrade.HRB500,  new[] { 49, 44, 40, 37, 35, 33, 32, 31 } },
        };

        public static int GetLabMultiplier(G16GlobalSettings s)
            => LabTable[s.RebarGrade][GradeIndex(s.ConcreteGrade)];

        public static int GetLabEMultiplier(G16GlobalSettings s)
        {
            if (s.SeismicGrade == SeismicGrade.Grade4)
                return GetLabMultiplier(s);
            var table = s.SeismicGrade == SeismicGrade.Grade3 ? LabEGrade3 : LabEGrade12;
            return table[s.RebarGrade][GradeIndex(s.ConcreteGrade)];
        }

        public static double GetLab(G16GlobalSettings s) => GetLabMultiplier(s) * s.RebarDiameter;
        public static double GetLabE(G16GlobalSettings s) => GetLabEMultiplier(s) * s.RebarDiameter;
        public static double GetLa(G16GlobalSettings s) => GetLab(s);
        public static double GetLaE(G16GlobalSettings s) => GetLabE(s);

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

        private static int GradeIndex(ConcreteGrade g)
        {
            for (int i = 0; i < Grades.Length; i++)
                if (Grades[i] == g) return i;
            return 1;
        }
    }
}
