namespace YOLOTerm.Core.Pty;

/// <summary>
/// Write user input bytes to PTY session (contracts v1)
/// Threading: Main thread only
/// </summary>
public interface IPtyWriting
{
    /// <summary>
    /// Write raw bytes from user input (keyboard, paste, etc.)
    /// Non-blocking; implementation should buffer if needed
    /// </summary>
    /// <exception cref="InvalidOperationException">Session closed or write failed</exception>
    void Write(byte[] data);
}
