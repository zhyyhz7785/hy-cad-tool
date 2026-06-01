using HYFEA.Core.Dofs;
using HYFEA.Core.Model;

namespace HYFEA.Core.Loads;

public sealed record NodalLoad(NodeId NodeId, DofType Dof, double Value);

/// <summary>Single load case: nodal forces + optional per-element loads.</summary>
public sealed record LoadCase
{
    public LoadCase(IReadOnlyList<NodalLoad> loads)
        : this(loads, Array.Empty<IElementLoad>())
    {
    }

    public LoadCase(IReadOnlyList<NodalLoad> loads, IReadOnlyList<IElementLoad> elementLoads)
    {
        Loads = loads;
        ElementLoads = elementLoads;
    }

    public IReadOnlyList<NodalLoad> Loads { get; init; }

    public IReadOnlyList<IElementLoad> ElementLoads { get; init; }
}
