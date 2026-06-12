using YOLOTerm.Core.Contracts;

namespace YOLOTerm.Tests;

/// <summary>
/// Tests for environment policy (B1.3 validation)
/// </summary>
public class EnvPolicyTests
{
    [Fact]
    public void EnvPolicy_RemovesHostileVars()
    {
        var policy = new EnvPolicy
        {
            Set = new Dictionary<string, string>
            {
                ["TERM"] = "xterm-256color",
                ["COLORTERM"] = "truecolor"
            },
            Remove = new List<string> { "NO_COLOR", "FORCE_COLOR", "CLICOLOR_FORCE" }
        };

        var parentEnv = new Dictionary<string, string>
        {
            ["NO_COLOR"] = "1",
            ["FORCE_COLOR"] = "1",
            ["CLICOLOR_FORCE"] = "1",
            ["PATH"] = "/usr/bin",
            ["HOME"] = "/home/user"
        };

        var result = policy.Apply(parentEnv, "0.1.0");

        Assert.False(result.ContainsKey("NO_COLOR"));
        Assert.False(result.ContainsKey("FORCE_COLOR"));
        Assert.False(result.ContainsKey("CLICOLOR_FORCE"));
        Assert.Equal("xterm-256color", result["TERM"]);
        Assert.Equal("truecolor", result["COLORTERM"]);
        Assert.Equal("/usr/bin", result["PATH"]);
        Assert.Equal("/home/user", result["HOME"]);
    }

    [Fact]
    public void EnvPolicy_SubstitutesVersion()
    {
        var policy = new EnvPolicy
        {
            Set = new Dictionary<string, string>
            {
                ["TERM_PROGRAM"] = "YOLOTerm",
                ["TERM_PROGRAM_VERSION"] = "<app version placeholder>"
            },
            Remove = new List<string>()
        };

        var result = policy.Apply(new Dictionary<string, string>(), "1.2.3");

        Assert.Equal("YOLOTerm", result["TERM_PROGRAM"]);
        Assert.Equal("1.2.3", result["TERM_PROGRAM_VERSION"]);
    }

    [Fact]
    public void EnvPolicy_LoadsFromJson()
    {
        var fixturesPath = FindEnvPolicyFixture();
        if (fixturesPath == null)
        {
            Assert.True(false, "Could not find env-policy.json fixture");
            return;
        }

        var policy = EnvPolicy.Load(fixturesPath);

        Assert.NotNull(policy);
        Assert.Contains("TERM", policy.Set.Keys);
        Assert.Contains("COLORTERM", policy.Set.Keys);
        Assert.Contains("TERM_PROGRAM", policy.Set.Keys);
        Assert.Contains("NO_COLOR", policy.Remove);
        Assert.Contains("FORCE_COLOR", policy.Remove);
    }

    private string? FindEnvPolicyFixture()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var dir = new DirectoryInfo(currentDir);

        while (dir != null)
        {
            var contractsPath = Path.Combine(dir.FullName, "contracts", "fixtures", "env-policy.json");
            if (File.Exists(contractsPath))
                return contractsPath;

            dir = dir.Parent;
        }

        return null;
    }
}
