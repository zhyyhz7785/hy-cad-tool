namespace HYFEA.Core.Diagnostics;

public enum FemErrorKind
{
    SingularMatrix,
    InsufficientConstraints,
    NegativePivot,
    NumericalOverflow,
}

public sealed record FemError(FemErrorKind Kind, string Message, int? AtRow = null);
