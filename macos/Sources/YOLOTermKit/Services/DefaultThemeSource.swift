import Foundation

/// ThemeSource implementation that loads themes from contracts/themes/
public actor DefaultThemeSource: ThemeSource {
    private var current: Theme
    private var themes: [Theme] = []
    private var handlers: [@Sendable (Theme) -> Void] = []
    
    public init() async throws {
        // Load default theme
        let themesPath = Bundle.main.resourceURL?
            .deletingLastPathComponent()
            .deletingLastPathComponent()
            .appendingPathComponent("contracts/themes")
        
        // Start with default theme
        self.current = Self.defaultVividTheme()
        themes.append(current)
        
        if let themesPath = themesPath {
            try await loadThemesFromDirectory(themesPath)
            // Update current to terminal-default if available
            if let defaultTheme = themes.first(where: { $0.id == "terminal-default" }) {
                self.current = defaultTheme
            }
        }
    }
    
    public init(themesPath: URL) async throws {
        self.current = Self.defaultVividTheme()
        themes.append(current)
        try await loadThemesFromDirectory(themesPath)
        if let defaultTheme = themes.first(where: { $0.id == "terminal-default" }) {
            self.current = defaultTheme
        }
    }
    
    public var currentTheme: Theme {
        current
    }
    
    public func loadTheme(id: String) async throws {
        guard let theme = themes.first(where: { $0.id == id }) else {
            throw ThemeError.themeNotFound(id)
        }
        
        current = theme
        
        // Notify handlers
        for handler in handlers {
            handler(theme)
        }
    }
    
    public func availableThemes() async -> [Theme] {
        themes
    }
    
    public func onChange(_ handler: @escaping @Sendable (Theme) -> Void) async {
        handlers.append(handler)
    }
    
    private func loadThemesFromDirectory(_ path: URL) async throws {
        let fileManager = FileManager.default
        
        guard fileManager.fileExists(atPath: path.path) else {
            return
        }
        
        let contents = try fileManager.contentsOfDirectory(
            at: path,
            includingPropertiesForKeys: nil,
            options: [.skipsHiddenFiles]
        )
        
        for fileURL in contents where fileURL.pathExtension == "json" && !fileURL.lastPathComponent.contains("schema") {
            do {
                let data = try Data(contentsOf: fileURL)
                let theme = try JSONDecoder().decode(Theme.self, from: data)
                themes.append(theme)
            } catch {
                print("Failed to load theme from \(fileURL.lastPathComponent): \(error)")
            }
        }
    }
    
    private static func defaultVividTheme() -> Theme {
        Theme(
            id: "terminal-default",
            name: "Terminal Default (Vivid)",
            description: "Default vivid xterm ANSI palette",
            background: "#1e1e1e",
            foreground: "#d4d4d4",
            cursor: "#ffffff",
            cursorAccent: "#1e1e1e",
            selectionBackground: "#264f78",
            useDefaultAnsi: true,
            ansiColors: nil
        )
    }
}

public enum ThemeError: Error {
    case themeNotFound(String)
    case invalidThemeFormat
}
