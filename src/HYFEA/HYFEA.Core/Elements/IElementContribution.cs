using HYFEA.Core.Dofs;
using HYFEA.Core.LinearAlgebra;
using HYFEA.Core.Loads;
using HYFEA.Core.Model;

namespace HYFEA.Core.Elements;

/// <summary>
/// Polymorphic element contribution interface (P1.v0.1b): stiffness, equivalent loads, DOF layout, recovery.
/// Enables Track A/B dual-implementation (see 00 §三, 02 §5).
/// </summary>
public interface IElementContribution
{
    /// <summary>Element definition type this contribution handles (e.g. typeof(EulerBeam2DElementDef)).</summary>
    Type ElementDefinitionType { get; }

    /// <summary>Returns DOF types required at <paramref name="node"/> for the given element.</summary>
    IReadOnlyList<DofType> ActiveDofsForNode(FemElementDefinition element, NodeId node);

    /// <summary>Assembles element stiffness into global <paramref name="K"/>.</summary>
    void ApplyStiffness(FemProblem problem, FemElementDefinition element, DofLayout layout, DenseMatrix K);

    /// <summary>Converts element loads (e.g. uniform beam load) to equivalent nodal forces and adds to <paramref name="F"/>.</summary>
    void ApplyEquivalentLoads(
        FemProblem problem,
        FemElementDefinition element,
        IReadOnlyList<IElementLoad> elementLoads,
        DofLayout layout,
        DenseVector F);

    /// <summary>
    /// Recovers element internal forces from global displacement <paramref name="uFull"/>.
    /// Return type: double (axial) or BeamEndValues (beam); future: IElementInternalForces base.
    /// </summary>
    object Recover(FemProblem problem, FemElementDefinition element, DofLayout layout, DenseVector uFull);
}

