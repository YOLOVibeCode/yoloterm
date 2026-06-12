using System.Text;
using Newtonsoft.Json;
using YOLOTerm.Core.Contracts;
using YOLOTerm.Core.Terminal;

namespace YOLOTerm.Tests;

/// <summary>
/// Golden test fixture from contracts/fixtures/colors/color-fixtures.json
/// </summary>
public class ColorFixture
{
    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("input_b64")]
    public string InputBase64 { get; set; } = string.Empty;

    [JsonProperty("grid")]
    public List<List<ExpectedCell>> Grid { get; set; } = new();

    public byte[] GetInputBytes()
    {
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(InputBase64));
        return Encoding.UTF8.GetBytes(decoded);
    }
}

public class ExpectedCell
{
    [JsonProperty("ch")]
    public string Char { get; set; } = string.Empty;

    [JsonProperty("fg")]
    public string? Fg { get; set; }

    [JsonProperty("bg")]
    public string? Bg { get; set; }

    [JsonProperty("attrs")]
    public List<string>? Attrs { get; set; }

    public CellAttributes GetAttributes()
    {
        if (Attrs == null || Attrs.Count == 0)
            return CellAttributes.None;

        var result = CellAttributes.None;
        foreach (var attr in Attrs)
        {
            result |= attr.ToLowerInvariant() switch
            {
                "bold" => CellAttributes.Bold,
                "italic" => CellAttributes.Italic,
                "underline" => CellAttributes.Underline,
                "strikethrough" => CellAttributes.Strikethrough,
                "dim" => CellAttributes.Dim,
                "inverse" => CellAttributes.Inverse,
                _ => CellAttributes.None
            };
        }
        return result;
    }

    public Color? GetFgColor() => Fg != null ? Color.FromHex(Fg) : null;
    public Color? GetBgColor() => Bg != null ? Color.FromHex(Bg) : null;
}

/// <summary>
/// Test fixture loader
/// </summary>
public static class ColorFixtureLoader
{
    public static List<ColorFixture> LoadFixtures(string fixturesPath)
    {
        var json = File.ReadAllText(fixturesPath);
        return JsonConvert.DeserializeObject<List<ColorFixture>>(json)
            ?? throw new InvalidOperationException("Failed to load color fixtures");
    }
}
