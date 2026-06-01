namespace HYFEA.Core.Elements;

/// <summary>
/// Future: polymorphic element stiffness / loads / recovery (see 02 MVP §5). P1 keeps switch-dispatch;
/// Spring1D / Truss2D / EulerBeam2D will implement this in a follow-up PR.
/// </summary>
public interface IElementContribution
{
}
