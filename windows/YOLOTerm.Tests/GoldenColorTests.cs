using YOLOTerm.Core.Contracts;
using YOLOTerm.Core.Terminal;
using Xunit.Abstractions;

namespace YOLOTerm.Tests;

/// <summary>
/// Golden test runner for color fixtures (B1.6 - GATE 4 requirement)
/// Tests that terminal rendering matches the golden corpus
/// </summary>
public class GoldenColorTests
{
    private readonly ITestOutputHelper _output;

    public GoldenColorTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void AllColorFixtures_ShouldPassGoldenTests()
    {
        var fixturesPath = GetFixturesPath();
        _output.WriteLine($"Loading fixtures from: {fixturesPath}");

        var fixtures = ColorFixtureLoader.LoadFixtures(fixturesPath);
        _output.WriteLine($"Loaded {fixtures.Count} fixtures");

        var failures = new List<string>();

        foreach (var fixture in fixtures)
        {
            _output.WriteLine($"\nTesting: {fixture.Name}");
            
            try
            {
                var result = RunFixture(fixture);
                if (!result.Success)
                {
                    failures.Add($"{fixture.Name}: {result.Error}");
                    _output.WriteLine($"  FAIL: {result.Error}");
                }
                else
                {
                    _output.WriteLine($"  PASS");
                }
            }
            catch (Exception ex)
            {
                failures.Add($"{fixture.Name}: Exception - {ex.Message}");
                _output.WriteLine($"  EXCEPTION: {ex.Message}");
            }
        }

        if (failures.Count > 0)
        {
            var errorMsg = $"{failures.Count} fixture(s) failed:\n" + string.Join("\n", failures);
            _output.WriteLine($"\n\nFAILURE SUMMARY:\n{errorMsg}");
            Assert.Fail(errorMsg);
        }

        _output.WriteLine($"\n\nSUCCESS: All {fixtures.Count} fixtures passed!");
    }

    private FixtureResult RunFixture(ColorFixture fixture)
    {
        var state = new HeadlessTerminalState(80, 24);
        var surface = new TerminalSurfaceAdapter(state);

        var input = fixture.GetInputBytes();
        surface.Feed(input);

        for (int rowIdx = 0; rowIdx < fixture.Grid.Count; rowIdx++)
        {
            var expectedRow = fixture.Grid[rowIdx];
            
            for (int colIdx = 0; colIdx < expectedRow.Count; colIdx++)
            {
                var expected = expectedRow[colIdx];
                var actual = surface.GetCell(colIdx, rowIdx);

                if (actual == null)
                {
                    return new FixtureResult
                    {
                        Success = false,
                        Error = $"Cell at ({colIdx}, {rowIdx}) is null"
                    };
                }

                var error = CompareCells(expected, actual.Value, colIdx, rowIdx);
                if (error != null)
                {
                    return new FixtureResult
                    {
                        Success = false,
                        Error = error
                    };
                }
            }
        }

        return new FixtureResult { Success = true };
    }

    private string? CompareCells(ExpectedCell expected, Cell actual, int col, int row)
    {
        if (expected.Char != actual.Char)
        {
            return $"Cell ({col}, {row}): char mismatch. Expected '{expected.Char}', got '{actual.Char}'";
        }

        var expectedFg = expected.GetFgColor();
        var expectedBg = expected.GetBgColor();
        var expectedAttrs = expected.GetAttributes();

        if (expectedFg.HasValue != actual.Fg.HasValue)
        {
            return $"Cell ({col}, {row}): fg presence mismatch. Expected {(expectedFg.HasValue ? "color" : "null")}, got {(actual.Fg.HasValue ? "color" : "null")}";
        }

        if (expectedFg.HasValue && actual.Fg.HasValue && !ColorsEqual(expectedFg.Value, actual.Fg.Value))
        {
            return $"Cell ({col}, {row}): fg color mismatch. Expected {expectedFg.Value.ToHex()}, got {actual.Fg.Value.ToHex()}";
        }

        if (expectedBg.HasValue != actual.Bg.HasValue)
        {
            return $"Cell ({col}, {row}): bg presence mismatch. Expected {(expectedBg.HasValue ? "color" : "null")}, got {(actual.Bg.HasValue ? "color" : "null")}";
        }

        if (expectedBg.HasValue && actual.Bg.HasValue && !ColorsEqual(expectedBg.Value, actual.Bg.Value))
        {
            return $"Cell ({col}, {row}): bg color mismatch. Expected {expectedBg.Value.ToHex()}, got {actual.Bg.Value.ToHex()}";
        }

        if (expectedAttrs != actual.Attrs)
        {
            return $"Cell ({col}, {row}): attrs mismatch. Expected {expectedAttrs}, got {actual.Attrs}";
        }

        return null;
    }

    private bool ColorsEqual(Color a, Color b)
    {
        return a.R == b.R && a.G == b.G && a.B == b.B;
    }

    private string GetFixturesPath()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var projectRoot = FindProjectRoot(currentDir);
        
        if (projectRoot == null)
            throw new InvalidOperationException("Could not find project root");

        var path = Path.Combine(projectRoot, "contracts", "fixtures", "colors", "color-fixtures.json");
        
        if (!File.Exists(path))
            throw new FileNotFoundException($"Color fixtures not found at: {path}");

        return path;
    }

    private string? FindProjectRoot(string startPath)
    {
        var dir = new DirectoryInfo(startPath);
        
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "contracts")))
                return dir.FullName;
            
            dir = dir.Parent;
        }

        return null;
    }
}

internal class FixtureResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
}
