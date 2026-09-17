using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace YOLOTerm.Core.Persistence;

/// <summary>
/// PromptMarkParser is a state machine for parsing OSC 133 (prompt marks) and OSC 7 (CWD)
/// escape sequences from terminal output.
/// 
/// OSC 133 zones:
/// - A: prompt start
/// - B: prompt end / command start
/// - C: command executed (pre-exec)
/// - D: command finished (with exit code and optional duration)
/// 
/// OSC 7: file://hostname/path (current working directory)
/// 
/// This parser is used by both:
/// - HistoryStore: to capture command boundaries and metadata
/// - PaneMetadataProvider: to update pane labels with current CWD
/// 
/// Heuristic fallback: If shell plugins are not installed, we fall back to
/// pattern matching on common shell prompts (limited accuracy).
/// </summary>
public struct PromptMarkParser
{
    // MARK: - Public Models
    
    public enum Event
    {
        PromptStart,
        PromptEnd,
        CommandStart,
        CommandEnd,
        CwdChanged
    }
    
    public sealed class EventData
    {
        public Event EventType { get; set; }
        public string? Command { get; set; }
        public int? ExitCode { get; set; }
        public int? DurationMs { get; set; }
        public string? CwdPath { get; set; }
    }
    
    // MARK: - State
    
    private enum State
    {
        Normal,
        Escape,
        Osc,
        OscPayload,
        CollectingCommand
    }
    
    private State state;
    private List<byte> buffer;
    private int oscNumber;
    
    /// <summary>
    /// Accumulated command text between zones B (command start) and C (command exec)
    /// </summary>
    private StringBuilder commandBuffer;
    
    /// <summary>
    /// Whether we've seen any OSC 133 marks (indicates shell plugin is active)
    /// </summary>
    public bool HasShellPlugin { get; private set; }
    
    // MARK: - Public API
    
    public PromptMarkParser()
    {
        state = State.Normal;
        buffer = new List<byte>();
        oscNumber = 0;
        commandBuffer = new StringBuilder();
        HasShellPlugin = false;
    }
    
    /// <summary>
    /// Feed raw terminal output bytes to the parser.
    /// Returns events as they are recognized.
    /// </summary>
    public List<EventData> Feed(byte[] data)
    {
        List<EventData> events = new();
        
        foreach (byte b in data)
        {
            events.AddRange(ProcessByte(b));
        }
        
        return events;
    }
    
    /// <summary>
    /// Feed raw terminal output bytes to the parser.
    /// Returns events as they are recognized.
    /// </summary>
    public List<EventData> Feed(ReadOnlySpan<byte> data)
    {
        List<EventData> events = new();
        
        foreach (byte b in data)
        {
            events.AddRange(ProcessByte(b));
        }
        
        return events;
    }
    
    /// <summary>
    /// Reset the parser state (e.g., when switching panes).
    /// </summary>
    public void Reset()
    {
        state = State.Normal;
        buffer.Clear();
        oscNumber = 0;
        commandBuffer.Clear();
        // Note: Don't reset HasShellPlugin - it's a detection flag that persists
    }
    
    // MARK: - State Machine
    
    private List<EventData> ProcessByte(byte b)
    {
        return state switch
        {
            State.Normal or State.CollectingCommand => ProcessNormalByte(b),
            State.Escape => ProcessEscapeByte(b),
            State.Osc => ProcessOscByte(b),
            State.OscPayload => ProcessOscPayloadByte(b),
            _ => new List<EventData>()
        };
    }
    
    private List<EventData> ProcessNormalByte(byte b)
    {
        if (b == 0x1B) // ESC
        {
            state = State.Escape;
            buffer = new List<byte> { b };
            return new List<EventData>();
        }
        
        // Accumulate command characters if we're in command collection mode
        if (state == State.CollectingCommand)
        {
            // Collect printable characters
            if (b >= 0x20 && b <= 0x7E)
            {
                commandBuffer.Append((char)b);
            }
        }
        
        return new List<EventData>();
    }
    
    private List<EventData> ProcessEscapeByte(byte b)
    {
        buffer.Add(b);
        
        if (b == 0x5D) // ESC ] → OSC
        {
            state = State.Osc;
            return new List<EventData>();
        }
        
        // Not an OSC sequence, reset
        state = State.Normal;
        buffer.Clear();
        return new List<EventData>();
    }
    
    private List<EventData> ProcessOscByte(byte b)
    {
        // OSC format: ESC ] <number> ; <payload> ST
        // ST can be BEL (0x07) or ESC \ (0x1B 0x5C)
        
        if (b == 0x3B) // semicolon
        {
            // Extract OSC number (before adding semicolon to buffer)
            byte[] numberBytes = buffer.Skip(2).ToArray(); // skip ESC ]
            string numberString = Encoding.ASCII.GetString(numberBytes);
            if (int.TryParse(numberString, out int num))
            {
                oscNumber = num;
                state = State.OscPayload;
                buffer.Clear();
                return new List<EventData>();
            }
        }
        
        buffer.Add(b);
        
        // Continue accumulating OSC number
        return new List<EventData>();
    }
    
    private List<EventData> ProcessOscPayloadByte(byte b)
    {
        // Check for string terminator
        if (b == 0x07) // BEL
        {
            return FinalizeOsc();
        }
        
        if (b == 0x5C && buffer.Count > 0 && buffer[^1] == 0x1B) // ESC \
        {
            buffer.RemoveAt(buffer.Count - 1); // remove ESC
            return FinalizeOsc();
        }
        
        buffer.Add(b);
        return new List<EventData>();
    }
    
    private List<EventData> FinalizeOsc()
    {
        List<EventData> events;
        
        string payload = Encoding.UTF8.GetString(buffer.ToArray());
        
        events = oscNumber switch
        {
            7 => ParseOsc7(payload),
            133 => ParseOsc133(payload),
            _ => new List<EventData>()
        };
        
        state = State.Normal;
        buffer.Clear();
        oscNumber = 0;
        
        return events;
    }
    
    // MARK: - OSC Parsers
    
    private List<EventData> ParseOsc7(string payload)
    {
        // OSC 7: file://hostname/path
        if (!payload.StartsWith("file://"))
        {
            return new List<EventData>();
        }
        
        // Extract path (skip hostname)
        int pathStart = payload.IndexOf('/', 7);
        if (pathStart == -1)
        {
            return new List<EventData>();
        }
        
        string path = payload.Substring(pathStart);
        
        // URL decode
        string decoded = Uri.UnescapeDataString(path);
        
        return new List<EventData>
        {
            new EventData
            {
                EventType = Event.CwdChanged,
                CwdPath = decoded
            }
        };
    }
    
    private List<EventData> ParseOsc133(string payload)
    {
        HasShellPlugin = true;
        
        // OSC 133 format: <zone>[;<key>=<value>]*
        string[] parts = payload.Split(';', 2);
        if (parts.Length == 0)
        {
            return new List<EventData>();
        }
        
        string zone = parts[0];
        Dictionary<string, string> parameters = parts.Length > 1
            ? ParseKeyValuePairs(parts[1])
            : new Dictionary<string, string>();
        
        return zone switch
        {
            "A" => new List<EventData>
            {
                new EventData { EventType = Event.PromptStart }
            },
            
            "B" => HandleZoneB(),
            
            "C" => HandleZoneC(),
            
            "D" => HandleZoneD(parameters),
            
            _ => new List<EventData>()
        };
    }
    
    private List<EventData> HandleZoneB()
    {
        commandBuffer.Clear();
        state = State.CollectingCommand;
        return new List<EventData>
        {
            new EventData { EventType = Event.PromptEnd }
        };
    }
    
    private List<EventData> HandleZoneC()
    {
        string command = commandBuffer.ToString().Trim();
        commandBuffer.Clear();
        state = State.Normal;
        
        return new List<EventData>
        {
            new EventData
            {
                EventType = Event.CommandStart,
                Command = command
            }
        };
    }
    
    private List<EventData> HandleZoneD(Dictionary<string, string> parameters)
    {
        state = State.Normal;
        
        int? exitCode = null;
        if (parameters.TryGetValue("exitCode", out string? exitCodeStr) &&
            int.TryParse(exitCodeStr, out int ec))
        {
            exitCode = ec;
        }
        
        int? durationMs = null;
        if (parameters.TryGetValue("duration", out string? durationStr) &&
            int.TryParse(durationStr, out int dur))
        {
            durationMs = dur;
        }
        
        return new List<EventData>
        {
            new EventData
            {
                EventType = Event.CommandEnd,
                ExitCode = exitCode,
                DurationMs = durationMs
            }
        };
    }
    
    private static Dictionary<string, string> ParseKeyValuePairs(string input)
    {
        Dictionary<string, string> result = new();
        
        string[] pairs = input.Split(';');
        foreach (string pair in pairs)
        {
            string[] kv = pair.Split('=', 2);
            if (kv.Length == 2)
            {
                result[kv[0]] = kv[1];
            }
        }
        
        return result;
    }
    
    // MARK: - Heuristic Fallback
    
    /// <summary>
    /// Heuristic command detection for shells without plugin support.
    /// This is much less accurate but provides some functionality.
    /// </summary>
    public static string? ExtractCommandHeuristic(string line)
    {
        // Strip common prompt patterns
        string[] patterns = new[]
        {
            @"^\w+@[\w\-]+:~?[^\$#]*[\$#]\s*",  // user@host:path$
            @"^❯\s*",                             // Starship prompt
            @"^➜\s*",                             // Oh My Zsh prompt
            @"^[▶►]\s*"                           // Various custom prompts
        };
        
        string cleaned = line;
        foreach (string pattern in patterns)
        {
            try
            {
                Regex regex = new(pattern, RegexOptions.None, TimeSpan.FromMilliseconds(100));
                Match match = regex.Match(cleaned);
                if (match.Success)
                {
                    cleaned = cleaned.Substring(match.Length);
                }
            }
            catch (RegexMatchTimeoutException)
            {
                // Continue with next pattern
            }
        }
        
        string trimmed = cleaned.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
