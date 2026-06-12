namespace YOLOTerm.Core.Metadata;

/// <summary>
/// Metadata about a pane (CWD, git branch, shell, SSH host)
/// </summary>
public class PaneMetadata
{
    public string? Cwd { get; init; }
    public string? GitBranch { get; init; }
    public string? Shell { get; init; }
    public string? SshHost { get; init; }

    public static PaneMetadata Empty => new();
}
