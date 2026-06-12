using YOLOTerm.Core.Contracts;

namespace YOLOTerm.Core.Terminal;

/// <summary>
/// Wraps terminal emulator for rendering, input, and golden tests (contracts v1)
/// Threading: Main thread only
/// </summary>
public interface ITerminalSurface
{
    /// <summary>
    /// Parse VT sequences and update buffer (non-blocking)
    /// </summary>
    void Feed(byte[] data);

    /// <summary>
    /// Get cell dimensions in pixels
    /// </summary>
    CellMetrics GetCellMetrics();

    /// <summary>
    /// Get cell content for golden tests (0-indexed)
    /// Returns null if out of bounds
    /// </summary>
    Cell? GetCell(int col, int row);

    /// <summary>
    /// Current terminal dimensions
    /// </summary>
    int Cols { get; }
    int Rows { get; }

    /// <summary>
    /// Snapshot visible buffer state for session restore
    /// Format: implementation-specific (round-trip restore only)
    /// </summary>
    byte[] Serialize();
}
