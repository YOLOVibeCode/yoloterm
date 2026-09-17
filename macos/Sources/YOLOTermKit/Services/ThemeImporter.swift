import Foundation

/// Theme format types supported for import
public enum ThemeFormat {
    case iTerm2
    case windowsTerminal
    case ghostty
    
    static func detect(from url: URL) -> ThemeFormat? {
        let ext = url.pathExtension.lowercased()
        if ext == "itermcolors" {
            return .iTerm2
        }
        
        if ext == "json" {
            // Try to parse and detect format
            guard let data = try? Data(contentsOf: url),
                  let json = try? JSONSerialization.jsonObject(with: data) as? [String: Any] else {
                return nil
            }
            
            // Windows Terminal has "schemes" array
            if json["schemes"] != nil {
                return .windowsTerminal
            }
            
            // Ghostty themes have specific keys
            if json["background"] != nil && json["foreground"] != nil {
                // Could be Ghostty or already YOLOTerm format
                // Ghostty uses different format (e.g., "background = 0x...")
                return .ghostty
            }
        }
        
        // Check for Ghostty text format
        if ext.isEmpty || ext == "conf" {
            return .ghostty
        }
        
        return nil
    }
}

/// YOLOTerm theme structure
public struct YOLOTermTheme: Codable {
    public let id: String
    public let name: String
    public let description: String?
    public let background: String
    public let foreground: String
    public let cursor: String
    public let cursorAccent: String
    public let selectionBackground: String
    public let useDefaultAnsi: Bool?
    public let ansiColors: AnsiColors?
    
    public struct AnsiColors: Codable {
        public let black: String
        public let red: String
        public let green: String
        public let yellow: String
        public let blue: String
        public let magenta: String
        public let cyan: String
        public let white: String
        public let brightBlack: String
        public let brightRed: String
        public let brightGreen: String
        public let brightYellow: String
        public let brightBlue: String
        public let brightMagenta: String
        public let brightCyan: String
        public let brightWhite: String
    }
}

/// Imports themes from various terminal emulator formats
public struct ThemeImporter {
    
    public init() {}
    
    public func importTheme(from url: URL) throws -> YOLOTermTheme {
        guard let format = ThemeFormat.detect(from: url) else {
            throw ThemeImportError.unsupportedFormat
        }
        
        switch format {
        case .iTerm2:
            return try importITerm2(from: url)
        case .windowsTerminal:
            return try importWindowsTerminal(from: url)
        case .ghostty:
            return try importGhostty(from: url)
        }
    }
    
    // MARK: - iTerm2 Import
    
    private func importITerm2(from url: URL) throws -> YOLOTermTheme {
        let data = try Data(contentsOf: url)
        guard let plist = try PropertyListSerialization.propertyList(from: data, format: nil) as? [String: Any] else {
            throw ThemeImportError.invalidFormat
        }
        
        func extractColor(_ key: String) -> String? {
            guard let colorDict = plist[key] as? [String: Any],
                  let red = colorDict["Red Component"] as? Double,
                  let green = colorDict["Green Component"] as? Double,
                  let blue = colorDict["Blue Component"] as? Double else {
                return nil
            }
            
            let r = Int(red * 255)
            let g = Int(green * 255)
            let b = Int(blue * 255)
            return String(format: "#%02x%02x%02x", r, g, b)
        }
        
        let name = url.deletingPathExtension().lastPathComponent
        let id = name.lowercased().replacingOccurrences(of: " ", with: "-")
        
        guard let background = extractColor("Background Color"),
              let foreground = extractColor("Foreground Color"),
              let cursor = extractColor("Cursor Color") ?? extractColor("Foreground Color"),
              let selection = extractColor("Selection Color") else {
            throw ThemeImportError.missingRequiredColors
        }
        
        let ansiColors = YOLOTermTheme.AnsiColors(
            black: extractColor("Ansi 0 Color") ?? "#000000",
            red: extractColor("Ansi 1 Color") ?? "#cc0000",
            green: extractColor("Ansi 2 Color") ?? "#00cc00",
            yellow: extractColor("Ansi 3 Color") ?? "#cccc00",
            blue: extractColor("Ansi 4 Color") ?? "#0000cc",
            magenta: extractColor("Ansi 5 Color") ?? "#cc00cc",
            cyan: extractColor("Ansi 6 Color") ?? "#00cccc",
            white: extractColor("Ansi 7 Color") ?? "#cccccc",
            brightBlack: extractColor("Ansi 8 Color") ?? "#555555",
            brightRed: extractColor("Ansi 9 Color") ?? "#ff5555",
            brightGreen: extractColor("Ansi 10 Color") ?? "#55ff55",
            brightYellow: extractColor("Ansi 11 Color") ?? "#ffff55",
            brightBlue: extractColor("Ansi 12 Color") ?? "#5555ff",
            brightMagenta: extractColor("Ansi 13 Color") ?? "#ff55ff",
            brightCyan: extractColor("Ansi 14 Color") ?? "#55ffff",
            brightWhite: extractColor("Ansi 15 Color") ?? "#ffffff"
        )
        
        return YOLOTermTheme(
            id: id,
            name: name,
            description: "Imported from iTerm2",
            background: background,
            foreground: foreground,
            cursor: cursor,
            cursorAccent: background,
            selectionBackground: selection,
            useDefaultAnsi: nil,
            ansiColors: ansiColors
        )
    }
    
    // MARK: - Windows Terminal Import
    
    private func importWindowsTerminal(from url: URL) throws -> YOLOTermTheme {
        let data = try Data(contentsOf: url)
        guard let json = try JSONSerialization.jsonObject(with: data) as? [String: Any] else {
            throw ThemeImportError.invalidFormat
        }
        
        // Windows Terminal settings.json has a "schemes" array
        guard let schemes = json["schemes"] as? [[String: Any]],
              let scheme = schemes.first else {
            throw ThemeImportError.missingRequiredColors
        }
        
        return try parseWindowsTerminalScheme(scheme)
    }
    
    private func parseWindowsTerminalScheme(_ scheme: [String: Any]) throws -> YOLOTermTheme {
        guard let name = scheme["name"] as? String,
              let background = scheme["background"] as? String,
              let foreground = scheme["foreground"] as? String else {
            throw ThemeImportError.missingRequiredColors
        }
        
        let id = name.lowercased().replacingOccurrences(of: " ", with: "-")
        let cursor = scheme["cursorColor"] as? String ?? foreground
        let selection = scheme["selectionBackground"] as? String ?? "#44475a"
        
        let ansiColors = YOLOTermTheme.AnsiColors(
            black: scheme["black"] as? String ?? "#000000",
            red: scheme["red"] as? String ?? "#cc0000",
            green: scheme["green"] as? String ?? "#00cc00",
            yellow: scheme["yellow"] as? String ?? "#cccc00",
            blue: scheme["blue"] as? String ?? "#0000cc",
            magenta: scheme["purple"] as? String ?? "#cc00cc",
            cyan: scheme["cyan"] as? String ?? "#00cccc",
            white: scheme["white"] as? String ?? "#cccccc",
            brightBlack: scheme["brightBlack"] as? String ?? "#555555",
            brightRed: scheme["brightRed"] as? String ?? "#ff5555",
            brightGreen: scheme["brightGreen"] as? String ?? "#55ff55",
            brightYellow: scheme["brightYellow"] as? String ?? "#ffff55",
            brightBlue: scheme["brightBlue"] as? String ?? "#5555ff",
            brightMagenta: scheme["brightPurple"] as? String ?? "#ff55ff",
            brightCyan: scheme["brightCyan"] as? String ?? "#55ffff",
            brightWhite: scheme["brightWhite"] as? String ?? "#ffffff"
        )
        
        return YOLOTermTheme(
            id: id,
            name: name,
            description: "Imported from Windows Terminal",
            background: background,
            foreground: foreground,
            cursor: cursor,
            cursorAccent: background,
            selectionBackground: selection,
            useDefaultAnsi: nil,
            ansiColors: ansiColors
        )
    }
    
    // MARK: - Ghostty Import
    
    private func importGhostty(from url: URL) throws -> YOLOTermTheme {
        let content = try String(contentsOf: url, encoding: .utf8)
        var config: [String: String] = [:]
        
        // Parse Ghostty config format (key = value)
        for line in content.components(separatedBy: .newlines) {
            let trimmed = line.trimmingCharacters(in: .whitespaces)
            guard !trimmed.isEmpty, !trimmed.hasPrefix("#") else { continue }
            
            let parts = trimmed.components(separatedBy: "=").map { $0.trimmingCharacters(in: .whitespaces) }
            guard parts.count == 2 else { continue }
            
            config[parts[0]] = parts[1]
        }
        
        func convertColor(_ value: String?) -> String? {
            guard let value = value else { return nil }
            
            // Ghostty format: 0xRRGGBB or #RRGGBB or rgb(r, g, b)
            if value.hasPrefix("0x") {
                return "#" + value.dropFirst(2)
            } else if value.hasPrefix("#") {
                return value
            } else if value.hasPrefix("rgb(") {
                // Parse rgb(r, g, b)
                let rgb = value.dropFirst(4).dropLast()
                let components = rgb.components(separatedBy: ",").compactMap { Int($0.trimmingCharacters(in: .whitespaces)) }
                guard components.count == 3 else { return nil }
                return String(format: "#%02x%02x%02x", components[0], components[1], components[2])
            }
            return nil
        }
        
        let name = url.deletingPathExtension().lastPathComponent
        let id = name.lowercased().replacingOccurrences(of: " ", with: "-")
        
        guard let background = convertColor(config["background"]),
              let foreground = convertColor(config["foreground"]) else {
            throw ThemeImportError.missingRequiredColors
        }
        
        let cursor = convertColor(config["cursor-color"]) ?? foreground
        let selection = convertColor(config["selection-background"]) ?? "#44475a"
        
        let ansiColors = YOLOTermTheme.AnsiColors(
            black: convertColor(config["palette"] ?? config["color0"]) ?? "#000000",
            red: convertColor(config["color1"]) ?? "#cc0000",
            green: convertColor(config["color2"]) ?? "#00cc00",
            yellow: convertColor(config["color3"]) ?? "#cccc00",
            blue: convertColor(config["color4"]) ?? "#0000cc",
            magenta: convertColor(config["color5"]) ?? "#cc00cc",
            cyan: convertColor(config["color6"]) ?? "#00cccc",
            white: convertColor(config["color7"]) ?? "#cccccc",
            brightBlack: convertColor(config["color8"]) ?? "#555555",
            brightRed: convertColor(config["color9"]) ?? "#ff5555",
            brightGreen: convertColor(config["color10"]) ?? "#55ff55",
            brightYellow: convertColor(config["color11"]) ?? "#ffff55",
            brightBlue: convertColor(config["color12"]) ?? "#5555ff",
            brightMagenta: convertColor(config["color13"]) ?? "#ff55ff",
            brightCyan: convertColor(config["color14"]) ?? "#55ffff",
            brightWhite: convertColor(config["color15"]) ?? "#ffffff"
        )
        
        return YOLOTermTheme(
            id: id,
            name: name,
            description: "Imported from Ghostty",
            background: background,
            foreground: foreground,
            cursor: cursor,
            cursorAccent: background,
            selectionBackground: selection,
            useDefaultAnsi: nil,
            ansiColors: ansiColors
        )
    }
    
    // MARK: - Export
    
    public func saveTheme(_ theme: YOLOTermTheme, to directory: URL) throws {
        let encoder = JSONEncoder()
        encoder.outputFormatting = [.prettyPrinted, .sortedKeys]
        let data = try encoder.encode(theme)
        
        let filename = "\(theme.id).json"
        let fileURL = directory.appendingPathComponent(filename)
        try data.write(to: fileURL)
    }
}

public enum ThemeImportError: Error, LocalizedError {
    case unsupportedFormat
    case invalidFormat
    case missingRequiredColors
    case fileNotFound
    
    public var errorDescription: String? {
        switch self {
        case .unsupportedFormat:
            return "Unsupported theme format. Supported formats: .itermcolors, Windows Terminal JSON, Ghostty themes"
        case .invalidFormat:
            return "Invalid theme file format"
        case .missingRequiredColors:
            return "Theme file is missing required colors"
        case .fileNotFound:
            return "Theme file not found"
        }
    }
}
