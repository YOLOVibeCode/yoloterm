using YOLOTerm.Core.Contracts;
using YOLOTerm.Core.Pty;

namespace YOLOTerm.Tests;

/// <summary>
/// Tests for PTY lifecycle (B1.3 validation)
/// </summary>
public class PtyLifecycleTests
{
    [Fact(Skip = "Requires pty-probe.exe built and accessible")]
    public async Task PtySession_NaturalExit_FiresOnExitExactlyOnce()
    {
        var probePath = FindPtyProbe();
        if (probePath == null)
            return;

        var spawner = new ConPtySpawner();
        var session = spawner.Spawn(probePath, Directory.GetCurrentDirectory(), 80, 24, new Dictionary<string, string>());

        var exitCount = 0;
        var exitCode = -1;
        var exitEvent = new ManualResetEventSlim(false);

        session.OnExit += code =>
        {
            exitCount++;
            exitCode = code;
            exitEvent.Set();
        };

        await Task.Delay(100);
        session.Write(System.Text.Encoding.UTF8.GetBytes("exit\n"));

        var signaled = exitEvent.Wait(5000);
        Assert.True(signaled, "OnExit should fire within 5 seconds");
        
        await Task.Delay(500);
        
        Assert.Equal(1, exitCount);
        Assert.Equal(0, exitCode);
    }

    [Fact(Skip = "Requires pty-probe.exe built and accessible")]
    public async Task PtySession_Kill_FiresOnExitExactlyOnce()
    {
        var probePath = FindPtyProbe();
        if (probePath == null)
            return;

        var spawner = new ConPtySpawner();
        var session = spawner.Spawn(probePath, Directory.GetCurrentDirectory(), 80, 24, new Dictionary<string, string>());

        var exitCount = 0;
        var exitEvent = new ManualResetEventSlim(false);

        session.OnExit += code =>
        {
            exitCount++;
            exitEvent.Set();
        };

        await Task.Delay(100);
        Assert.True(session.IsAlive);
        
        session.Kill();

        var signaled = exitEvent.Wait(5000);
        Assert.True(signaled, "OnExit should fire within 5 seconds after kill");
        
        await Task.Delay(500);
        
        Assert.Equal(1, exitCount);
        Assert.False(session.IsAlive);
    }

    private string? FindPtyProbe()
    {
        var candidates = new[]
        {
            Path.Combine(Directory.GetCurrentDirectory(), "pty-probe.exe"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "pty-probe", "bin", "Debug", "net9.0", "pty-probe.exe"),
            Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "pty-probe", "bin", "Release", "net9.0", "pty-probe.exe")
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
                return Path.GetFullPath(candidate);
        }

        return null;
    }
}
