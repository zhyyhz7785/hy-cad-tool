using HYFEA.Core.Model;

namespace HYFEA.Core.Loads;

/// <summary>Per-element distributed / other loads (P1: uniform beam only).</summary>
public interface IElementLoad
{
    ElementId TargetElement { get; }
}

/// <summary>Uniform load along the beam axis; transverse component resolved in <see cref="BeamLoadDirection"/>.</summary>
public enum BeamLoadDirection
{
    /// <summary>Intensity <see cref="UniformBeamLoad.Q"/> acts along local +y (perpendicular to chord A→B, CCW from chord).</summary>
    LocalPerpendicular = 0,

    /// <summary>Global distributed load vector (0, -Q) per unit length, projected onto local transverse.</summary>
    GlobalNegY = 1,
}

public sealed record UniformBeamLoad(ElementId TargetElement, BeamLoadDirection Direction, double Q) : IElementLoad;
