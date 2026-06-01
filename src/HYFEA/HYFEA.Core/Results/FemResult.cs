using HYFEA.Core.Diagnostics;
using HYFEA.Core.Dofs;
using HYFEA.Core.LinearAlgebra;
using HYFEA.Core.Model;

namespace HYFEA.Core.Results;

public sealed class NodalValueField
{
    private readonly Dictionary<(NodeId Node, DofType Dof), double> _values = [];

    public void Set(NodeId node, DofType dof, double value) => _values[(node, dof)] = value;

    public double Get(NodeId node, DofType dof) => _values[(node, dof)];

    public bool TryGet(NodeId node, DofType dof, out double value) =>
        _values.TryGetValue((node, dof), out value);

    public IReadOnlyDictionary<(NodeId Node, DofType Dof), double> AsReadOnly() => _values;
}

public sealed class ElementAxialForceMap
{
    private readonly Dictionary<ElementId, double> _values = [];

    public void Set(ElementId id, double axial) => _values[id] = axial;

    public double Get(ElementId id) => _values[id];

    public IReadOnlyDictionary<ElementId, double> AsReadOnly() => _values;
}

public readonly record struct BeamEndValues(double N, double VA, double MA, double VB, double MB);

/// <summary>Internal beam resultants at element ends (local / structural sign; see <see cref="Elements.EulerBeam2DContribution"/>).</summary>
public sealed class ElementBeamEndForceMap
{
    private readonly Dictionary<ElementId, BeamEndValues> _values = [];

    public void Set(ElementId id, BeamEndValues v) => _values[id] = v;

    public BeamEndValues Get(ElementId id) => _values[id];

    public IReadOnlyDictionary<ElementId, BeamEndValues> AsReadOnly() => _values;
}

public sealed record FemResult(
    bool Success,
    FemError? Error,
    DenseVector? UFull,
    NodalValueField? Displacements,
    NodalValueField? Reactions,
    ElementAxialForceMap? AxialForces,
    ElementBeamEndForceMap? BeamEndForces,
    DofLayout? Layout,
    SolveDiagnostics? Diagnostics);
