namespace HYFEA.Core.Geometry;

/// <summary>2D point in model plane (mm for HyCAD convention). Z is out-of-plane.</summary>
public readonly record struct Point2D(double X, double Y)
{
    public double DistanceTo(Point2D other)
    {
        double dx = X - other.X;
        double dy = Y - other.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }
}
