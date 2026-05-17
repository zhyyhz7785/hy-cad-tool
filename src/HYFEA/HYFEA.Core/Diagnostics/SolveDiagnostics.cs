namespace HYFEA.Core.Diagnostics;

public sealed record SolveDiagnostics(int Dof, double RhsNorm, double ResidualNorm, TimeSpan Elapsed);
