using HYFEA.Core.Boundary;
using HYFEA.Core.Loads;
using HYFEA.Core.Materials;
using HYFEA.Core.Sections;

namespace HYFEA.Core.Model;

public sealed record FemProblem(
    IReadOnlyList<FemNode> Nodes,
    IReadOnlyList<FemElementDefinition> Elements,
    IReadOnlyDictionary<MaterialId, MaterialDefinition> Materials,
    IReadOnlyDictionary<SectionId, SectionDefinition> Sections,
    IReadOnlyList<FixedSupport> Supports,
    LoadCase LoadCase
);
