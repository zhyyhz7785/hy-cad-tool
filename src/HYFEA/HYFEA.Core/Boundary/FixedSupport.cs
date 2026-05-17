using HYFEA.Core.Dofs;
using HYFEA.Core.Model;

namespace HYFEA.Core.Boundary;

/// <summary>Homogeneous Dirichlet: dof fixed at zero.</summary>
public sealed record FixedSupport(NodeId NodeId, DofType Dof);
