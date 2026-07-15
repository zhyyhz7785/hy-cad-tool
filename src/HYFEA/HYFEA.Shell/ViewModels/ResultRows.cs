namespace HYFEA.Shell.ViewModels;

public sealed class NodalResultRow
{
    public int Node { get; init; }
    public double UX { get; init; }
    public double UY { get; init; }
    public double RZ { get; init; }
}

public sealed class BeamForceRow
{
    public int Element { get; init; }
    public double N { get; init; }
    public double VA { get; init; }
    public double MA { get; init; }
    public double VB { get; init; }
    public double MB { get; init; }
}
