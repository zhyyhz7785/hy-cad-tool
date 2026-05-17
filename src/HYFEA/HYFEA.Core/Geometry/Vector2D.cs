namespace HYFEA.Core.Geometry;

public readonly record struct Vector2D(double X, double Y)
{
    public double Length => Math.Sqrt(X * X + Y * Y);

    public Vector2D Normalized()
    {
        double len = Length;
        if (len < 1e-30)
            throw new InvalidOperationException("Zero-length vector cannot be normalized.");
        return new Vector2D(X / len, Y / len);
    }

    public static Vector2D From(Point2D a, Point2D b) => new(b.X - a.X, b.Y - a.Y);
}
