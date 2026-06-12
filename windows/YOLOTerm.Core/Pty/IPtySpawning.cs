namespace YOLOTerm.Core.Pty;

/// <summary>
/// Spawn shell process with environment policy (contracts v1)
/// Threading: Main thread only
/// </summary>
public interface IPtySpawning
{
    /// <summary>
    /// Spawn a shell process
    /// </summary>
    /// <param name="shell">Full path to shell executable</param>
    /// <param name="cwd">Starting working directory</param>
    /// <param name="cols">Initial terminal columns</param>
    /// <param name="rows">Initial terminal rows</param>
    /// <param name="env">Environment variables after policy applied</param>
    /// <returns>PTY session</returns>
    /// <exception cref="FileNotFoundException">Shell not found</exception>
    /// <exception cref="DirectoryNotFoundException">CWD doesn't exist</exception>
    /// <exception cref="InvalidOperationException">Spawn failed</exception>
    IPtySession Spawn(string shell, string cwd, int cols, int rows, Dictionary<string, string> env);
}
