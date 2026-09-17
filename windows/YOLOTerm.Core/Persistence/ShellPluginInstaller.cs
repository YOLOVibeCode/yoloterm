using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace YOLOTerm.Core.Persistence;

/// <summary>
/// ShellPluginInstaller manages the installation of YOLOTerm shell plugins
/// into user shell profiles for PowerShell and Bash.
/// 
/// Features:
/// - Detect installed shells (PowerShell, pwsh, Git Bash)
/// - One-click installation with backup
/// - Uninstall support
/// - Profile modification tracking
/// </summary>
public sealed class ShellPluginInstaller
{
    public sealed class ShellInfo
    {
        public string Name { get; set; } = string.Empty;
        public string ShellPath { get; set; } = string.Empty;
        public string ProfilePath { get; set; } = string.Empty;
        public bool IsInstalled { get; set; }
        public bool ProfileExists { get; set; }
    }
    
    private readonly string pluginsDirectory;
    
    public ShellPluginInstaller(string? sharedPluginsPath = null)
    {
        if (sharedPluginsPath != null)
        {
            pluginsDirectory = sharedPluginsPath;
        }
        else
        {
            // Default: find shared/shell-plugins relative to the app
            string? projectRoot = FindProjectRoot(AppContext.BaseDirectory);
            if (projectRoot != null)
            {
                pluginsDirectory = Path.Combine(projectRoot, "shared", "shell-plugins");
            }
            else
            {
                // Fallback: use a relative path
                pluginsDirectory = Path.Combine("..", "..", "..", "..", "..", "shared", "shell-plugins");
            }
        }
    }
    
    // MARK: - Detection
    
    /// <summary>
    /// Detect all installed shells that YOLOTerm supports.
    /// </summary>
    public async Task<List<ShellInfo>> DetectShellsAsync()
    {
        List<ShellInfo> shells = new();
        
        // PowerShell (Windows PowerShell 5.x)
        string powershellPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell", "v1.0", "powershell.exe");
        
        if (File.Exists(powershellPath))
        {
            shells.Add(await GetPowerShellInfoAsync("PowerShell", powershellPath));
        }
        
        // PowerShell Core (pwsh)
        string? pwshPath = FindExecutableInPath("pwsh.exe");
        if (pwshPath != null)
        {
            shells.Add(await GetPowerShellInfoAsync("PowerShell Core", pwshPath));
        }
        
        // Git Bash
        string[] gitBashLocations = new[]
        {
            @"C:\Program Files\Git\bin\bash.exe",
            @"C:\Program Files (x86)\Git\bin\bash.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                "Programs", "Git", "bin", "bash.exe")
        };
        
        foreach (string location in gitBashLocations)
        {
            if (File.Exists(location))
            {
                shells.Add(await GetBashInfoAsync("Git Bash", location));
                break;
            }
        }
        
        // WSL (future enhancement)
        // Detect WSL distributions and add them
        
        return shells;
    }
    
    private async Task<ShellInfo> GetPowerShellInfoAsync(string name, string shellPath)
    {
        string profilePath = await GetPowerShellProfilePathAsync(shellPath);
        bool profileExists = File.Exists(profilePath);
        bool isInstalled = profileExists && await CheckInstalledAsync(profilePath, "yoloterm.ps1");
        
        return new ShellInfo
        {
            Name = name,
            ShellPath = shellPath,
            ProfilePath = profilePath,
            IsInstalled = isInstalled,
            ProfileExists = profileExists
        };
    }
    
    private async Task<ShellInfo> GetBashInfoAsync(string name, string shellPath)
    {
        string homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string profilePath = Path.Combine(homeDir, ".bashrc");
        bool profileExists = File.Exists(profilePath);
        bool isInstalled = profileExists && await CheckInstalledAsync(profilePath, "yoloterm.bash");
        
        return new ShellInfo
        {
            Name = name,
            ShellPath = shellPath,
            ProfilePath = profilePath,
            IsInstalled = isInstalled,
            ProfileExists = profileExists
        };
    }
    
    private async Task<string> GetPowerShellProfilePathAsync(string shellPath)
    {
        try
        {
            // Query PowerShell for its profile path
            ProcessStartInfo psi = new()
            {
                FileName = shellPath,
                Arguments = "-NoProfile -Command \"$PROFILE.CurrentUserCurrentHost\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            
            using Process process = Process.Start(psi)!;
            string output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();
            
            return output.Trim();
        }
        catch
        {
            // Fallback: construct default path
            string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            string psVersion = shellPath.Contains("pwsh") ? "PowerShell" : "WindowsPowerShell";
            return Path.Combine(documentsPath, psVersion, "Microsoft.PowerShell_profile.ps1");
        }
    }
    
    private async Task<bool> CheckInstalledAsync(string profilePath, string pluginFileName)
    {
        if (!File.Exists(profilePath))
        {
            return false;
        }
        
        string content = await File.ReadAllTextAsync(profilePath);
        return content.Contains(pluginFileName) || content.Contains("YOLOTerm Shell Plugin");
    }
    
    // MARK: - Installation
    
    /// <summary>
    /// Install the shell plugin for a given shell.
    /// Creates a backup of the existing profile before modification.
    /// </summary>
    public async Task InstallAsync(ShellInfo shell)
    {
        string pluginFileName = shell.Name.Contains("Bash") ? "yoloterm.bash" : "yoloterm.ps1";
        string pluginSourcePath = Path.Combine(pluginsDirectory, pluginFileName);
        
        if (!File.Exists(pluginSourcePath))
        {
            throw new FileNotFoundException($"Plugin file not found: {pluginSourcePath}");
        }
        
        // Ensure profile directory exists
        string? profileDir = Path.GetDirectoryName(shell.ProfilePath);
        if (profileDir != null)
        {
            Directory.CreateDirectory(profileDir);
        }
        
        // Create backup if profile exists
        if (File.Exists(shell.ProfilePath))
        {
            string backupPath = shell.ProfilePath + ".yoloterm.backup";
            File.Copy(shell.ProfilePath, backupPath, overwrite: true);
        }
        
        // Read plugin content
        string pluginContent = await File.ReadAllTextAsync(pluginSourcePath);
        
        // Prepare installation snippet
        string snippet = shell.Name.Contains("Bash")
            ? GenerateBashSnippet(pluginSourcePath)
            : GeneratePowerShellSnippet(pluginSourcePath);
        
        // Append to profile
        StringBuilder profileContent = new();
        
        if (File.Exists(shell.ProfilePath))
        {
            profileContent.Append(await File.ReadAllTextAsync(shell.ProfilePath));
            if (!profileContent.ToString().EndsWith("\n"))
            {
                profileContent.AppendLine();
            }
            profileContent.AppendLine();
        }
        
        profileContent.AppendLine("# YOLOTerm Shell Integration");
        profileContent.AppendLine($"# Installed: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        profileContent.AppendLine(snippet);
        
        await File.WriteAllTextAsync(shell.ProfilePath, profileContent.ToString());
    }
    
    /// <summary>
    /// Uninstall the shell plugin for a given shell.
    /// Removes the YOLOTerm integration block from the profile.
    /// </summary>
    public async Task UninstallAsync(ShellInfo shell)
    {
        if (!File.Exists(shell.ProfilePath))
        {
            return;
        }
        
        string content = await File.ReadAllTextAsync(shell.ProfilePath);
        
        // Remove YOLOTerm integration block
        string[] lines = content.Split('\n');
        List<string> newLines = new();
        bool inYoloTermBlock = false;
        
        foreach (string line in lines)
        {
            if (line.Contains("# YOLOTerm Shell Integration"))
            {
                inYoloTermBlock = true;
                continue;
            }
            
            if (inYoloTermBlock)
            {
                // Skip lines until we hit a non-YOLOTerm line
                if (line.TrimStart().StartsWith("#") || 
                    line.Contains("yoloterm") ||
                    line.Contains("YOLOTerm") ||
                    string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }
                
                inYoloTermBlock = false;
            }
            
            newLines.Add(line);
        }
        
        await File.WriteAllTextAsync(shell.ProfilePath, string.Join('\n', newLines));
    }
    
    // MARK: - Helpers
    
    private string GeneratePowerShellSnippet(string pluginPath)
    {
        // Use dot-sourcing to load the plugin
        return $". \"{pluginPath}\"";
    }
    
    private string GenerateBashSnippet(string pluginPath)
    {
        // Convert Windows path to Unix-style path for Git Bash
        string unixPath = pluginPath.Replace('\\', '/');
        if (unixPath.Length >= 2 && char.IsLetter(unixPath[0]) && unixPath[1] == ':')
        {
            // Convert C:\path to /c/path
            char drive = char.ToLower(unixPath[0]);
            unixPath = $"/{drive}{unixPath.Substring(2)}";
        }
        
        return $"source \"{unixPath}\"";
    }
    
    private static string? FindExecutableInPath(string executableName)
    {
        string? pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (pathEnv == null)
        {
            return null;
        }
        
        string[] paths = pathEnv.Split(Path.PathSeparator);
        
        foreach (string path in paths)
        {
            string fullPath = Path.Combine(path, executableName);
            if (File.Exists(fullPath))
            {
                return fullPath;
            }
        }
        
        return null;
    }
    
    private static string? FindProjectRoot(string startPath)
    {
        string? currentDir = startPath;
        
        while (currentDir != null)
        {
            string sharedPath = Path.Combine(currentDir, "shared");
            if (Directory.Exists(sharedPath))
            {
                return currentDir;
            }
            
            currentDir = Path.GetDirectoryName(currentDir);
        }
        
        return null;
    }
}
