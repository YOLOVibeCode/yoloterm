namespace YOLOTerm.Core.Contracts;

/// <summary>
/// Terminal cell attributes (contracts v1)
/// </summary>
[Flags]
public enum CellAttributes
{
    None = 0,
    Bold = 1 << 0,
    Italic = 1 << 1,
    Underline = 1 << 2,
    Strikethrough = 1 << 3,
    Dim = 1 << 4,
    Inverse = 1 << 5
}
