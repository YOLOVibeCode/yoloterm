namespace YOLOTerm.Core.Pty;

/// <summary>
/// PTY spawner implementation using ConPTY (contracts v1)
/// </summary>
public class ConPtySpawner : IPtySpawning
{
    public IPtySession Spawn(string shell, string cwd, int cols, int rows, Dictionary<string, string> env)
    {
        return ConPtySession.Spawn(shell, cwd, cols, rows, env);
    }
}
