using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Xml.Linq;

namespace YOLOTerm.Core.Theme;

/// <summary>
/// Theme format types supported for import
/// </summary>
public enum ThemeFormat
{
    ITerm2,
    WindowsTerminal,
    Ghostty
}

/// <summary>
/// Imports themes from various terminal emulator formats.
/// Supports: iTerm2 (.itermcolors), Windows Terminal (JSON), Ghostty (config).
/// </summary>
public class ThemeImporter
{
    /// <summary>
    /// Detects the theme format from a file URL.
    /// </summary>
    public static ThemeFormat? DetectFormat(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        
        if (ext == ".itermcolors")
        {
            return ThemeFormat.ITerm2;
        }
        
        if (ext == ".json")
        {
            // Try to parse and detect format
            try
            {
                var json = File.ReadAllText(filePath);
                var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                
                // Windows Terminal has "schemes" array
                if (root.TryGetProperty("schemes", out _))
                {
                    return ThemeFormat.WindowsTerminal;
                }
                
                // Ghostty or YOLOTerm format
                if (root.TryGetProperty("background", out _) && root.TryGetProperty("foreground", out _))
                {
                    return ThemeFormat.Ghostty;
                }
            }
            catch
            {
                return null;
            }
        }
        
        // Ghostty text format
        if (string.IsNullOrEmpty(ext) || ext == ".conf")
        {
            return ThemeFormat.Ghostty;
        }
        
        return null;
    }
    
    /// <summary>
    /// Imports a theme from a file.
    /// </summary>
    public Core.Theme.Theme ImportTheme(string filePath)
    {
        var format = DetectFormat(filePath);
        if (!format.HasValue)
        {
            throw new ThemeImportException("Unsupported theme format");
        }
        
        return format.Value switch
        {
            ThemeFormat.ITerm2 => ImportITerm2(filePath),
            ThemeFormat.WindowsTerminal => ImportWindowsTerminal(filePath),
            ThemeFormat.Ghostty => ImportGhostty(filePath),
            _ => throw new ThemeImportException("Unsupported format")
        };
    }
    
    // MARK: - iTerm2 Import
    
    private Core.Theme.Theme ImportITerm2(string filePath)
    {
        var doc = XDocument.Load(filePath);
        var dict = doc.Root?.Element("dict");
        if (dict == null)
        {
            throw new ThemeImportException("Invalid iTerm2 theme format");
        }
        
        var colors = ParsePlistDict(dict);
        
        string? ExtractColor(string key)
        {
            if (!colors.TryGetValue(key, out var colorDict))
            {
                return null;
            }
            
            if (colorDict is not Dictionary<string, object> cd)
            {
                return null;
            }
            
            if (!cd.TryGetValue("Red Component", out var r) ||
                !cd.TryGetValue("Green Component", out var g) ||
                !cd.TryGetValue("Blue Component", out var b))
            {
                return null;
            }
            
            var red = Convert.ToInt32(Convert.ToDouble(r) * 255);
            var green = Convert.ToInt32(Convert.ToDouble(g) * 255);
            var blue = Convert.ToInt32(Convert.ToDouble(b) * 255);
            
            return $"#{red:x2}{green:x2}{blue:x2}";
        }
        
        var name = Path.GetFileNameWithoutExtension(filePath);
        var id = name.ToLowerInvariant().Replace(" ", "-");
        
        var background = ExtractColor("Background Color") ?? throw new ThemeImportException("Missing background color");
        var foreground = ExtractColor("Foreground Color") ?? throw new ThemeImportException("Missing foreground color");
        var cursor = ExtractColor("Cursor Color") ?? foreground;
        var selection = ExtractColor("Selection Color") ?? "#44475a";
        
        return new Core.Theme.Theme
        {
            Id = id,
            Name = name,
            Description = "Imported from iTerm2",
            Background = background,
            Foreground = foreground,
            Cursor = cursor,
            CursorAccent = background,
            SelectionBackground = selection,
            UseDefaultAnsi = null,
            AnsiColors = new()
            {
                Black = ExtractColor("Ansi 0 Color") ?? "#000000",
                Red = ExtractColor("Ansi 1 Color") ?? "#cc0000",
                Green = ExtractColor("Ansi 2 Color") ?? "#00cc00",
                Yellow = ExtractColor("Ansi 3 Color") ?? "#cccc00",
                Blue = ExtractColor("Ansi 4 Color") ?? "#0000cc",
                Magenta = ExtractColor("Ansi 5 Color") ?? "#cc00cc",
                Cyan = ExtractColor("Ansi 6 Color") ?? "#00cccc",
                White = ExtractColor("Ansi 7 Color") ?? "#cccccc",
                BrightBlack = ExtractColor("Ansi 8 Color") ?? "#555555",
                BrightRed = ExtractColor("Ansi 9 Color") ?? "#ff5555",
                BrightGreen = ExtractColor("Ansi 10 Color") ?? "#55ff55",
                BrightYellow = ExtractColor("Ansi 11 Color") ?? "#ffff55",
                BrightBlue = ExtractColor("Ansi 12 Color") ?? "#5555ff",
                BrightMagenta = ExtractColor("Ansi 13 Color") ?? "#ff55ff",
                BrightCyan = ExtractColor("Ansi 14 Color") ?? "#55ffff",
                BrightWhite = ExtractColor("Ansi 15 Color") ?? "#ffffff"
            }
        };
    }
    
    private Dictionary<string, object> ParsePlistDict(XElement dict)
    {
        var result = new Dictionary<string, object>();
        var elements = dict.Elements().ToList();
        
        for (int i = 0; i < elements.Count - 1; i += 2)
        {
            if (elements[i].Name == "key")
            {
                var key = elements[i].Value;
                var valueElement = elements[i + 1];
                
                result[key] = valueElement.Name.LocalName switch
                {
                    "dict" => ParsePlistDict(valueElement),
                    "string" => valueElement.Value,
                    "real" => double.Parse(valueElement.Value, CultureInfo.InvariantCulture),
                    "integer" => int.Parse(valueElement.Value, CultureInfo.InvariantCulture),
                    _ => valueElement.Value
                };
            }
        }
        
        return result;
    }
    
    // MARK: - Windows Terminal Import
    
    private Core.Theme.Theme ImportWindowsTerminal(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        if (!root.TryGetProperty("schemes", out var schemes) || schemes.GetArrayLength() == 0)
        {
            throw new ThemeImportException("No schemes found in Windows Terminal theme");
        }
        
        var scheme = schemes[0];
        return ParseWindowsTerminalScheme(scheme);
    }
    
    private Core.Theme.Theme ParseWindowsTerminalScheme(JsonElement scheme)
    {
        if (!scheme.TryGetProperty("name", out var nameElem) ||
            !scheme.TryGetProperty("background", out var bgElem) ||
            !scheme.TryGetProperty("foreground", out var fgElem))
        {
            throw new ThemeImportException("Missing required colors in Windows Terminal scheme");
        }
        
        var name = nameElem.GetString() ?? "Unnamed";
        var id = name.ToLowerInvariant().Replace(" ", "-");
        var background = bgElem.GetString() ?? "#000000";
        var foreground = fgElem.GetString() ?? "#ffffff";
        
        var cursor = scheme.TryGetProperty("cursorColor", out var cursorElem) 
            ? cursorElem.GetString() ?? foreground 
            : foreground;
            
        var selection = scheme.TryGetProperty("selectionBackground", out var selElem)
            ? selElem.GetString() ?? "#44475a"
            : "#44475a";
        
        return new Core.Theme.Theme
        {
            Id = id,
            Name = name,
            Description = "Imported from Windows Terminal",
            Background = background,
            Foreground = foreground,
            Cursor = cursor,
            CursorAccent = background,
            SelectionBackground = selection,
            UseDefaultAnsi = null,
            AnsiColors = new()
            {
                Black = GetProperty(scheme, "black") ?? "#000000",
                Red = GetProperty(scheme, "red") ?? "#cc0000",
                Green = GetProperty(scheme, "green") ?? "#00cc00",
                Yellow = GetProperty(scheme, "yellow") ?? "#cccc00",
                Blue = GetProperty(scheme, "blue") ?? "#0000cc",
                Magenta = GetProperty(scheme, "purple") ?? "#cc00cc",
                Cyan = GetProperty(scheme, "cyan") ?? "#00cccc",
                White = GetProperty(scheme, "white") ?? "#cccccc",
                BrightBlack = GetProperty(scheme, "brightBlack") ?? "#555555",
                BrightRed = GetProperty(scheme, "brightRed") ?? "#ff5555",
                BrightGreen = GetProperty(scheme, "brightGreen") ?? "#55ff55",
                BrightYellow = GetProperty(scheme, "brightYellow") ?? "#ffff55",
                BrightBlue = GetProperty(scheme, "brightBlue") ?? "#5555ff",
                BrightMagenta = GetProperty(scheme, "brightPurple") ?? "#ff55ff",
                BrightCyan = GetProperty(scheme, "brightCyan") ?? "#55ffff",
                BrightWhite = GetProperty(scheme, "brightWhite") ?? "#ffffff"
            }
        };
    }
    
    private string? GetProperty(JsonElement element, string name)
    {
        return element.TryGetProperty(name, out var prop) ? prop.GetString() : null;
    }
    
    // MARK: - Ghostty Import
    
    private Core.Theme.Theme ImportGhostty(string filePath)
    {
        var content = File.ReadAllText(filePath);
        var config = new Dictionary<string, string>();
        
        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
            {
                continue;
            }
            
            var parts = trimmed.Split('=', 2);
            if (parts.Length == 2)
            {
                config[parts[0].Trim()] = parts[1].Trim();
            }
        }
        
        string? ConvertColor(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }
            
            // Ghostty format: 0xRRGGBB or #RRGGBB or rgb(r, g, b)
            if (value.StartsWith("0x"))
            {
                return "#" + value.Substring(2);
            }
            else if (value.StartsWith("#"))
            {
                return value;
            }
            else if (value.StartsWith("rgb("))
            {
                // Parse rgb(r, g, b)
                var rgb = value.Substring(4, value.Length - 5);
                var components = rgb.Split(',').Select(s => int.Parse(s.Trim())).ToArray();
                if (components.Length == 3)
                {
                    return $"#{components[0]:x2}{components[1]:x2}{components[2]:x2}";
                }
            }
            
            return null;
        }
        
        var name = Path.GetFileNameWithoutExtension(filePath);
        var id = name.ToLowerInvariant().Replace(" ", "-");
        
        var background = ConvertColor(config.GetValueOrDefault("background"))
            ?? throw new ThemeImportException("Missing background color");
        var foreground = ConvertColor(config.GetValueOrDefault("foreground"))
            ?? throw new ThemeImportException("Missing foreground color");
        var cursor = ConvertColor(config.GetValueOrDefault("cursor-color")) ?? foreground;
        var selection = ConvertColor(config.GetValueOrDefault("selection-background")) ?? "#44475a";
        
        return new Core.Theme.Theme
        {
            Id = id,
            Name = name,
            Description = "Imported from Ghostty",
            Background = background,
            Foreground = foreground,
            Cursor = cursor,
            CursorAccent = background,
            SelectionBackground = selection,
            UseDefaultAnsi = null,
            AnsiColors = new()
            {
                Black = ConvertColor(config.GetValueOrDefault("palette") ?? config.GetValueOrDefault("color0")) ?? "#000000",
                Red = ConvertColor(config.GetValueOrDefault("color1")) ?? "#cc0000",
                Green = ConvertColor(config.GetValueOrDefault("color2")) ?? "#00cc00",
                Yellow = ConvertColor(config.GetValueOrDefault("color3")) ?? "#cccc00",
                Blue = ConvertColor(config.GetValueOrDefault("color4")) ?? "#0000cc",
                Magenta = ConvertColor(config.GetValueOrDefault("color5")) ?? "#cc00cc",
                Cyan = ConvertColor(config.GetValueOrDefault("color6")) ?? "#00cccc",
                White = ConvertColor(config.GetValueOrDefault("color7")) ?? "#cccccc",
                BrightBlack = ConvertColor(config.GetValueOrDefault("color8")) ?? "#555555",
                BrightRed = ConvertColor(config.GetValueOrDefault("color9")) ?? "#ff5555",
                BrightGreen = ConvertColor(config.GetValueOrDefault("color10")) ?? "#55ff55",
                BrightYellow = ConvertColor(config.GetValueOrDefault("color11")) ?? "#ffff55",
                BrightBlue = ConvertColor(config.GetValueOrDefault("color12")) ?? "#5555ff",
                BrightMagenta = ConvertColor(config.GetValueOrDefault("color13")) ?? "#ff55ff",
                BrightCyan = ConvertColor(config.GetValueOrDefault("color14")) ?? "#55ffff",
                BrightWhite = ConvertColor(config.GetValueOrDefault("color15")) ?? "#ffffff"
            }
        };
    }
    
    /// <summary>
    /// Saves a theme to the contracts/themes/ directory.
    /// </summary>
    public void SaveTheme(Core.Theme.Theme theme, string contractsThemesDir)
    {
        var filename = $"{theme.Id}.json";
        var filePath = Path.Combine(contractsThemesDir, filename);
        
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        
        var json = JsonSerializer.Serialize(theme, options);
        File.WriteAllText(filePath, json);
    }
}

public class ThemeImportException : Exception
{
    public ThemeImportException(string message) : base(message) { }
    public ThemeImportException(string message, Exception inner) : base(message, inner) { }
}
