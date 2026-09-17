using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using YOLOTerm.Core.Persistence;

namespace YOLOTerm.Tests.Persistence;

public class WorkspaceStoreTests : IDisposable
{
    private readonly string testDir;
    private readonly WorkspaceStore store;
    
    public WorkspaceStoreTests()
    {
        testDir = Path.Combine(Path.GetTempPath(), $"yoloterm-test-{Guid.NewGuid()}");
        Directory.CreateDirectory(testDir);
        store = new WorkspaceStore(testDir);
    }
    
    public void Dispose()
    {
        if (Directory.Exists(testDir))
        {
            Directory.Delete(testDir, recursive: true);
        }
    }
    
    [Fact]
    public async Task Load_NoFile_ReturnsNull()
    {
        // Act
        var workspace = await store.LoadAsync();
        
        // Assert
        Assert.Null(workspace);
    }
    
    [Fact]
    public async Task Save_And_Load_RoundTrip()
    {
        // Arrange
        var workspace = new WorkspaceStore.Workspace
        {
            Tabs = new()
            {
                new WorkspaceStore.Tab(
                    Guid.NewGuid(),
                    "Tab 1",
                    new()
                    {
                        new WorkspaceStore.Pane(Guid.NewGuid(), "pwsh", @"C:\Users\Test", "Test Pane")
                    },
                    "single"
                )
            },
            SelectedTabIndex = 0
        };
        
        // Act
        await store.SaveImmediateAsync(workspace);
        var loaded = await store.LoadAsync();
        
        // Assert
        Assert.NotNull(loaded);
        Assert.Single(loaded.Tabs);
        Assert.Equal("Tab 1", loaded.Tabs[0].Name);
        Assert.Equal("single", loaded.Tabs[0].LayoutPreset);
        Assert.Single(loaded.Tabs[0].Panes);
        Assert.Equal("pwsh", loaded.Tabs[0].Panes[0].Shell);
        Assert.Equal(@"C:\Users\Test", loaded.Tabs[0].Panes[0].Cwd);
    }
    
    [Fact]
    public async Task Save_Multiple_PreservesAllTabs()
    {
        // Arrange
        var workspace = new WorkspaceStore.Workspace
        {
            Tabs = new()
            {
                new WorkspaceStore.Tab(Guid.NewGuid(), "Tab 1", new(), "auto"),
                new WorkspaceStore.Tab(Guid.NewGuid(), "Tab 2", new(), "columns"),
                new WorkspaceStore.Tab(Guid.NewGuid(), "Tab 3", new(), "rows")
            },
            SelectedTabIndex = 1
        };
        
        // Act
        await store.SaveImmediateAsync(workspace);
        var loaded = await store.LoadAsync();
        
        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(3, loaded.Tabs.Count);
        Assert.Equal(1, loaded.SelectedTabIndex);
    }
    
    [Fact]
    public async Task GracefulDegradation_InvalidJson_ReturnsEmptyWorkspace()
    {
        // Arrange
        await store.WriteCorruptWorkspaceAsync(WorkspaceStore.CorruptWorkspaceScenario.InvalidJson);
        
        // Act
        var loaded = await store.LoadAsync();
        
        // Assert
        Assert.NotNull(loaded);
        Assert.Empty(loaded.Tabs);
    }
    
    [Fact]
    public async Task GracefulDegradation_MissingTabs_ReturnsEmptyWorkspace()
    {
        // Arrange
        await store.WriteCorruptWorkspaceAsync(WorkspaceStore.CorruptWorkspaceScenario.MissingTabs);
        
        // Act
        var loaded = await store.LoadAsync();
        
        // Assert
        Assert.NotNull(loaded);
        Assert.Empty(loaded.Tabs);
    }
    
    [Fact]
    public async Task GracefulDegradation_PartiallyCorruptTab_RecoverValidTabs()
    {
        // Arrange
        await store.WriteCorruptWorkspaceAsync(WorkspaceStore.CorruptWorkspaceScenario.PartiallyCorruptTab);
        
        // Act
        var loaded = await store.LoadAsync();
        
        // Assert - Should recover the valid tab, skip the corrupt one
        Assert.NotNull(loaded);
        Assert.Single(loaded.Tabs); // Only one valid tab
        Assert.Equal("Tab 1", loaded.Tabs[0].Name);
    }
}
