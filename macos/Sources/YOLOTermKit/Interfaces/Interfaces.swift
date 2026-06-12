import Foundation

// MARK: - PTY Interfaces

/// Spawn a shell with cwd, size, and env policy applied
public protocol PtySpawning {
    func spawn(
        shell: String,
        cwd: URL,
        cols: Int,
        rows: Int,
        env: [String: String]
    ) throws -> any PtySession
}

/// Write user input bytes to a PTY session
public protocol PtyWriting {
    func write(_ data: Data) throws
}

/// Propagate terminal size changes to PTY
public protocol PtyResizing {
    func resize(cols: Int, rows: Int) throws
}

/// PTY lifecycle management
public protocol PtyLifecycle {
    /// Kill the PTY session
    func kill() throws
    
    /// Observe natural exit (fires exactly once)
    var onExit: (@Sendable (Int32) -> Void)? { get set }
    
    /// Check if the PTY is still alive
    var isAlive: Bool { get }
}

/// Combined PTY session interface
public protocol PtySession: PtyWriting, PtyResizing, PtyLifecycle {
    var pid: Int32 { get }
}

// MARK: - Terminal Surface

/// Cell representation for golden tests
public struct Cell: Equatable, Sendable {
    public let char: String
    public let fg: Color?
    public let bg: Color?
    public let attrs: Attributes
    
    public struct Attributes: OptionSet, Sendable {
        public let rawValue: UInt16
        public init(rawValue: UInt16) { self.rawValue = rawValue }
        
        public static let bold = Attributes(rawValue: 1 << 0)
        public static let italic = Attributes(rawValue: 1 << 1)
        public static let underline = Attributes(rawValue: 1 << 2)
        public static let strikethrough = Attributes(rawValue: 1 << 3)
        public static let dim = Attributes(rawValue: 1 << 4)
        public static let inverse = Attributes(rawValue: 1 << 5)
    }
    
    public init(char: String, fg: Color?, bg: Color?, attrs: Attributes) {
        self.char = char
        self.fg = fg
        self.bg = bg
        self.attrs = attrs
    }
}

/// RGB color representation
public struct Color: Equatable, Sendable {
    public let r: UInt8
    public let g: UInt8
    public let b: UInt8
    
    public init(r: UInt8, g: UInt8, b: UInt8) {
        self.r = r
        self.g = g
        self.b = b
    }
    
    public init?(hex: String) {
        let hex = hex.trimmingCharacters(in: CharacterSet(charactersIn: "#"))
        guard hex.count == 6 else { return nil }
        
        guard let value = UInt32(hex, radix: 16) else { return nil }
        self.r = UInt8((value >> 16) & 0xFF)
        self.g = UInt8((value >> 8) & 0xFF)
        self.b = UInt8(value & 0xFF)
    }
    
    public var hex: String {
        String(format: "#%02x%02x%02x", r, g, b)
    }
}

/// Terminal surface metrics
public struct CellMetrics: Sendable {
    public let width: Double
    public let height: Double
    
    public init(width: Double, height: Double) {
        self.width = width
        self.height = height
    }
}

/// Wraps SwiftTerm Terminal/TerminalView
public protocol TerminalSurface {
    /// Feed raw PTY bytes
    func feed(_ data: Data)
    
    /// Get cell metrics
    func getCellMetrics() -> CellMetrics
    
    /// Get cell at position (for golden tests)
    func getCell(col: Int, row: Int) -> Cell?
    
    /// Get terminal dimensions
    var cols: Int { get }
    var rows: Int { get }
    
    /// Serialize visible buffer state
    func serialize() -> Data
}

// MARK: - Theme

/// Theme definition matching contracts/themes/*.json
public struct Theme: Codable, Sendable {
    public let id: String
    public let name: String
    public let description: String
    public let background: String
    public let foreground: String
    public let cursor: String
    public let cursorAccent: String
    public let selectionBackground: String
    public let useDefaultAnsi: Bool
    public let ansiColors: [String]?
    
    public init(
        id: String,
        name: String,
        description: String,
        background: String,
        foreground: String,
        cursor: String,
        cursorAccent: String,
        selectionBackground: String,
        useDefaultAnsi: Bool,
        ansiColors: [String]? = nil
    ) {
        self.id = id
        self.name = name
        self.description = description
        self.background = background
        self.foreground = foreground
        self.cursor = cursor
        self.cursorAccent = cursorAccent
        self.selectionBackground = selectionBackground
        self.useDefaultAnsi = useDefaultAnsi
        self.ansiColors = ansiColors
    }
}

/// Theme source and change notifications
public protocol ThemeSource {
    /// Current theme
    var currentTheme: Theme { get async }
    
    /// Load theme by ID
    func loadTheme(id: String) async throws
    
    /// List available themes
    func availableThemes() async -> [Theme]
    
    /// Subscribe to theme changes
    func onChange(_ handler: @escaping @Sendable (Theme) -> Void) async
}

// MARK: - Environment Policy

/// Environment variable policy from contracts/fixtures/env-policy.json
public struct EnvPolicy: Codable, Sendable {
    public let set: [String: String]
    public let remove: [String]
    
    public init(set: [String: String], remove: [String]) {
        self.set = set
        self.remove = remove
    }
    
    /// Apply policy to current environment
    public func apply(to env: [String: String], version: String) -> [String: String] {
        var result = env
        
        // Remove hostile vars
        for key in remove {
            result.removeValue(forKey: key)
        }
        
        // Set required vars
        for (key, value) in set {
            if key == "TERM_PROGRAM_VERSION" {
                result[key] = version
            } else {
                result[key] = value
            }
        }
        
        return result
    }
}
