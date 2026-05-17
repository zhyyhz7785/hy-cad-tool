using HYFEA.Core.Model;

namespace HYFEA.Core.Materials;

/// <summary>Abstract material row; P0 only implements linear elastic isotropic.</summary>
public abstract record MaterialDefinition(MaterialId Id);

/// <summary>Young's modulus E (force/area) and Poisson's ratio; P0 truss/spring use E only.</summary>
public sealed record LinearElasticMaterial(MaterialId Id, double YoungsModulus, double PoissonRatio)
    : MaterialDefinition(Id);
