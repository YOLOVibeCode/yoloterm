using Newtonsoft.Json;

namespace YOLOTerm.Core.Theme;

/// <summary>
/// Theme definition matching contracts/themes/*.json (contracts v1)
/// </summary>
public class Theme
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("background")]
    public string Background { get; set; } = string.Empty;

    [JsonProperty("foreground")]
    public string Foreground { get; set; } = string.Empty;

    [JsonProperty("cursor")]
    public string Cursor { get; set; } = string.Empty;

    [JsonProperty("cursorAccent")]
    public string CursorAccent { get; set; } = string.Empty;

    [JsonProperty("selectionBackground")]
    public string SelectionBackground { get; set; } = string.Empty;

    [JsonProperty("useDefaultAnsi")]
    public bool UseDefaultAnsi { get; set; }

    [JsonProperty("ansiColors")]
    public List<string>? AnsiColors { get; set; }

    /// <summary>
    /// Load theme from JSON file
    /// </summary>
    public static Theme Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonConvert.DeserializeObject<Theme>(json)
            ?? throw new InvalidOperationException($"Failed to parse theme: {path}");
    }

    /// <summary>
    /// Load all themes from directory
    /// </summary>
    public static List<Theme> LoadAll(string directory)
    {
        var themes = new List<Theme>();
        foreach (var file in Directory.GetFiles(directory, "*.json"))
        {
            themes.Add(Load(file));
        }
        return themes;
    }
}
