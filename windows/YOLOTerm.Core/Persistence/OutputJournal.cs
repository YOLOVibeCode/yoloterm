using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace YOLOTerm.Core.Persistence;

/// <summary>
/// OutputJournal manages append-only capped raw-byte files for terminal output capture.
/// Each pane gets its own journal file for scrollback restoration.
/// 
/// Features:
/// - Capped size with atomic rotation (prevents unbounded growth)
/// - Replay-before-attach for restoring scrollback on pane reopen
/// - Orphan purge on startup (cleans up journals for deleted panes)
/// 
/// Storage: %LOCALAPPDATA%\YOLOTerm\journals\{pane-uuid}.bytes
/// </summary>
public sealed class OutputJournal
{
    // MARK: - Configuration
    
    /// <summary>
    /// Maximum journal file size before rotation (default: 10 MB)
    /// </summary>
    public const int MaxJournalSize = 10 * 1024 * 1024;
    
    /// <summary>
    /// Number of rotated journals to keep (default: 2)
    /// </summary>
    public const int MaxRotatedFiles = 2;
    
    // MARK: - Paths
    
    private readonly string journalsDirectory;
    
    public OutputJournal(string? baseDirectory = null)
    {
        if (baseDirectory != null)
        {
            journalsDirectory = Path.Combine(baseDirectory, "journals");
        }
        else
        {
            string localAppData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            string yolotermDir = Path.Combine(localAppData, "YOLOTerm");
            journalsDirectory = Path.Combine(yolotermDir, "journals");
        }
        
        // Ensure journals directory exists
        Directory.CreateDirectory(journalsDirectory);
    }
    
    // MARK: - Public API
    
    /// <summary>
    /// Append raw terminal output bytes to the journal for a given pane.
    /// </summary>
    public async Task AppendAsync(Guid paneId, byte[] data)
    {
        string journalPath = GetJournalPath(paneId);
        
        // Open or create the journal file (append mode)
        await using (FileStream fs = new FileStream(
            journalPath,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read,
            bufferSize: 4096,
            useAsync: true))
        {
            await fs.WriteAsync(data, 0, data.Length);
        }
        
        // Check if rotation is needed
        FileInfo fileInfo = new FileInfo(journalPath);
        if (fileInfo.Exists && fileInfo.Length >= MaxJournalSize)
        {
            await RotateJournalAsync(paneId);
        }
    }
    
    /// <summary>
    /// Replay the journal for a pane, returning all captured output bytes.
    /// Used to restore scrollback when reopening a pane.
    /// </summary>
    public async Task<byte[]> ReplayAsync(Guid paneId)
    {
        string journalPath = GetJournalPath(paneId);
        
        if (!File.Exists(journalPath))
        {
            return Array.Empty<byte>();
        }
        
        // Read all rotated journals in order, then the main journal
        List<byte> allData = new List<byte>();
        
        // Read rotated journals in reverse order (oldest to newest)
        for (int i = MaxRotatedFiles - 1; i >= 0; i--)
        {
            string rotatedPath = GetRotatedJournalPath(paneId, i);
            if (File.Exists(rotatedPath))
            {
                byte[] rotatedData = await File.ReadAllBytesAsync(rotatedPath);
                allData.AddRange(rotatedData);
            }
        }
        
        // Append the main journal
        byte[] mainData = await File.ReadAllBytesAsync(journalPath);
        allData.AddRange(mainData);
        
        return allData.ToArray();
    }
    
    /// <summary>
    /// Delete the journal for a pane (called when pane is closed).
    /// </summary>
    public void Delete(Guid paneId)
    {
        string journalPath = GetJournalPath(paneId);
        
        // Delete main journal
        if (File.Exists(journalPath))
        {
            File.Delete(journalPath);
        }
        
        // Delete rotated journals
        for (int i = 0; i < MaxRotatedFiles; i++)
        {
            string rotatedPath = GetRotatedJournalPath(paneId, i);
            if (File.Exists(rotatedPath))
            {
                File.Delete(rotatedPath);
            }
        }
    }
    
    /// <summary>
    /// Purge orphaned journals (journals for panes that no longer exist).
    /// Should be called on app startup with the set of active pane IDs.
    /// </summary>
    public void PurgeOrphans(ISet<Guid> activePaneIds)
    {
        if (!Directory.Exists(journalsDirectory))
        {
            return;
        }
        
        string[] files = Directory.GetFiles(journalsDirectory, "*.bytes*");
        
        foreach (string filePath in files)
        {
            string filename = Path.GetFileName(filePath);
            
            // Extract pane UUID from filename (format: {uuid}.bytes or {uuid}.bytes.{n})
            string[] parts = filename.Split('.');
            if (parts.Length > 0 && Guid.TryParse(parts[0], out Guid paneId))
            {
                // If this pane ID is not active, delete the journal
                if (!activePaneIds.Contains(paneId))
                {
                    File.Delete(filePath);
                }
            }
        }
    }
    
    // MARK: - Private Helpers
    
    private string GetJournalPath(Guid paneId)
    {
        return Path.Combine(journalsDirectory, $"{paneId:D}.bytes");
    }
    
    private string GetRotatedJournalPath(Guid paneId, int index)
    {
        return Path.Combine(journalsDirectory, $"{paneId:D}.bytes.{index}");
    }
    
    /// <summary>
    /// Rotate the journal: rename current to .0, shift existing rotated files up, delete oldest.
    /// </summary>
    private async Task RotateJournalAsync(Guid paneId)
    {
        string mainPath = GetJournalPath(paneId);
        
        // Delete the oldest rotated file if it exists
        string oldestPath = GetRotatedJournalPath(paneId, MaxRotatedFiles - 1);
        if (File.Exists(oldestPath))
        {
            File.Delete(oldestPath);
        }
        
        // Shift all rotated files up by one
        for (int i = MaxRotatedFiles - 2; i >= 0; i--)
        {
            string fromPath = GetRotatedJournalPath(paneId, i);
            string toPath = GetRotatedJournalPath(paneId, i + 1);
            
            if (File.Exists(fromPath))
            {
                File.Move(fromPath, toPath, overwrite: true);
            }
        }
        
        // Move main journal to .0
        string newRotatedPath = GetRotatedJournalPath(paneId, 0);
        File.Move(mainPath, newRotatedPath, overwrite: true);
        
        // Create new empty main journal
        await using (File.Create(mainPath)) { }
    }
}
