namespace YOLOTerm.Core.Layout;

/// <summary>
/// Container size
/// </summary>
public readonly struct ContainerSize : IEquatable<ContainerSize>
{
    public double Width { get; init; }
    public double Height { get; init; }

    public ContainerSize(double width, double height)
    {
        Width = width;
        Height = height;
    }

    public bool Equals(ContainerSize other)
    {
        return Math.Abs(Width - other.Width) < 0.001 &&
               Math.Abs(Height - other.Height) < 0.001;
    }

    public override bool Equals(object? obj) => obj is ContainerSize other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Width, Height);

    public static bool operator ==(ContainerSize left, ContainerSize right) => left.Equals(right);
    public static bool operator !=(ContainerSize left, ContainerSize right) => !left.Equals(right);
}
