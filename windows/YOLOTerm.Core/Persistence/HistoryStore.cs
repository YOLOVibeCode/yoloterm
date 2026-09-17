using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;

namespace YOLOTerm.Core.Persistence;

/// <summary>
/// HistoryStore manages the command history database with full-text search.
/// 
/// Features:
/// - Microsoft.Data.Sqlite integration with FTS5 for fast full-text search
/// - Privacy-first: redaction patterns applied before insertion
/// - Per-pane and global history queries
/// - Performance target: &lt;50ms search on 100k rows
/// 
/// Storage: %LOCALAPPDATA%\YOLOTerm\history.db
/// Schema: contracts/schema/history.sql
/// </summary>
public sealed class HistoryStore : IDisposable
{
    // MARK: - Models
    
    public sealed class Command
    {
        public long? Id { get; set; }
        public string CommandText { get; set; } = string.Empty;
        public string Shell { get; set; } = string.Empty;
        public string? Cwd { get; set; }
        public string? PaneId { get; set; }
        public string? TabName { get; set; }
        public int? ExitCode { get; set; }
        public int? DurationMs { get; set; }
        public long Timestamp { get; set; }
        public string? Project { get; set; }
        public bool Favorite { get; set; }
        public string? Note { get; set; }
        public bool Redacted { get; set; }
    }
    
    public sealed class SearchOptions
    {
        public string? PaneId { get; set; }
        public int Limit { get; set; } = 100;
        public int Offset { get; set; }
        public bool OnlyFavorites { get; set; }
        public bool OnlyFailed { get; set; }
    }
    
    // MARK: - Configuration
    
    private readonly SqliteConnection connection;
    private readonly List<RedactionPattern> redactionPatterns;
    
    public HistoryStore(string? baseDirectory = null, string? redactionPatternsPath = null)
    {
        // Determine database path
        string dbPath;
        if (baseDirectory != null)
        {
            dbPath = Path.Combine(baseDirectory, "history.db");
        }
        else
        {
            string localAppData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            string yolotermDir = Path.Combine(localAppData, "YOLOTerm");
            Directory.CreateDirectory(yolotermDir);
            dbPath = Path.Combine(yolotermDir, "history.db");
        }
        
        // Open database with WAL mode
        SqliteConnectionStringBuilder builder = new()
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        };
        
        connection = new SqliteConnection(builder.ToString());
        connection.Open();
        
        // Configure database
        using (SqliteCommand cmd = connection.CreateCommand())
        {
            cmd.CommandText = "PRAGMA journal_mode = WAL; PRAGMA foreign_keys = ON;";
            cmd.ExecuteNonQuery();
        }
        
        // Load redaction patterns
        string patternsPath = redactionPatternsPath ?? GetDefaultRedactionPatternsPath();
        redactionPatterns = LoadRedactionPatterns(patternsPath);
        
        // Initialize schema
        InitializeSchema();
    }
    
    // MARK: - Public API
    
    /// <summary>
    /// Insert a command into the history (with redaction applied).
    /// </summary>
    public async Task InsertAsync(Command command)
    {
        string originalCommand = command.CommandText;
        string redactedCommand = ApplyRedaction(originalCommand);
        bool wasRedacted = redactedCommand != originalCommand;
        
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO commands (command, shell, cwd, pane_id, tab_name, exit_code, duration_ms, timestamp, project, favorite, note, redacted)
            VALUES (@command, @shell, @cwd, @pane_id, @tab_name, @exit_code, @duration_ms, @timestamp, @project, @favorite, @note, @redacted)
        ";
        
        cmd.Parameters.AddWithValue("@command", redactedCommand);
        cmd.Parameters.AddWithValue("@shell", command.Shell);
        cmd.Parameters.AddWithValue("@cwd", (object?)command.Cwd ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@pane_id", (object?)command.PaneId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@tab_name", (object?)command.TabName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@exit_code", (object?)command.ExitCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@duration_ms", (object?)command.DurationMs ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@timestamp", command.Timestamp);
        cmd.Parameters.AddWithValue("@project", (object?)command.Project ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@favorite", command.Favorite ? 1 : 0);
        cmd.Parameters.AddWithValue("@note", (object?)command.Note ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@redacted", wasRedacted ? 1 : 0);
        
        await cmd.ExecuteNonQueryAsync();
    }
    
    /// <summary>
    /// Search commands using full-text search.
    /// </summary>
    public async Task<List<Command>> SearchAsync(string query, SearchOptions? options = null)
    {
        options ??= new SearchOptions();
        
        List<Command> results = new();
        
        string ftsQuery = query + "*"; // Prefix matching
        
        string sql = @"
            SELECT * FROM commands
            WHERE id IN (
                SELECT rowid FROM commands_fts WHERE commands_fts MATCH @query
            )
        ";
        
        List<SqliteParameter> parameters = new()
        {
            new SqliteParameter("@query", ftsQuery)
        };
        
        if (options.PaneId != null)
        {
            sql += " AND pane_id = @pane_id";
            parameters.Add(new SqliteParameter("@pane_id", options.PaneId));
        }
        
        if (options.OnlyFavorites)
        {
            sql += " AND favorite = 1";
        }
        
        if (options.OnlyFailed)
        {
            sql += " AND exit_code != 0";
        }
        
        sql += " ORDER BY timestamp DESC LIMIT @limit OFFSET @offset";
        parameters.Add(new SqliteParameter("@limit", options.Limit));
        parameters.Add(new SqliteParameter("@offset", options.Offset));
        
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddRange(parameters.ToArray());
        
        using SqliteDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(ReadCommand(reader));
        }
        
        return results;
    }
    
    /// <summary>
    /// Get recent commands (optionally filtered by pane).
    /// </summary>
    public async Task<List<Command>> RecentAsync(SearchOptions? options = null)
    {
        options ??= new SearchOptions();
        
        List<Command> results = new();
        
        string sql = "SELECT * FROM commands WHERE 1=1";
        List<SqliteParameter> parameters = new();
        
        if (options.PaneId != null)
        {
            sql += " AND pane_id = @pane_id";
            parameters.Add(new SqliteParameter("@pane_id", options.PaneId));
        }
        
        if (options.OnlyFavorites)
        {
            sql += " AND favorite = 1";
        }
        
        if (options.OnlyFailed)
        {
            sql += " AND exit_code != 0";
        }
        
        sql += " ORDER BY timestamp DESC LIMIT @limit OFFSET @offset";
        parameters.Add(new SqliteParameter("@limit", options.Limit));
        parameters.Add(new SqliteParameter("@offset", options.Offset));
        
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = sql;
        cmd.Parameters.AddRange(parameters.ToArray());
        
        using SqliteDataReader reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(ReadCommand(reader));
        }
        
        return results;
    }
    
    /// <summary>
    /// Toggle favorite status for a command.
    /// </summary>
    public async Task ToggleFavoriteAsync(long commandId)
    {
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = @"
            UPDATE commands 
            SET favorite = CASE WHEN favorite = 0 THEN 1 ELSE 0 END
            WHERE id = @id
        ";
        cmd.Parameters.AddWithValue("@id", commandId);
        await cmd.ExecuteNonQueryAsync();
    }
    
    /// <summary>
    /// Update the note for a command.
    /// </summary>
    public async Task UpdateNoteAsync(long commandId, string? note)
    {
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "UPDATE commands SET note = @note WHERE id = @id";
        cmd.Parameters.AddWithValue("@id", commandId);
        cmd.Parameters.AddWithValue("@note", (object?)note ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
    }
    
    /// <summary>
    /// Delete old commands (for retention policy).
    /// </summary>
    public async Task DeleteOlderThanAsync(int days)
    {
        long cutoff = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - 
                      (days * 24L * 60 * 60 * 1000);
        
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "DELETE FROM commands WHERE timestamp < @cutoff";
        cmd.Parameters.AddWithValue("@cutoff", cutoff);
        await cmd.ExecuteNonQueryAsync();
    }
    
    /// <summary>
    /// Get total command count.
    /// </summary>
    public async Task<int> CountAsync()
    {
        using SqliteCommand cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM commands";
        object? result = await cmd.ExecuteScalarAsync();
        return result != null ? Convert.ToInt32(result) : 0;
    }
    
    // MARK: - Schema Initialization
    
    private void InitializeSchema()
    {
        using SqliteTransaction transaction = connection.BeginTransaction();
        
        // Read schema from contracts/schema/history.sql
        // For now, inline the schema (in production, could read from file)
        
        using (SqliteCommand cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
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
                );
                
                CREATE INDEX IF NOT EXISTS idx_commands_timestamp ON commands(timestamp DESC);
                CREATE INDEX IF NOT EXISTS idx_commands_shell ON commands(shell);
                CREATE INDEX IF NOT EXISTS idx_commands_cwd ON commands(cwd);
                CREATE INDEX IF NOT EXISTS idx_commands_project ON commands(project);
                CREATE INDEX IF NOT EXISTS idx_commands_exit_code ON commands(exit_code);
                
                CREATE VIRTUAL TABLE IF NOT EXISTS commands_fts USING fts5(
                    command,
                    note,
                    content=commands,
                    content_rowid=id,
                    tokenize='porter unicode61'
                );
            ";
            cmd.ExecuteNonQuery();
        }
        
        // Create triggers for FTS sync
        using (SqliteCommand cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TRIGGER IF NOT EXISTS commands_fts_insert AFTER INSERT ON commands BEGIN
                    INSERT INTO commands_fts(rowid, command, note)
                    VALUES (new.id, new.command, new.note);
                END;
            ";
            cmd.ExecuteNonQuery();
        }
        
        using (SqliteCommand cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TRIGGER IF NOT EXISTS commands_fts_delete AFTER DELETE ON commands BEGIN
                    INSERT INTO commands_fts(commands_fts, rowid, command, note)
                    VALUES ('delete', old.id, old.command, old.note);
                END;
            ";
            cmd.ExecuteNonQuery();
        }
        
        using (SqliteCommand cmd = connection.CreateCommand())
        {
            cmd.CommandText = @"
                CREATE TRIGGER IF NOT EXISTS commands_fts_update AFTER UPDATE ON commands BEGIN
                    INSERT INTO commands_fts(commands_fts, rowid, command, note)
                    VALUES ('delete', old.id, old.command, old.note);
                    INSERT INTO commands_fts(rowid, command, note)
                    VALUES (new.id, new.command, new.note);
                END;
            ";
            cmd.ExecuteNonQuery();
        }
        
        transaction.Commit();
    }
    
    // MARK: - Redaction
    
    private sealed class RedactionPattern
    {
        public string Name { get; set; } = string.Empty;
        public string RegexPattern { get; set; } = string.Empty;
        public string Replacement { get; set; } = string.Empty;
        public string Reason { get; set; } = string.Empty;
        
        public Regex? CompiledRegex { get; set; }
    }
    
    private sealed class RedactionConfig
    {
        public List<RedactionPattern> Patterns { get; set; } = new();
    }
    
    private static string GetDefaultRedactionPatternsPath()
    {
        // Navigate to contracts/fixtures/redaction.json from Core project
        string currentDir = AppContext.BaseDirectory;
        
        // Try to find the project root (contains contracts folder)
        string? projectRoot = FindProjectRoot(currentDir);
        
        if (projectRoot != null)
        {
            return Path.Combine(projectRoot, "contracts", "fixtures", "redaction.json");
        }
        
        // Fallback: use a relative path
        return Path.Combine("..", "..", "..", "..", "..", "contracts", "fixtures", "redaction.json");
    }
    
    private static string? FindProjectRoot(string startPath)
    {
        string? currentDir = startPath;
        
        while (currentDir != null)
        {
            string contractsPath = Path.Combine(currentDir, "contracts");
            if (Directory.Exists(contractsPath))
            {
                return currentDir;
            }
            
            currentDir = Path.GetDirectoryName(currentDir);
        }
        
        return null;
    }
    
    private static List<RedactionPattern> LoadRedactionPatterns(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                Console.WriteLine($"HistoryStore: Redaction patterns file not found at {path}, using empty patterns");
                return new List<RedactionPattern>();
            }
            
            string json = File.ReadAllText(path);
            
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;
            
            List<RedactionPattern> patterns = new();
            
            if (root.TryGetProperty("patterns", out JsonElement patternsElement))
            {
                foreach (JsonElement patternElement in patternsElement.EnumerateArray())
                {
                    RedactionPattern pattern = new()
                    {
                        Name = patternElement.GetProperty("name").GetString() ?? string.Empty,
                        RegexPattern = patternElement.GetProperty("regex").GetString() ?? string.Empty,
                        Replacement = patternElement.GetProperty("replacement").GetString() ?? string.Empty,
                        Reason = patternElement.GetProperty("reason").GetString() ?? string.Empty
                    };
                    
                    try
                    {
                        pattern.CompiledRegex = new Regex(
                            pattern.RegexPattern,
                            RegexOptions.IgnoreCase | RegexOptions.Compiled,
                            TimeSpan.FromSeconds(1)
                        );
                        patterns.Add(pattern);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"HistoryStore: Failed to compile regex pattern '{pattern.Name}': {ex.Message}");
                    }
                }
            }
            
            return patterns;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"HistoryStore: Failed to load redaction patterns: {ex.Message}");
            return new List<RedactionPattern>();
        }
    }
    
    private string ApplyRedaction(string command)
    {
        string result = command;
        
        foreach (RedactionPattern pattern in redactionPatterns)
        {
            if (pattern.CompiledRegex != null)
            {
                try
                {
                    result = pattern.CompiledRegex.Replace(result, pattern.Replacement);
                }
                catch (RegexMatchTimeoutException)
                {
                    Console.WriteLine($"HistoryStore: Regex timeout for pattern '{pattern.Name}'");
                }
            }
        }
        
        return result;
    }
    
    // MARK: - Helpers
    
    private static Command ReadCommand(SqliteDataReader reader)
    {
        return new Command
        {
            Id = reader.GetInt64(reader.GetOrdinal("id")),
            CommandText = reader.GetString(reader.GetOrdinal("command")),
            Shell = reader.GetString(reader.GetOrdinal("shell")),
            Cwd = reader.IsDBNull(reader.GetOrdinal("cwd")) ? null : reader.GetString(reader.GetOrdinal("cwd")),
            PaneId = reader.IsDBNull(reader.GetOrdinal("pane_id")) ? null : reader.GetString(reader.GetOrdinal("pane_id")),
            TabName = reader.IsDBNull(reader.GetOrdinal("tab_name")) ? null : reader.GetString(reader.GetOrdinal("tab_name")),
            ExitCode = reader.IsDBNull(reader.GetOrdinal("exit_code")) ? null : reader.GetInt32(reader.GetOrdinal("exit_code")),
            DurationMs = reader.IsDBNull(reader.GetOrdinal("duration_ms")) ? null : reader.GetInt32(reader.GetOrdinal("duration_ms")),
            Timestamp = reader.GetInt64(reader.GetOrdinal("timestamp")),
            Project = reader.IsDBNull(reader.GetOrdinal("project")) ? null : reader.GetString(reader.GetOrdinal("project")),
            Favorite = reader.GetInt32(reader.GetOrdinal("favorite")) == 1,
            Note = reader.IsDBNull(reader.GetOrdinal("note")) ? null : reader.GetString(reader.GetOrdinal("note")),
            Redacted = reader.GetInt32(reader.GetOrdinal("redacted")) == 1
        };
    }
    
    public void Dispose()
    {
        connection?.Dispose();
    }
}
