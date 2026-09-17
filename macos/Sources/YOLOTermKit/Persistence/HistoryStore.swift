import Foundation
import GRDB

/// HistoryStore manages the command history database with full-text search.
///
/// Features:
/// - GRDB.swift integration with FTS5 for fast full-text search
/// - Privacy-first: redaction patterns applied before insertion
/// - Per-pane and global history queries
/// - Performance target: <50ms search on 100k rows
///
/// Storage: ~/Library/Application Support/YOLOTerm/history.db
/// Schema: contracts/schema/history.sql
@MainActor
public final class HistoryStore {
    
    // MARK: - Models
    
    public struct Command: Codable, FetchableRecord, PersistableRecord {
        public var id: Int64?
        public var command: String
        public var shell: String
        public var cwd: String?
        public var paneID: String?
        public var tabName: String?
        public var exitCode: Int?
        public var durationMs: Int?
        public var timestamp: Int64
        public var project: String?
        public var favorite: Bool
        public var note: String?
        public var redacted: Bool
        
        public static let databaseTableName = "commands"
        
        public init(
            id: Int64? = nil,
            command: String,
            shell: String,
            cwd: String? = nil,
            paneID: String? = nil,
            tabName: String? = nil,
            exitCode: Int? = nil,
            durationMs: Int? = nil,
            timestamp: Int64 = Int64(Date().timeIntervalSince1970 * 1000),
            project: String? = nil,
            favorite: Bool = false,
            note: String? = nil,
            redacted: Bool = false
        ) {
            self.id = id
            self.command = command
            self.shell = shell
            self.cwd = cwd
            self.paneID = paneID
            self.tabName = tabName
            self.exitCode = exitCode
            self.durationMs = durationMs
            self.timestamp = timestamp
            self.project = project
            self.favorite = favorite
            self.note = note
            self.redacted = redacted
        }
        
        enum CodingKeys: String, CodingKey {
            case id
            case command
            case shell
            case cwd
            case paneID = "pane_id"
            case tabName = "tab_name"
            case exitCode = "exit_code"
            case durationMs = "duration_ms"
            case timestamp
            case project
            case favorite
            case note
            case redacted
        }
    }
    
    public struct SearchOptions {
        public var paneID: String?
        public var limit: Int
        public var offset: Int
        public var onlyFavorites: Bool
        public var onlyFailed: Bool
        
        public init(
            paneID: String? = nil,
            limit: Int = 100,
            offset: Int = 0,
            onlyFavorites: Bool = false,
            onlyFailed: Bool = false
        ) {
            self.paneID = paneID
            self.limit = limit
            self.offset = offset
            self.onlyFavorites = onlyFavorites
            self.onlyFailed = onlyFailed
        }
    }
    
    // MARK: - Configuration
    
    private let dbQueue: DatabaseQueue
    private let redactionPatterns: [RedactionPattern]
    
    public init(baseDirectory: URL? = nil, redactionPatternsURL: URL? = nil) throws {
        // Determine database path
        let dbURL: URL
        if let baseDirectory {
            dbURL = baseDirectory.appendingPathComponent("history.db")
        } else {
            let appSupport = FileManager.default.urls(
                for: .applicationSupportDirectory,
                in: .userDomainMask
            ).first!
            let yolotermDir = appSupport.appendingPathComponent("YOLOTerm", isDirectory: true)
            try? FileManager.default.createDirectory(at: yolotermDir, withIntermediateDirectories: true)
            dbURL = yolotermDir.appendingPathComponent("history.db")
        }
        
        // Open database with WAL mode
        var config = Configuration()
        config.prepareDatabase { db in
            try db.execute(sql: "PRAGMA journal_mode = WAL")
            try db.execute(sql: "PRAGMA foreign_keys = ON")
        }
        
        self.dbQueue = try DatabaseQueue(path: dbURL.path, configuration: config)
        
        // Load redaction patterns
        let patternsURL = redactionPatternsURL ?? Self.defaultRedactionPatternsURL()
        self.redactionPatterns = try Self.loadRedactionPatterns(from: patternsURL)
        
        // Initialize schema
        try initializeSchema()
    }
    
    // MARK: - Public API
    
    /// Insert a command into the history (with redaction applied).
    public func insert(_ command: Command) throws {
        var redactedCommand = command
        redactedCommand.command = applyRedaction(to: command.command)
        redactedCommand.redacted = redactedCommand.command != command.command
        
        try dbQueue.write { db in
            try redactedCommand.insert(db)
        }
    }
    
    /// Search commands using full-text search.
    public func search(query: String, options: SearchOptions = SearchOptions()) throws -> [Command] {
        return try dbQueue.read { db in
            let ftsQuery = query + "*" // Prefix matching
            
            var sql = """
                SELECT * FROM commands
                WHERE id IN (
                    SELECT rowid FROM commands_fts WHERE commands_fts MATCH ?
                )
                """
            
            var arguments: [DatabaseValueConvertible] = [ftsQuery]
            
            if let paneID = options.paneID {
                sql += " AND pane_id = ?"
                arguments.append(paneID)
            }
            
            if options.onlyFavorites {
                sql += " AND favorite = 1"
            }
            
            if options.onlyFailed {
                sql += " AND exit_code != 0"
            }
            
            sql += " ORDER BY timestamp DESC LIMIT ? OFFSET ?"
            arguments.append(options.limit)
            arguments.append(options.offset)
            
            return try Command.fetchAll(db, sql: sql, arguments: StatementArguments(arguments))
        }
    }
    
    /// Get recent commands (optionally filtered by pane).
    public func recent(options: SearchOptions = SearchOptions()) throws -> [Command] {
        return try dbQueue.read { db in
            var sql = "SELECT * FROM commands WHERE 1=1"
            var arguments: [DatabaseValueConvertible] = []
            
            if let paneID = options.paneID {
                sql += " AND pane_id = ?"
                arguments.append(paneID)
            }
            
            if options.onlyFavorites {
                sql += " AND favorite = 1"
            }
            
            if options.onlyFailed {
                sql += " AND exit_code != 0"
            }
            
            sql += " ORDER BY timestamp DESC LIMIT ? OFFSET ?"
            arguments.append(options.limit)
            arguments.append(options.offset)
            
            return try Command.fetchAll(db, sql: sql, arguments: StatementArguments(arguments))
        }
    }
    
    /// Toggle favorite status for a command.
    public func toggleFavorite(commandID: Int64) throws {
        try dbQueue.write { db in
            guard var command = try Command.fetchOne(db, key: commandID) else { return }
            command.favorite.toggle()
            try command.update(db)
        }
    }
    
    /// Update the note for a command.
    public func updateNote(commandID: Int64, note: String?) throws {
        try dbQueue.write { db in
            guard var command = try Command.fetchOne(db, key: commandID) else { return }
            command.note = note
            try command.update(db)
        }
    }
    
    /// Delete old commands (for retention policy).
    public func deleteOlderThan(days: Int) throws {
        let cutoff = Int64(Date().timeIntervalSince1970 * 1000) - Int64(days * 24 * 60 * 60 * 1000)
        
        try dbQueue.write { db in
            try db.execute(sql: "DELETE FROM commands WHERE timestamp < ?", arguments: [cutoff])
        }
    }
    
    /// Get total command count.
    public func count() throws -> Int {
        return try dbQueue.read { db in
            try Int.fetchOne(db, sql: "SELECT COUNT(*) FROM commands") ?? 0
        }
    }
    
    // MARK: - Schema Initialization
    
    private func initializeSchema() throws {
        try dbQueue.write { db in
            // Create main commands table
            try db.execute(sql: """
                CREATE TABLE IF NOT EXISTS commands (
                    id          INTEGER PRIMARY KEY AUTOINCREMENT,
                    command     TEXT NOT NULL,
                    shell       TEXT NOT NULL,
                    cwd         TEXT,
                    pane_id     TEXT,
                    tab_name    TEXT,
                    exit_code   INTEGER,
                    duration_ms INTEGER,
                    timestamp   INTEGER NOT NULL,
                    project     TEXT,
                    favorite    INTEGER DEFAULT 0,
                    note        TEXT,
                    redacted    INTEGER DEFAULT 0
                )
                """)
            
            // Create indexes
            try db.execute(sql: "CREATE INDEX IF NOT EXISTS idx_commands_timestamp ON commands(timestamp DESC)")
            try db.execute(sql: "CREATE INDEX IF NOT EXISTS idx_commands_shell ON commands(shell)")
            try db.execute(sql: "CREATE INDEX IF NOT EXISTS idx_commands_cwd ON commands(cwd)")
            try db.execute(sql: "CREATE INDEX IF NOT EXISTS idx_commands_project ON commands(project)")
            try db.execute(sql: "CREATE INDEX IF NOT EXISTS idx_commands_exit_code ON commands(exit_code)")
            
            // Create FTS5 virtual table
            try db.execute(sql: """
                CREATE VIRTUAL TABLE IF NOT EXISTS commands_fts USING fts5(
                    command,
                    note,
                    content=commands,
                    content_rowid=id,
                    tokenize='porter unicode61'
                )
                """)
            
            // Create triggers for FTS sync
            try db.execute(sql: """
                CREATE TRIGGER IF NOT EXISTS commands_fts_insert AFTER INSERT ON commands BEGIN
                    INSERT INTO commands_fts(rowid, command, note)
                    VALUES (new.id, new.command, new.note);
                END
                """)
            
            try db.execute(sql: """
                CREATE TRIGGER IF NOT EXISTS commands_fts_delete AFTER DELETE ON commands BEGIN
                    INSERT INTO commands_fts(commands_fts, rowid, command, note)
                    VALUES ('delete', old.id, old.command, old.note);
                END
                """)
            
            try db.execute(sql: """
                CREATE TRIGGER IF NOT EXISTS commands_fts_update AFTER UPDATE ON commands BEGIN
                    INSERT INTO commands_fts(commands_fts, rowid, command, note)
                    VALUES ('delete', old.id, old.command, old.note);
                    INSERT INTO commands_fts(rowid, command, note)
                    VALUES (new.id, new.command, new.note);
                END
                """)
        }
    }
    
    // MARK: - Redaction
    
    private struct RedactionPattern: Codable {
        let name: String
        let regex: String
        let replacement: String
        let reason: String
        
        var compiledRegex: NSRegularExpression? {
            try? NSRegularExpression(pattern: regex, options: [.caseInsensitive])
        }
    }
    
    private struct RedactionConfig: Codable {
        let patterns: [RedactionPattern]
    }
    
    private static func defaultRedactionPatternsURL() -> URL {
        // Default to contracts/fixtures/redaction.json relative to project root
        let currentFile = URL(fileURLWithPath: #file)
        let projectRoot = currentFile
            .deletingLastPathComponent() // Persistence
            .deletingLastPathComponent() // YOLOTermKit
            .deletingLastPathComponent() // Sources
            .deletingLastPathComponent() // macos
        return projectRoot
            .appendingPathComponent("contracts", isDirectory: true)
            .appendingPathComponent("fixtures", isDirectory: true)
            .appendingPathComponent("redaction.json")
    }
    
    private static func loadRedactionPatterns(from url: URL) throws -> [RedactionPattern] {
        let data = try Data(contentsOf: url)
        let config = try JSONDecoder().decode(RedactionConfig.self, from: data)
        return config.patterns
    }
    
    private func applyRedaction(to command: String) -> String {
        var result = command
        
        for pattern in redactionPatterns {
            guard let regex = pattern.compiledRegex else { continue }
            
            let range = NSRange(result.startIndex..<result.endIndex, in: result)
            result = regex.stringByReplacingMatches(
                in: result,
                range: range,
                withTemplate: pattern.replacement
            )
        }
        
        return result
    }
}
