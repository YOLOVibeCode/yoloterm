namespace YOLOTerm.Core.Pty;

/// <summary>
/// PTY lifecycle management and exit observation (contracts v1)
/// </summary>
public interface IPtyLifecycle
{
    /// <summary>
    /// Forcefully terminate the session (TerminateProcess on Windows)
    /// Threading: Main thread only
    /// </summary>
    /// <exception cref="InvalidOperationException">Already dead or kill failed</exception>
    void Kill();

    /// <summary>
    /// Closure called exactly once when process exits (naturally or via kill)
    /// Parameter: Exit code (arbitrary on Windows)
    /// Threading: Callback may be on background thread
    /// </summary>
    event Action<int>? OnExit;

    /// <summary>
    /// Returns true if process running, false after exit
    /// Threading: Any thread
    /// </summary>
    bool IsAlive { get; }
}
