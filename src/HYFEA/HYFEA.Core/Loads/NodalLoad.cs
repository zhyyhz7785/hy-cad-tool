using HYFEA.Core.Dofs;
using HYFEA.Core.Model;

namespace HYFEA.Core.Loads;

public sealed record NodalLoad(NodeId NodeId, DofType Dof, double Value);

/// <summary>Load case: collection of nodal loads (P0 single-case analysis).</summary>
public sealed record LoadCase(IReadOnlyList<NodalLoad> Loads);
