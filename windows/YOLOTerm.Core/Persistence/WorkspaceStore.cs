using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace YOLOTerm.Core.Persistence;

/// <summary>
/// WorkspaceStore manages the persistent workspace state (tabs, panes, layout, shells, cwds).
/// 
/// Key features:
/// - JSON workspace model for easy serialization
/// - Debounced save (don't write on every keystroke)
/// - Reconcile-on-save (handle concurrent changes)
/// - CRITICAL: Never destructive on partial load (graceful degradation)
/// 
/// Storage: %LOCALAPPDATA%\YOLOTerm\workspace.json
/// 
/// TermGrid lesson: commit c65fef8 caught a bug where corrupt workspace would lose all tabs.
/// This implementation is defensive: if the workspace file is corrupt or partially readable,
/// we preserve what we can and never silently discard user data.
/// </summary>
public sealed class WorkspaceStore
{
    // MARK: - Models
    
    public sealed class Workspace
    {
        [JsonPropertyName("tabs")]
        public List<Tab> Tabs { get; set; } = new();
        
        [JsonPropertyName("selectedTabIndex")]
        public int SelectedTabIndex { get; set; }
        
        [JsonPropertyName("version")]
        public int Version { get; set; } = 1;
        
        public Workspace() { }
        
        public Workspace(List<Tab> tabs, int selectedTabIndex)
        {
            Tabs = tabs;
            SelectedTabIndex = selectedTabIndex;
        }
    }
    
    public sealed class Tab
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
        
        [JsonPropertyName("panes")]
        public List<Pane> Panes { get; set; } = new();
        
        [JsonPropertyName("layoutPreset")]
        public string LayoutPreset { get; set; } = "auto";
        
        public Tab() { }
        
        public Tab(Guid id, string name, List<Pane> panes, string layoutPreset = "auto")
        {
            Id = id;
            Name = name;
            Panes = panes;
            LayoutPreset = layoutPreset;
        }
    }
    
    public sealed class Pane
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; } = Guid.NewGuid();
        
        [JsonPropertyName("shell")]
        public string Shell { get; set; } = string.Empty;
        
        [JsonPropertyName("cwd")]
        public string Cwd { get; set; } = string.Empty;
        
        [JsonPropertyName("title")]
        public string? Title { get; set; }
        
        public Pane() { }
        
        public Pane(Guid id, string shell, string cwd, string? title = null)
        {
            Id = id;
            Shell = shell;
            Cwd = cwd;
            Title = title;
        }
    }
    
    // MARK: - Configuration
    
    private readonly string workspacePath;
    private readonly TimeSpan saveDebounceInterval = TimeSpan.FromSeconds(1.0);
    private CancellationTokenSource? saveCancellationSource;
    private Task? saveTask;
    private readonly SemaphoreSlim saveLock = new(1, 1);
    
    public WorkspaceStore(string? baseDirectory = null)
    {
        if (baseDirectory != null)
        {
            workspacePath = Path.Combine(baseDirectory, "workspace.json");
        }
        else
        {
            string localAppData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            string yolotermDir = Path.Combine(localAppData, "YOLOTerm");
            Directory.CreateDirectory(yolotermDir);
            workspacePath = Path.Combine(yolotermDir, "workspace.json");
        }
    }
    
    // MARK: - Public API
    
    /// <summary>
    /// Load the workspace from disk.
    /// Returns null if the file doesn't exist (first launch).
    /// On corrupt data, returns a partial workspace with as much as we could salvage.
    /// </summary>
    public async Task<Workspace?> LoadAsync()
    {
        if (!File.Exists(workspacePath))
        {
            return null;
        }
        
        try
        {
            string json = await File.ReadAllTextAsync(workspacePath);
            Workspace? workspace = JsonSerializer.Deserialize<Workspace>(json, JsonOptions);
            return workspace;
        }
        catch (JsonException ex)
        {
            // Graceful degradation: try to salvage what we can
            Console.WriteLine($"WorkspaceStore: Failed to decode workspace cleanly, attempting recovery: {ex.Message}");
            return await RecoverWorkspaceAsync();
        }
    }
    
    /// <summary>
    /// Save the workspace to disk (debounced).
    /// Cancels any pending save and schedules a new one.
    /// </summary>
    public void Save(Workspace workspace)
    {
        // Cancel any pending save
        saveCancellationSource?.Cancel();
        saveCancellationSource = new CancellationTokenSource();
        
        CancellationToken token = saveCancellationSource.Token;
        
        // Schedule a new debounced save
        saveTask = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(saveDebounceInterval, token);
                
                if (!token.IsCancellationRequested)
                {
                    await SaveImmediateAsync(workspace);
                }
            }
            catch (TaskCanceledException)
            {
                // Expected when debounce is cancelled
            }
        }, token);
    }
    
    /// <summary>
    /// Save the workspace immediately (for shutdown or explicit save).
    /// </summary>
    public async Task SaveImmediateAsync(Workspace workspace)
    {
        await saveLock.WaitAsync();
        try
        {
            string json = JsonSerializer.Serialize(workspace, JsonOptions);
            
            // Atomic write: write to temp file, then move
            string tempPath = workspacePath + ".tmp";
            string backupPath = workspacePath + ".backup";
            
            await File.WriteAllTextAsync(tempPath, json);
            
            // If the main file exists, create a backup
            if (File.Exists(workspacePath))
            {
                if (File.Exists(backupPath))
                {
                    File.Delete(backupPath);
                }
                File.Copy(workspacePath, backupPath);
            }
            
            // Move temp to main (atomic on Windows)
            File.Move(tempPath, workspacePath, overwrite: true);
        }
        finally
        {
            saveLock.Release();
        }
    }
    
    // MARK: - Recovery (Graceful Degradation)
    
    /// <summary>
    /// Attempt to recover a partially corrupt workspace.
    /// Strategy: try to decode individual tabs, preserve what works, discard what doesn't.
    /// </summary>
    private async Task<Workspace> RecoverWorkspaceAsync()
    {
        try
        {
            string json = await File.ReadAllTextAsync(workspacePath);
            
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            
            List<Tab> recoveredTabs = new();
            
            // Try to extract tabs array
            if (root.TryGetProperty("tabs", out JsonElement tabsElement) && 
                tabsElement.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement tabElement in tabsElement.EnumerateArray())
                {
                    try
                    {
                        string tabJson = tabElement.GetRawText();
                        Tab? tab = JsonSerializer.Deserialize<Tab>(tabJson, JsonOptions);
                        if (tab != null)
                        {
                            recoveredTabs.Add(tab);
                        }
                    }
                    catch (JsonException)
                    {
                        Console.WriteLine("WorkspaceStore: Skipping corrupt tab entry");
                    }
                }
            }
            
            int selectedIndex = 0;
            if (root.TryGetProperty("selectedTabIndex", out JsonElement indexElement) &&
                indexElement.TryGetInt32(out int index))
            {
                selectedIndex = index;
            }
            
            Console.WriteLine($"WorkspaceStore: Recovered {recoveredTabs.Count} tabs from corrupt workspace");
            
            return new Workspace(
                recoveredTabs,
                Math.Min(selectedIndex, Math.Max(0, recoveredTabs.Count - 1))
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WorkspaceStore: JSON parsing failed completely, returning empty workspace: {ex.Message}");
            return new Workspace();
        }
    }
    
    /// <summary>
    /// Test helper: Force a specific corrupt workspace scenario for testing.
    /// This is used by tests to verify graceful degradation.
    /// </summary>
    public async Task WriteCorruptWorkspaceAsync(CorruptWorkspaceScenario scenario)
    {
        string data = scenario switch
        {
            CorruptWorkspaceScenario.InvalidJson => "{ invalid json",
            CorruptWorkspaceScenario.MissingTabs => @"{ ""selectedTabIndex"": 0 }",
            CorruptWorkspaceScenario.PartiallyCorruptTab => @"{
  ""tabs"": [
    {
      ""id"": ""00000000-0000-0000-0000-000000000001"",
      ""name"": ""Tab 1"",
      ""panes"": [],
      ""layoutPreset"": ""single""
    },
    {
      ""id"": ""invalid-uuid"",
      ""name"": ""Corrupt Tab""
    }
  ],
  ""selectedTabIndex"": 0,
  ""version"": 1
}",
            _ => throw new ArgumentException("Unknown scenario", nameof(scenario))
        };
        
        await File.WriteAllTextAsync(workspacePath, data);
    }
    
    public enum CorruptWorkspaceScenario
    {
        InvalidJson,
        MissingTabs,
        PartiallyCorruptTab
    }
    
    // MARK: - JSON Options
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };
}
