using System.Text.Json;
using System.Text.Json.Serialization;
using YOLOTerm.Core.Layout;
using Xunit;

namespace YOLOTerm.Tests;

public class LayoutEngineTests
{
    private class LayoutFixture
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("input")]
        public LayoutInput Input { get; set; } = new();

        [JsonPropertyName("expected")]
        public List<ExpectedRect> Expected { get; set; } = new();
    }

    private class LayoutInput
    {
        [JsonPropertyName("containerSize")]
        public ContainerSizeData ContainerSize { get; set; } = new();

        [JsonPropertyName("panes")]
        public List<PaneData> Panes { get; set; } = new();

        [JsonPropertyName("preset")]
        public string Preset { get; set; } = "";

        [JsonPropertyName("zoomedPane")]
        public string? ZoomedPane { get; set; }
    }

    private class ContainerSizeData
    {
        [JsonPropertyName("width")]
        public double Width { get; set; }

        [JsonPropertyName("height")]
        public double Height { get; set; }
    }

    private class PaneData
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";
    }

    private class ExpectedRect
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("x")]
        public double X { get; set; }

        [JsonPropertyName("y")]
        public double Y { get; set; }

        [JsonPropertyName("width")]
        public double Width { get; set; }

        [JsonPropertyName("height")]
        public double Height { get; set; }
    }

    private static string GetContractsPath()
    {
        // Navigate from test assembly to contracts directory
        var testDir = Directory.GetCurrentDirectory();
        var repoRoot = Path.GetFullPath(Path.Combine(testDir, "..", "..", "..", ".."));
        return Path.Combine(repoRoot, "contracts", "fixtures", "layout");
    }

    private static LayoutPreset ParsePreset(string preset)
    {
        return preset.ToLowerInvariant() switch
        {
            "auto" => LayoutPreset.Auto,
            "single" => LayoutPreset.Single,
            "columns" => LayoutPreset.Columns,
            "rows" => LayoutPreset.Rows,
            "grid" => LayoutPreset.Grid,
            "main-left" => LayoutPreset.MainLeft,
            "main-right" => LayoutPreset.MainRight,
            _ => throw new ArgumentException($"Unknown preset: {preset}")
        };
    }

    private static void AssertRectsEqual(List<ExpectedRect> expected, List<PaneRect> actual, string testName)
    {
        Assert.Equal(expected.Count, actual.Count);

        for (int i = 0; i < expected.Count; i++)
        {
            var exp = expected[i];
            var act = actual[i];

            Assert.Equal(exp.Id, act.Id);
            Assert.Equal(exp.X, act.X, precision: 2);
            Assert.Equal(exp.Y, act.Y, precision: 2);
            Assert.Equal(exp.Width, act.Width, precision: 2);
            Assert.Equal(exp.Height, act.Height, precision: 2);
        }
    }

    private void RunFixtureTest(string fixtureFileName)
    {
        var contractsPath = GetContractsPath();
        var fixturePath = Path.Combine(contractsPath, fixtureFileName);

        Assert.True(File.Exists(fixturePath), $"Fixture file not found: {fixturePath}");

        var json = File.ReadAllText(fixturePath);
        var fixture = JsonSerializer.Deserialize<LayoutFixture>(json);
        Assert.NotNull(fixture);

        var engine = new LayoutEngine();
        var paneIds = fixture.Input.Panes.Select(p => p.Id).ToList();
        var containerSize = new ContainerSize(
            fixture.Input.ContainerSize.Width,
            fixture.Input.ContainerSize.Height);
        var preset = ParsePreset(fixture.Input.Preset);

        var result = engine.Calculate(
            paneIds,
            preset,
            containerSize,
            zoomedPane: fixture.Input.ZoomedPane);

        AssertRectsEqual(fixture.Expected, result, fixture.Name);
    }

    [Fact]
    public void SinglePane_FillsContainer()
    {
        RunFixtureTest("single-pane.json");
    }

    [Fact]
    public void TwoColumns_SplitsHorizontally()
    {
        RunFixtureTest("two-columns.json");
    }

    [Fact]
    public void FourGrid_Creates2x2Layout()
    {
        RunFixtureTest("four-grid.json");
    }

    [Fact]
    public void Equalize_ResetsToDefaultSizing()
    {
        var engine = new LayoutEngine();
        var paneIds = new List<string> { "pane-1", "pane-2" };
        var containerSize = new ContainerSize(800, 600);

        var result = engine.Equalize(paneIds, LayoutPreset.Columns, containerSize);

        Assert.Equal(2, result.Count);
        Assert.Equal(0, result[0].X);
        Assert.Equal(400, result[0].Width);
        Assert.Equal(400, result[1].X);
        Assert.Equal(400, result[1].Width);
    }

    [Fact]
    public void ZoomedPane_FillsContainer()
    {
        var engine = new LayoutEngine();
        var paneIds = new List<string> { "pane-1", "pane-2", "pane-3" };
        var containerSize = new ContainerSize(800, 600);

        var result = engine.Calculate(
            paneIds,
            LayoutPreset.Columns,
            containerSize,
            zoomedPane: "pane-2");

        Assert.Single(result);
        Assert.Equal("pane-2", result[0].Id);
        Assert.Equal(0, result[0].X);
        Assert.Equal(0, result[0].Y);
        Assert.Equal(800, result[0].Width);
        Assert.Equal(600, result[0].Height);
    }

    [Fact]
    public void AutoPreset_SinglePane_UsesSingle()
    {
        var engine = new LayoutEngine();
        var paneIds = new List<string> { "pane-1" };
        var containerSize = new ContainerSize(800, 600);

        var result = engine.Calculate(paneIds, LayoutPreset.Auto, containerSize);

        Assert.Single(result);
        Assert.Equal(800, result[0].Width);
        Assert.Equal(600, result[0].Height);
    }

    [Fact]
    public void AutoPreset_TwoPanes_UsesColumns()
    {
        var engine = new LayoutEngine();
        var paneIds = new List<string> { "pane-1", "pane-2" };
        var containerSize = new ContainerSize(800, 600);

        var result = engine.Calculate(paneIds, LayoutPreset.Auto, containerSize);

        Assert.Equal(2, result.Count);
        Assert.Equal(400, result[0].Width);
        Assert.Equal(400, result[1].Width);
    }

    [Fact]
    public void AutoPreset_ThreePanes_UsesMainLeft()
    {
        var engine = new LayoutEngine();
        var paneIds = new List<string> { "pane-1", "pane-2", "pane-3" };
        var containerSize = new ContainerSize(800, 600);

        var result = engine.Calculate(paneIds, LayoutPreset.Auto, containerSize);

        Assert.Equal(3, result.Count);
        // Main pane on left
        Assert.Equal(400, result[0].Width);
        Assert.Equal(600, result[0].Height);
        // Side panes stacked
        Assert.Equal(400, result[1].Width);
        Assert.Equal(300, result[1].Height);
    }

    [Fact]
    public void AutoPreset_FourPanes_UsesGrid()
    {
        var engine = new LayoutEngine();
        var paneIds = new List<string> { "pane-1", "pane-2", "pane-3", "pane-4" };
        var containerSize = new ContainerSize(800, 600);

        var result = engine.Calculate(paneIds, LayoutPreset.Auto, containerSize);

        Assert.Equal(4, result.Count);
        // 2x2 grid
        Assert.All(result, r => Assert.Equal(400, r.Width));
        Assert.All(result, r => Assert.Equal(300, r.Height));
    }

    [Fact]
    public void MainLeft_FirstPaneTakes50Percent()
    {
        var engine = new LayoutEngine();
        var paneIds = new List<string> { "pane-1", "pane-2", "pane-3" };
        var containerSize = new ContainerSize(800, 600);

        var result = engine.Calculate(paneIds, LayoutPreset.MainLeft, containerSize);

        Assert.Equal(3, result.Count);
        Assert.Equal("pane-1", result[0].Id);
        Assert.Equal(400, result[0].Width);
        Assert.Equal(600, result[0].Height);
        Assert.Equal(0, result[0].X);
    }

    [Fact]
    public void MainRight_FirstPaneTakes50PercentOnRight()
    {
        var engine = new LayoutEngine();
        var paneIds = new List<string> { "pane-1", "pane-2", "pane-3" };
        var containerSize = new ContainerSize(800, 600);

        var result = engine.Calculate(paneIds, LayoutPreset.MainRight, containerSize);

        Assert.Equal(3, result.Count);
        // Main pane is last in the list (on the right)
        var mainPane = result.Last();
        Assert.Equal("pane-1", mainPane.Id);
        Assert.Equal(400, mainPane.Width);
        Assert.Equal(600, mainPane.Height);
        Assert.Equal(400, mainPane.X);
    }

    [Fact]
    public void Rows_StacksVertically()
    {
        var engine = new LayoutEngine();
        var paneIds = new List<string> { "pane-1", "pane-2", "pane-3" };
        var containerSize = new ContainerSize(800, 600);

        var result = engine.Calculate(paneIds, LayoutPreset.Rows, containerSize);

        Assert.Equal(3, result.Count);
        Assert.All(result, r => Assert.Equal(800, r.Width));
        Assert.All(result, r => Assert.Equal(200, r.Height));
        Assert.Equal(0, result[0].Y);
        Assert.Equal(200, result[1].Y);
        Assert.Equal(400, result[2].Y);
    }

    [Fact]
    public void EmptyPanes_ReturnsEmptyList()
    {
        var engine = new LayoutEngine();
        var paneIds = new List<string>();
        var containerSize = new ContainerSize(800, 600);

        var result = engine.Calculate(paneIds, LayoutPreset.Columns, containerSize);

        Assert.Empty(result);
    }
}
