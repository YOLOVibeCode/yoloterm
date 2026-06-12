namespace YOLOTerm.Core.Layout;

/// <summary>
/// A rectangle representing a pane's position and size
/// </summary>
public readonly struct PaneRect : IEquatable<PaneRect>
{
    public string Id { get; init; }
    public double X { get; init; }
    public double Y { get; init; }
    public double Width { get; init; }
    public double Height { get; init; }

    public PaneRect(string id, double x, double y, double width, double height)
    {
        Id = id;
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public bool Equals(PaneRect other)
    {
        return Id == other.Id &&
               Math.Abs(X - other.X) < 0.001 &&
               Math.Abs(Y - other.Y) < 0.001 &&
               Math.Abs(Width - other.Width) < 0.001 &&
               Math.Abs(Height - other.Height) < 0.001;
    }

    public override bool Equals(object? obj) => obj is PaneRect other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Id, X, Y, Width, Height);

    public static bool operator ==(PaneRect left, PaneRect right) => left.Equals(right);
    public static bool operator !=(PaneRect left, PaneRect right) => !left.Equals(right);
}
