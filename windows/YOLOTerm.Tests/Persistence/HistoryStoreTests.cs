using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using YOLOTerm.Core.Persistence;

namespace YOLOTerm.Tests.Persistence;

public class HistoryStoreTests : IDisposable
{
    private readonly string testDir;
    private readonly HistoryStore store;
    
    public HistoryStoreTests()
    {
        testDir = Path.Combine(Path.GetTempPath(), $"yoloterm-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);
        store = new HistoryStore(testDir, redactionPatternsPath: null); // No redaction for basic tests
    }
    
    public void Dispose()
    {
        store.Dispose();
        if (Directory.Exists(testDir))
        {
            Directory.Delete(testDir, recursive: true);
        }
    }
    
    [Fact]
    public async Task Insert_And_Recent_RoundTrip()
    {
        // Arrange
        var command = new HistoryStore.Command
        {
            CommandText = "git status",
            Shell = "pwsh",
            Cwd = @"C:\Projects",
            PaneId = Guid.NewGuid().ToString(),
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ExitCode = 0
        };
        
        // Act
        await store.InsertAsync(command);
        var recent = await store.RecentAsync(new HistoryStore.SearchOptions { Limit = 10 });
        
        // Assert
        Assert.NotEmpty(recent);
        Assert.Equal("git status", recent[0].CommandText);
        Assert.Equal("pwsh", recent[0].Shell);
        Assert.Equal(0, recent[0].ExitCode);
    }
    
    [Fact]
    public async Task Search_FindsMatchingCommands()
    {
        // Arrange
        var commands = new[]
        {
            new HistoryStore.Command 
            { 
                CommandText = "git status", 
                Shell = "bash",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            },
            new HistoryStore.Command 
            { 
                CommandText = "git log", 
                Shell = "bash",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            },
            new HistoryStore.Command 
            { 
                CommandText = "npm install", 
                Shell = "pwsh",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            }
        };
        
        foreach (var cmd in commands)
        {
            await store.InsertAsync(cmd);
        }
        
        // Act
        var results = await store.SearchAsync("git", new HistoryStore.SearchOptions());
        
        // Assert
        Assert.Equal(2, results.Count);
        Assert.All(results, r => Assert.Contains("git", r.CommandText));
    }
    
    [Fact]
    public async Task Search_PaneScoped_FiltersCorrectly()
    {
        // Arrange
        string pane1 = Guid.NewGuid().ToString();
        string pane2 = Guid.NewGuid().ToString();
        
        await store.InsertAsync(new HistoryStore.Command
        {
            CommandText = "ls -la",
            Shell = "bash",
            PaneId = pane1,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
        
        await store.InsertAsync(new HistoryStore.Command
        {
            CommandText = "pwd",
            Shell = "bash",
            PaneId = pane2,
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });
        
        // Act
        var pane1Results = await store.RecentAsync(new HistoryStore.SearchOptions { PaneId = pane1 });
        var pane2Results = await store.RecentAsync(new HistoryStore.SearchOptions { PaneId = pane2 });
        
        // Assert
        Assert.Single(pane1Results);
        Assert.Equal("ls -la", pane1Results[0].CommandText);
        
        Assert.Single(pane2Results);
        Assert.Equal("pwd", pane2Results[0].CommandText);
    }
    
    [Fact]
    public async Task ToggleFavorite_UpdatesStatus()
    {
        // Arrange
        var command = new HistoryStore.Command
        {
            CommandText = "git push",
            Shell = "bash",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        
        await store.InsertAsync(command);
        var recent = await store.RecentAsync(new HistoryStore.SearchOptions { Limit = 1 });
        long commandId = recent[0].Id!.Value;
        
        // Act
        await store.ToggleFavoriteAsync(commandId);
        
        // Assert
        var updated = await store.RecentAsync(new HistoryStore.SearchOptions { Limit = 1 });
        Assert.True(updated[0].Favorite);
    }
    
    [Fact]
    public async Task UpdateNote_SavesNote()
    {
        // Arrange
        var command = new HistoryStore.Command
        {
            CommandText = "docker ps",
            Shell = "pwsh",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        
        await store.InsertAsync(command);
        var recent = await store.RecentAsync(new HistoryStore.SearchOptions { Limit = 1 });
        long commandId = recent[0].Id!.Value;
        
        // Act
        await store.UpdateNoteAsync(commandId, "This shows all running containers");
        
        // Assert
        var updated = await store.RecentAsync(new HistoryStore.SearchOptions { Limit = 1 });
        Assert.Equal("This shows all running containers", updated[0].Note);
    }
    
    [Fact]
    public async Task DeleteOlderThan_RemovesOldCommands()
    {
        // Arrange
        var oldTimestamp = DateTimeOffset.UtcNow.AddDays(-100).ToUnixTimeMilliseconds();
        var recentTimestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        
        await store.InsertAsync(new HistoryStore.Command
        {
            CommandText = "old command",
            Shell = "bash",
            Timestamp = oldTimestamp
        });
        
        await store.InsertAsync(new HistoryStore.Command
        {
            CommandText = "recent command",
            Shell = "bash",
            Timestamp = recentTimestamp
        });
        
        // Act
        await store.DeleteOlderThanAsync(90);
        
        // Assert
        var remaining = await store.RecentAsync(new HistoryStore.SearchOptions());
        Assert.Single(remaining);
        Assert.Equal("recent command", remaining[0].CommandText);
    }
    
    [Fact]
    public async Task Count_ReturnsCorrectNumber()
    {
        // Arrange
        for (int i = 0; i < 5; i++)
        {
            await store.InsertAsync(new HistoryStore.Command
            {
                CommandText = $"command {i}",
                Shell = "bash",
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
            });
        }
        
        // Act
        int count = await store.CountAsync();
        
        // Assert
        Assert.Equal(5, count);
    }
}
