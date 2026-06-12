namespace YOLOTerm.Core.Contracts;

/// <summary>
/// Terminal cell dimensions in pixels
/// </summary>
public struct CellMetrics
{
    public double CellWidth { get; init; }
    public double CellHeight { get; init; }

    public CellMetrics(double width, double height)
    {
        CellWidth = width;
        CellHeight = height;
    }
}
