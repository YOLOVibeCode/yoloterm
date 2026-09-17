using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Xunit;
using YOLOTerm.Core.Persistence;

namespace YOLOTerm.Tests.Persistence;

public class PromptMarkParserTests
{
    [Fact]
    public void Feed_OSC133A_EmitsPromptStart()
    {
        // Arrange
        var parser = new PromptMarkParser();
        byte[] data = Encoding.UTF8.GetBytes("\x1b]133;A\x07");
        
        // Act
        var events = parser.Feed(data);
        
        // Assert
        Assert.Single(events);
        Assert.Equal(PromptMarkParser.Event.PromptStart, events[0].EventType);
        Assert.True(parser.HasShellPlugin);
    }
    
    [Fact]
    public void Feed_OSC133B_EmitsPromptEnd()
    {
        // Arrange
        var parser = new PromptMarkParser();
        byte[] data = Encoding.UTF8.GetBytes("\x1b]133;B\x07");
        
        // Act
        var events = parser.Feed(data);
        
        // Assert
        Assert.Single(events);
        Assert.Equal(PromptMarkParser.Event.PromptEnd, events[0].EventType);
    }
    
    [Fact]
    public void Feed_OSC133C_EmitsCommandStart()
    {
        // Arrange
        var parser = new PromptMarkParser();
        
        // Simulate B (prompt end) → command typing → C (command start)
        parser.Feed(Encoding.UTF8.GetBytes("\x1b]133;B\x07"));
        parser.Feed(Encoding.UTF8.GetBytes("git status"));
        
        // Act
        var events = parser.Feed(Encoding.UTF8.GetBytes("\x1b]133;C\x07"));
        
        // Assert
        var commandStartEvent = events.FirstOrDefault(e => e.EventType == PromptMarkParser.Event.CommandStart);
        Assert.NotNull(commandStartEvent);
        Assert.Equal("git status", commandStartEvent.Command);
    }
    
    [Fact]
    public void Feed_OSC133D_EmitsCommandEnd()
    {
        // Arrange
        var parser = new PromptMarkParser();
        byte[] data = Encoding.UTF8.GetBytes("\x1b]133;D;0;1234\x07");
        
        // Act
        var events = parser.Feed(data);
        
        // Assert
        Assert.Single(events);
        Assert.Equal(PromptMarkParser.Event.CommandEnd, events[0].EventType);
        Assert.Equal(0, events[0].ExitCode);
        Assert.Equal(1234, events[0].DurationMs);
    }
    
    [Fact]
    public void Feed_OSC7_EmitsCwdChanged()
    {
        // Arrange
        var parser = new PromptMarkParser();
        byte[] data = Encoding.UTF8.GetBytes("\x1b]7;file://hostname/home/user/projects\x07");
        
        // Act
        var events = parser.Feed(data);
        
        // Assert
        Assert.Single(events);
        Assert.Equal(PromptMarkParser.Event.CwdChanged, events[0].EventType);
        Assert.Equal("/home/user/projects", events[0].CwdPath);
    }
    
    [Fact]
    public void Feed_FullCommandSequence_EmitsAllEvents()
    {
        // Arrange
        var parser = new PromptMarkParser();
        var allEvents = new List<PromptMarkParser.EventData>();
        
        // Act - Simulate a full command lifecycle
        allEvents.AddRange(parser.Feed(Encoding.UTF8.GetBytes("\x1b]133;A\x07"))); // Prompt start
        allEvents.AddRange(parser.Feed(Encoding.UTF8.GetBytes("\x1b]133;B\x07"))); // Prompt end
        allEvents.AddRange(parser.Feed(Encoding.UTF8.GetBytes("echo hello")));     // Command typing
        allEvents.AddRange(parser.Feed(Encoding.UTF8.GetBytes("\x1b]133;C\x07"))); // Command start
        allEvents.AddRange(parser.Feed(Encoding.UTF8.GetBytes("\x1b]133;D;0;42\x07"))); // Command end
        
        // Assert
        Assert.Equal(4, allEvents.Count);
        
        Assert.Equal(PromptMarkParser.Event.PromptStart, allEvents[0].EventType);
        Assert.Equal(PromptMarkParser.Event.PromptEnd, allEvents[1].EventType);
        
        Assert.Equal(PromptMarkParser.Event.CommandStart, allEvents[2].EventType);
        Assert.Equal("echo hello", allEvents[2].Command);
        
        Assert.Equal(PromptMarkParser.Event.CommandEnd, allEvents[3].EventType);
        Assert.Equal(0, allEvents[3].ExitCode);
        Assert.Equal(42, allEvents[3].DurationMs);
    }
    
    [Fact]
    public void Feed_WithESCBackslash_TerminatesCorrectly()
    {
        // Arrange
        var parser = new PromptMarkParser();
        
        // OSC 7 with ESC \ terminator instead of BEL
        byte[] data = Encoding.UTF8.GetBytes("\x1b]7;file://hostname/tmp\x1b\\");
        
        // Act
        var events = parser.Feed(data);
        
        // Assert
        Assert.Single(events);
        Assert.Equal(PromptMarkParser.Event.CwdChanged, events[0].EventType);
        Assert.Equal("/tmp", events[0].CwdPath);
    }
    
    [Fact]
    public void Reset_ClearsState()
    {
        // Arrange
        var parser = new PromptMarkParser();
        parser.Feed(Encoding.UTF8.GetBytes("\x1b]133;A\x07"));
        parser.Feed(Encoding.UTF8.GetBytes("\x1b]133;B\x07"));
        parser.Feed(Encoding.UTF8.GetBytes("partial command"));
        
        // Act
        parser.Reset();
        var events = parser.Feed(Encoding.UTF8.GetBytes("\x1b]133;C\x07"));
        
        // Assert - Command should be empty since we reset
        var commandEvent = events.FirstOrDefault(e => e.EventType == PromptMarkParser.Event.CommandStart);
        Assert.NotNull(commandEvent);
        Assert.Empty(commandEvent.Command ?? "");
    }
    
    [Fact]
    public void ExtractCommandHeuristic_RemovesPromptPattern()
    {
        // Arrange
        string line = "user@hostname:~/projects$ git status";
        
        // Act
        string? command = PromptMarkParser.ExtractCommandHeuristic(line);
        
        // Assert
        Assert.Equal("git status", command);
    }
    
    [Fact]
    public void ExtractCommandHeuristic_HandlesStarshipPrompt()
    {
        // Arrange
        string line = "❯ npm install";
        
        // Act
        string? command = PromptMarkParser.ExtractCommandHeuristic(line);
        
        // Assert
        Assert.Equal("npm install", command);
    }
}
