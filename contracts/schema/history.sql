-- YOLOTerm unified command history schema
-- SQLite 3 + FTS5 (all tracks use identical schema)
-- Based on TermGrid's proven model (SPEC.md §9.1)

PRAGMA foreign_keys = ON;
PRAGMA journal_mode = WAL;

CREATE TABLE IF NOT EXISTS commands (
    id          INTEGER PRIMARY KEY AUTOINCREMENT,
    command     TEXT NOT NULL,
    shell       TEXT NOT NULL,        -- 'zsh', 'bash', 'fish', 'pwsh', 'cmd', etc.
    cwd         TEXT,                 -- working directory at execution time
    pane_id     TEXT,                 -- which pane it ran in
    tab_name    TEXT,                 -- which tab (for grouping)
    exit_code   INTEGER,              -- NULL if still running or unknown
    duration_ms INTEGER,              -- execution time in milliseconds
    timestamp   INTEGER NOT NULL,     -- Unix epoch milliseconds
    project     TEXT,                 -- derived from cwd or .yoloterm.json
    favorite    INTEGER DEFAULT 0,    -- user-starred commands
    note        TEXT,                 -- user annotation
    redacted    INTEGER DEFAULT 0     -- 1 if privacy filter matched
);

-- Indexes for fast queries
CREATE INDEX IF NOT EXISTS idx_commands_timestamp ON commands(timestamp DESC);
CREATE INDEX IF NOT EXISTS idx_commands_shell ON commands(shell);
CREATE INDEX IF NOT EXISTS idx_commands_cwd ON commands(cwd);
CREATE INDEX IF NOT EXISTS idx_commands_project ON commands(project);
CREATE INDEX IF NOT EXISTS idx_commands_exit_code ON commands(exit_code);

-- Full-text search index (FTS5)
CREATE VIRTUAL TABLE IF NOT EXISTS commands_fts USING fts5(
    command,
    note,
    content=commands,
    content_rowid=id,
    tokenize='porter unicode61'
);

-- Triggers to keep FTS5 in sync with main table
CREATE TRIGGER IF NOT EXISTS commands_fts_insert AFTER INSERT ON commands BEGIN
    INSERT INTO commands_fts(rowid, command, note)
    VALUES (new.id, new.command, new.note);
END;

CREATE TRIGGER IF NOT EXISTS commands_fts_delete AFTER DELETE ON commands BEGIN
    INSERT INTO commands_fts(commands_fts, rowid, command, note)
    VALUES ('delete', old.id, old.command, old.note);
END;

CREATE TRIGGER IF NOT EXISTS commands_fts_update AFTER UPDATE ON commands BEGIN
    INSERT INTO commands_fts(commands_fts, rowid, command, note)
    VALUES ('delete', old.id, old.command, old.note);
    INSERT INTO commands_fts(rowid, command, note)
    VALUES (new.id, new.command, new.note);
END;

-- Example queries for reference:

-- Fuzzy search (FTS5):
-- SELECT * FROM commands WHERE id IN (
--     SELECT rowid FROM commands_fts WHERE commands_fts MATCH 'git*'
-- ) ORDER BY timestamp DESC LIMIT 20;

-- Per-pane history:
-- SELECT * FROM commands WHERE pane_id = ? ORDER BY timestamp DESC LIMIT 100;

-- Global recent:
-- SELECT * FROM commands ORDER BY timestamp DESC LIMIT 1000;

-- Failed commands:
-- SELECT * FROM commands WHERE exit_code != 0 ORDER BY timestamp DESC;

-- Favorites:
-- SELECT * FROM commands WHERE favorite = 1 ORDER BY timestamp DESC;
