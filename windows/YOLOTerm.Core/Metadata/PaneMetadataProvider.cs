using System.Diagnostics;
using System.Text.RegularExpressions;

namespace YOLOTerm.Core.Metadata;

/// <summary>
/// Provides pane metadata by polling process information and parsing OSC sequences
/// Poll interval: ~3 seconds
/// </summary>
public class PaneMetadataProvider : IPaneMetadataProvider, IDisposable
{
    private readonly int _pid;
    private readonly CancellationTokenSource _cts = new();
    private string? _oscCwd;
    private bool _disposed;

    public event EventHandler<PaneMetadata>? MetadataChanged;

    public PaneMetadataProvider(int pid)
    {
        _pid = pid;
        Task.Run(() => PollMetadataAsync());
    }

    public async Task<PaneMetadata> GetMetadataAsync()
    {
        try
        {
            var cwd = _oscCwd ?? await GetCwdFromProcessAsync();
            var gitBranch = !string.IsNullOrEmpty(cwd) ? await GetGitBranchAsync(cwd) : null;
            var shell = await GetShellNameAsync();
            var sshHost = await GetSshHostAsync();

            return new PaneMetadata
            {
                Cwd = cwd,
                GitBranch = gitBranch,
                Shell = shell,
                SshHost = sshHost
            };
        }
        catch
        {
            return PaneMetadata.Empty;
        }
    }

    public void UpdateCwdFromOsc(string cwd)
    {
        _oscCwd = cwd;
    }

    private async Task PollMetadataAsync()
    {
        while (!_cts.Token.IsCancellationRequested)
        {
            try
            {
                var metadata = await GetMetadataAsync();
                MetadataChanged?.Invoke(this, metadata);
            }
            catch
            {
                // Ignore errors during polling
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(3), _cts.Token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task<string?> GetCwdFromProcessAsync()
    {
        // On Windows, getting CWD from process is challenging
        // Best approach is OSC 7 from shell plugins
        // Fallback: try to get from process command line or environment
        try
        {
            var process = Process.GetProcessById(_pid);
            
            // Try to extract from command line (limited on Windows)
            // This is a fallback - OSC 7 is preferred
            return null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> GetGitBranchAsync(string cwd)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "git.exe",
                Arguments = "rev-parse --abbrev-ref HEAD",
                WorkingDirectory = cwd,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null) return null;

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                return output.Trim();
            }
        }
        catch
        {
            // git.exe not found or not a git repo
        }

        return null;
    }

    private async Task<string?> GetShellNameAsync()
    {
        try
        {
            var process = Process.GetProcessById(_pid);
            var processName = process.ProcessName;
            
            // Try to find the shell process (might be child of ConPTY)
            var children = GetChildProcesses(_pid);
            if (children.Any())
            {
                var shellProcess = children.FirstOrDefault(p => 
                    p.ProcessName.Contains("pwsh", StringComparison.OrdinalIgnoreCase) ||
                    p.ProcessName.Contains("powershell", StringComparison.OrdinalIgnoreCase) ||
                    p.ProcessName.Contains("cmd", StringComparison.OrdinalIgnoreCase) ||
                    p.ProcessName.Contains("bash", StringComparison.OrdinalIgnoreCase));

                if (shellProcess != null)
                {
                    processName = shellProcess.ProcessName;
                }
            }

            return processName;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> GetSshHostAsync()
    {
        try
        {
            // Walk process tree looking for ssh.exe or ssh
            var processes = new List<Process>();
            var current = _pid;
            
            for (int i = 0; i < 10; i++) // Max depth
            {
                try
                {
                    var proc = Process.GetProcessById(current);
                    processes.Add(proc);

                    if (proc.ProcessName.Contains("ssh", StringComparison.OrdinalIgnoreCase))
                    {
                        // Try to extract host from command line
                        // This is limited on Windows - need Win32 API for full command line
                        return "remote"; // Simplified for now
                    }

                    // Get parent (requires Win32 API, simplified here)
                    break;
                }
                catch
                {
                    break;
                }
            }
        }
        catch
        {
            // Not in SSH session
        }

        return null;
    }

    private List<Process> GetChildProcesses(int parentId)
    {
        var children = new List<Process>();
        
        try
        {
            var allProcesses = Process.GetProcesses();
            foreach (var process in allProcesses)
            {
                try
                {
                    // This is simplified - proper implementation needs Win32 API
                    // to get parent process ID from PROCESS_BASIC_INFORMATION
                }
                catch
                {
                    // Ignore
                }
            }
        }
        catch
        {
            // Ignore
        }

        return children;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _cts.Cancel();
        _cts.Dispose();
    }
}
