using Newtonsoft.Json;

namespace YOLOTerm.Core.Contracts;

/// <summary>
/// Environment policy from contracts/fixtures/env-policy.json (contracts v1)
/// </summary>
public class EnvPolicy
{
    [JsonProperty("set")]
    public Dictionary<string, string> Set { get; set; } = new();

    [JsonProperty("remove")]
    public List<string> Remove { get; set; } = new();

    /// <summary>
    /// Apply environment policy to parent environment
    /// </summary>
    /// <param name="parentEnv">Parent process environment</param>
    /// <param name="version">YOLOTerm version to substitute into TERM_PROGRAM_VERSION</param>
    /// <returns>Sanitized environment</returns>
    public Dictionary<string, string> Apply(Dictionary<string, string> parentEnv, string version)
    {
        var result = new Dictionary<string, string>(parentEnv, StringComparer.OrdinalIgnoreCase);

        // Remove hostile vars
        foreach (var key in Remove)
        {
            result.Remove(key);
        }

        // Set policy vars
        foreach (var kvp in Set)
        {
            var value = kvp.Value == "<app version placeholder>"
                ? version
                : kvp.Value;
            result[kvp.Key] = value;
        }

        return result;
    }

    /// <summary>
    /// Load environment policy from JSON file
    /// </summary>
    public static EnvPolicy Load(string path)
    {
        var json = File.ReadAllText(path);
        return JsonConvert.DeserializeObject<EnvPolicy>(json)
            ?? throw new InvalidOperationException("Failed to parse env-policy.json");
    }
}
