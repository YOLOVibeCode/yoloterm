namespace YOLOTerm.Core.Pty;

/// <summary>
/// Combined interface for a spawned PTY (contracts v1)
/// </summary>
public interface IPtySession : IPtyWriting, IPtyResizing, IPtyLifecycle
{
    /// <summary>
    /// Process ID of the spawned shell
    /// </summary>
    int Pid { get; }
}
