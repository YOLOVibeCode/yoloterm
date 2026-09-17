using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace YOLOTerm.Core.Integration;

/// <summary>
/// Manages Windows taskbar Jump List with recent directories.
/// </summary>
public class JumpListManager
{
    private const string RecentDirsKey = "RecentDirectories";
    private const int MaxRecentDirectories = 10;
    
    private readonly List<string> _recentDirectories = new();
    
    public IReadOnlyList<string> RecentDirectories => _recentDirectories.AsReadOnly();
    
    public JumpListManager()
    {
        LoadRecentDirectories();
    }
    
    /// <summary>
    /// Adds a directory to the recent list and updates the Jump List.
    /// </summary>
    public void AddRecentDirectory(string path)
    {
        if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
        {
            return;
        }
        
        // Normalize path
        path = Path.GetFullPath(path);
        
        // Remove if already exists
        _recentDirectories.RemoveAll(d => string.Equals(d, path, StringComparison.OrdinalIgnoreCase));
        
        // Add to front
        _recentDirectories.Insert(0, path);
        
        // Limit size
        if (_recentDirectories.Count > MaxRecentDirectories)
        {
            _recentDirectories.RemoveRange(MaxRecentDirectories, _recentDirectories.Count - MaxRecentDirectories);
        }
        
        SaveRecentDirectories();
    }
    
    /// <summary>
    /// Clears all recent directories.
    /// </summary>
    public void ClearRecentDirectories()
    {
        _recentDirectories.Clear();
        SaveRecentDirectories();
    }
    
    private void LoadRecentDirectories()
    {
        try
        {
            var settings = Persistence.SettingsStore.Instance;
            var dirs = settings.GetRecentDirectories();
            
            _recentDirectories.Clear();
            _recentDirectories.AddRange(dirs.Where(Directory.Exists));
        }
        catch
        {
            // Ignore load errors
        }
    }
    
    private void SaveRecentDirectories()
    {
        try
        {
            var settings = Persistence.SettingsStore.Instance;
            settings.SaveRecentDirectories(_recentDirectories);
        }
        catch
        {
            // Ignore save errors
        }
    }
}
