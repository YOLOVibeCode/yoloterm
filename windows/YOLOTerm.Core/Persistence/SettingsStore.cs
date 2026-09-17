using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace YOLOTerm.Core.Persistence;

/// <summary>
/// SettingsStore manages application settings persistence.
/// Settings are stored as JSON in %LOCALAPPDATA%\YOLOTerm\settings.json
/// </summary>
public sealed class SettingsStore
{
    public sealed class Settings
    {
        // Appearance
        [JsonPropertyName("theme")]
        public string Theme { get; set; } = "vivid";
        
        [JsonPropertyName("fontFamily")]
        public string FontFamily { get; set; } = "Cascadia Code";
        
        [JsonPropertyName("fontSize")]
        public int FontSize { get; set; } = 12;
        
        [JsonPropertyName("opacity")]
        public double Opacity { get; set; } = 1.0;
        
        // Behavior
        [JsonPropertyName("defaultShell")]
        public string DefaultShell { get; set; } = "pwsh";
        
        [JsonPropertyName("startupBehavior")]
        public string StartupBehavior { get; set; } = "restore"; // restore, empty, custom
        
        [JsonPropertyName("customStartupCommand")]
        public string? CustomStartupCommand { get; set; }
        
        [JsonPropertyName("closeWindowBehavior")]
        public string CloseWindowBehavior { get; set; } = "confirm"; // confirm, close, minimize
        
        [JsonPropertyName("scrollbackLines")]
        public int ScrollbackLines { get; set; } = 10000;
        
        // History
        [JsonPropertyName("historyEnabled")]
        public bool HistoryEnabled { get; set; } = true;
        
        [JsonPropertyName("historyRetentionDays")]
        public int HistoryRetentionDays { get; set; } = 90;
        
        [JsonPropertyName("historyRedactionEnabled")]
        public bool HistoryRedactionEnabled { get; set; } = true;
        
        [JsonPropertyName("historySyncAcrossPanes")]
        public bool HistorySyncAcrossPanes { get; set; } = true;
        
        // Shell Integration
        [JsonPropertyName("shellIntegrationEnabled")]
        public bool ShellIntegrationEnabled { get; set; } = true;
        
        [JsonPropertyName("installedPlugins")]
        public string[] InstalledPlugins { get; set; } = Array.Empty<string>();
        
        // Advanced
        [JsonPropertyName("debugLogging")]
        public bool DebugLogging { get; set; } = false;
        
        [JsonPropertyName("enableBell")]
        public bool EnableBell { get; set; } = true;
        
        [JsonPropertyName("copyOnSelect")]
        public bool CopyOnSelect { get; set; } = false;
        
        [JsonPropertyName("pasteOnRightClick")]
        public bool PasteOnRightClick { get; set; } = true;
        
        [JsonPropertyName("trimTrailingWhitespace")]
        public bool TrimTrailingWhitespace { get; set; } = true;
        
        [JsonPropertyName("cursorStyle")]
        public string CursorStyle { get; set; } = "block"; // block, underline, bar
        
        [JsonPropertyName("cursorBlink")]
        public bool CursorBlink { get; set; } = true;
        
        // Recent Directories (for Jump List)
        [JsonPropertyName("recentDirectories")]
        public string[] RecentDirectories { get; set; } = Array.Empty<string>();
    }
    
    private readonly string settingsPath;
    private static SettingsStore? _instance;
    
    public static SettingsStore Instance => _instance ??= new SettingsStore();
    
    public SettingsStore(string? baseDirectory = null)
    {
        if (baseDirectory != null)
        {
            settingsPath = Path.Combine(baseDirectory, "settings.json");
        }
        else
        {
            string localAppData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            string yolotermDir = Path.Combine(localAppData, "YOLOTerm");
            Directory.CreateDirectory(yolotermDir);
            settingsPath = Path.Combine(yolotermDir, "settings.json");
        }
    }
    
    public async Task<Settings> LoadAsync()
    {
        if (!File.Exists(settingsPath))
        {
            return new Settings();
        }
        
        try
        {
            string json = await File.ReadAllTextAsync(settingsPath);
            Settings? settings = JsonSerializer.Deserialize<Settings>(json, JsonOptions);
            return settings ?? new Settings();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SettingsStore: Failed to load settings: {ex.Message}");
            return new Settings();
        }
    }
    
    public async Task SaveAsync(Settings settings)
    {
        try
        {
            string json = JsonSerializer.Serialize(settings, JsonOptions);
            
            // Atomic write: write to temp file, then move
            string tempPath = settingsPath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json);
            File.Move(tempPath, settingsPath, overwrite: true);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SettingsStore: Failed to save settings: {ex.Message}");
            throw;
        }
    }
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    
    // Helper methods for recent directories
    public string[] GetRecentDirectories()
    {
        var settings = LoadAsync().Result;
        return settings.RecentDirectories;
    }
    
    public void SaveRecentDirectories(IEnumerable<string> directories)
    {
        var settings = LoadAsync().Result;
        settings.RecentDirectories = directories.ToArray();
        SaveAsync(settings).Wait();
    }
}
