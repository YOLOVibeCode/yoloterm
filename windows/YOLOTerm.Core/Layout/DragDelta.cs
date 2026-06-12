namespace YOLOTerm.Core.Layout;

/// <summary>
/// Drag delta for border adjustments
/// </summary>
public readonly struct DragDelta
{
    public enum OrientationType
    {
        Horizontal,
        Vertical
    }

    public OrientationType Orientation { get; init; }
    public double Position { get; init; } // normalized 0-1
    public double Delta { get; init; } // in pixels

    public DragDelta(OrientationType orientation, double position, double delta)
    {
        Orientation = orientation;
        Position = position;
        Delta = delta;
    }
}
