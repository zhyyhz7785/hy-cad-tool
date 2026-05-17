using HYFEA.Core.Model;

namespace HYFEA.Core.Sections;

public abstract record SectionDefinition(SectionId Id);

/// <summary>Axial bar / truss cross-sectional area A (area units²).</summary>
public sealed record AxialSection(SectionId Id, double Area) : SectionDefinition(Id);
