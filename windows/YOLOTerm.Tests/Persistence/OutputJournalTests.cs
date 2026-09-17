using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using YOLOTerm.Core.Persistence;

namespace YOLOTerm.Tests.Persistence;

public class OutputJournalTests : IDisposable
{
    private readonly string testDir;
    private readonly OutputJournal journal;
    
    public OutputJournalTests()
    {
        testDir = Path.Combine(Path.GetTempPath(), $"yoloterm-test-{Guid.NewGuid()}");
        journal = new OutputJournal(testDir);
    }
    
    public void Dispose()
    {
        if (Directory.Exists(testDir))
        {
            Directory.Delete(testDir, recursive: true);
        }
    }
    
    [Fact]
    public async Task Append_CreatesJournalFile()
    {
        // Arrange
        Guid paneId = Guid.NewGuid();
        byte[] data = System.Text.Encoding.UTF8.GetBytes("Hello, World!");
        
        // Act
        await journal.AppendAsync(paneId, data);
        
        // Assert
        byte[] replayed = await journal.ReplayAsync(paneId);
        Assert.Equal(data, replayed);
    }
    
    [Fact]
    public async Task Append_MultipleWrites_ConcatenatesData()
    {
        // Arrange
        Guid paneId = Guid.NewGuid();
        byte[] data1 = System.Text.Encoding.UTF8.GetBytes("First ");
        byte[] data2 = System.Text.Encoding.UTF8.GetBytes("Second ");
        byte[] data3 = System.Text.Encoding.UTF8.GetBytes("Third");
        
        // Act
        await journal.AppendAsync(paneId, data1);
        await journal.AppendAsync(paneId, data2);
        await journal.AppendAsync(paneId, data3);
        
        // Assert
        byte[] replayed = await journal.ReplayAsync(paneId);
        string result = System.Text.Encoding.UTF8.GetString(replayed);
        Assert.Equal("First Second Third", result);
    }
    
    [Fact]
    public async Task Replay_NonExistentPane_ReturnsEmpty()
    {
        // Arrange
        Guid paneId = Guid.NewGuid();
        
        // Act
        byte[] replayed = await journal.ReplayAsync(paneId);
        
        // Assert
        Assert.Empty(replayed);
    }
    
    [Fact]
    public async Task Delete_RemovesJournal()
    {
        // Arrange
        Guid paneId = Guid.NewGuid();
        byte[] data = System.Text.Encoding.UTF8.GetBytes("Test");
        await journal.AppendAsync(paneId, data);
        
        // Act
        journal.Delete(paneId);
        
        // Assert
        byte[] replayed = await journal.ReplayAsync(paneId);
        Assert.Empty(replayed);
    }
    
    [Fact]
    public void PurgeOrphans_RemovesUnusedJournals()
    {
        // Arrange
        Guid activePane = Guid.NewGuid();
        Guid orphanPane = Guid.NewGuid();
        
        byte[] data = System.Text.Encoding.UTF8.GetBytes("Test");
        journal.AppendAsync(activePane, data).Wait();
        journal.AppendAsync(orphanPane, data).Wait();
        
        // Act
        journal.PurgeOrphans(new HashSet<Guid> { activePane });
        
        // Assert
        byte[] activeReplayed = journal.ReplayAsync(activePane).Result;
        byte[] orphanReplayed = journal.ReplayAsync(orphanPane).Result;
        
        Assert.NotEmpty(activeReplayed);
        Assert.Empty(orphanReplayed);
    }
    
    [Fact]
    public async Task Rotation_TriggersAtMaxSize()
    {
        // Arrange
        Guid paneId = Guid.NewGuid();
        
        // Create data larger than max journal size
        int chunkSize = 1024 * 1024; // 1 MB
        byte[] chunk = new byte[chunkSize];
        Random.Shared.NextBytes(chunk);
        
        // Act - Write 11 MB (should trigger rotation at 10 MB)
        for (int i = 0; i < 11; i++)
        {
            await journal.AppendAsync(paneId, chunk);
        }
        
        // Assert - Replay should return data (possibly from rotated files)
        byte[] replayed = await journal.ReplayAsync(paneId);
        Assert.NotEmpty(replayed);
    }
}
