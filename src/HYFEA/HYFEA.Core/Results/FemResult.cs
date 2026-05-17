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

public sealed record FemResult(
    bool Success,
    FemError? Error,
    DenseVector? UFull,
    NodalValueField? Displacements,
    NodalValueField? Reactions,
    ElementAxialForceMap? AxialForces,
    DofLayout? Layout,
    SolveDiagnostics? Diagnostics);
