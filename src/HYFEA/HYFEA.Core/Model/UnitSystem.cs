namespace HYFEA.Core.Model;

/// <summary>Consistent unit convention carried with <see cref="FemProblem"/>.</summary>
public enum UnitSystem
{
    /// <summary>Length m, force N, stress Pa, moment N·m.</summary>
    SI = 0,

    /// <summary>Length mm, force N, stress MPa (N/mm²), moment N·mm — matches default AutoCAD model mm.</summary>
    MmN = 1,

    /// <summary>Use <see cref="UnitDescriptor"/> for scaling factors.</summary>
    Custom = 2,
}

/// <summary>Optional custom scaling to SI base (m, N) for diagnostics / interchange.</summary>
public sealed record UnitDescriptor(
    double LengthToMeter,
    double ForceToNewton,
    string LengthLabel = "m",
    string ForceLabel = "N",
    string StressLabel = "Pa",
    string MomentLabel = "N·m");
