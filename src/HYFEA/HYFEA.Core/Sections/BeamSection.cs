using HYFEA.Core.Model;

namespace HYFEA.Core.Sections;

/// <summary>Plane beam: area A and second moment of area I about out-of-plane Z (bending in x-y).</summary>
public sealed record BeamSection(SectionId Id, double Area, double MomentOfInertiaZ) : SectionDefinition(Id);
