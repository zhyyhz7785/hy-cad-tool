namespace HyCADTool.Features.G101.Domain.Tables
{
    /// <summary>混凝土强度等级（22G101 查表用）。</summary>
    public enum ConcreteGrade
    {
        C25, C30, C35, C40, C45, C50, C55, C60Plus
    }

    /// <summary>纵向受力钢筋种类（22G101-1 P2-2）。</summary>
    public enum RebarGrade
    {
        HPB300,
        HRB400,
        HRB500
    }

    /// <summary>抗震等级（22G101-1 P2-2 / P2-3）。</summary>
    public enum SeismicGrade
    {
        Grade1,
        Grade2,
        Grade3,
        Grade4
    }

    /// <summary>混凝土环境类别（22G101-1 P2-1）。</summary>
    public enum EnvironmentClass
    {
        ClassI,
        ClassIIa,
        ClassIIb,
        ClassIIIa,
        ClassIIIb
    }

    /// <summary>搭接接头面积百分率（22G101-1 P2-5 / P2-6）。</summary>
    public enum SplicePercent
    {
        P0 = 0,
        P25 = 25,
        P50 = 50,
        P100 = 100
    }
}
