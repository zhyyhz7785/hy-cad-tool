using HYFEA.Core.Model;

namespace HYFEA.Core.Model;

/// <summary>Element instance in the discrete model.</summary>
public abstract record FemElementDefinition(ElementId Id);

public sealed record Spring1DElementDef(
    ElementId Id,
    NodeId NodeA,
    NodeId NodeB,
    MaterialId MaterialId,
    SectionId SectionId
) : FemElementDefinition(Id);

public sealed record Truss2DElementDef(
    ElementId Id,
    NodeId NodeA,
    NodeId NodeB,
    MaterialId MaterialId,
    SectionId SectionId
) : FemElementDefinition(Id);

public sealed record EulerBeam2DElementDef(
    ElementId Id,
    NodeId NodeA,
    NodeId NodeB,
    MaterialId MaterialId,
    SectionId SectionId
) : FemElementDefinition(Id);
