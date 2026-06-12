namespace YOLOTerm.Core.Contracts;

/// <summary>
/// Terminal cell for golden tests (contracts v1)
/// </summary>
public struct Cell
{
    public string Char { get; init; }
    public Color? Fg { get; init; }
    public Color? Bg { get; init; }
    public CellAttributes Attrs { get; init; }

    public Cell(string ch, Color? fg = null, Color? bg = null, CellAttributes attrs = CellAttributes.None)
    {
        Char = ch;
        Fg = fg;
        Bg = bg;
        Attrs = attrs;
    }
}
