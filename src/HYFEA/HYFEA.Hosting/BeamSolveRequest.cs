using HYFEA.Core.Loads;
using HYFEA.Core.Model;

namespace HYFEA.Hosting;

/// <summary>梁元 MVP 求解参数（独立壳与 CAD 面板应对齐同名字段）。</summary>
public sealed class BeamSolveRequest
{
    public double YoungsModulus { get; set; } = 2.06e5;

    public double Area { get; set; } = 8e4;

    public double InertiaZ { get; set; } = 1.067e9;

    public double Q { get; set; } = 10;

    public BeamLoadDirection LoadDirection { get; set; } = BeamLoadDirection.GlobalNegY;

    public BeamSupportKind EndA { get; set; } = BeamSupportKind.Fixed;

    public BeamSupportKind EndB { get; set; } = BeamSupportKind.Free;

    public UnitSystem Units { get; set; } = UnitSystem.MmN;
}
