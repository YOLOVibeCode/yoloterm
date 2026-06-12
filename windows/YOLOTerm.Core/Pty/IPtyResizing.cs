namespace YOLOTerm.Core.Pty;

/// <summary>
/// Propagate terminal size changes to PTY (contracts v1)
/// Threading: Main thread only
/// </summary>
public interface IPtyResizing
{
    /// <summary>
    /// Resize PTY (sends ResizePseudoConsole on Windows)
    /// Must be called on every window/pane resize before feeding more data
    /// </summary>
    /// <exception cref="InvalidOperationException">Session closed or resize failed</exception>
    void Resize(int cols, int rows);
}
