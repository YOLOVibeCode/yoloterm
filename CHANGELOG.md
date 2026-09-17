# Changelog

All notable changes to YOLOTerm will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [0.1.0] - 2026-06-12

### Initial Release

YOLOTerm v0.1.0 is the first public release for macOS, delivering a modern terminal emulator with true color support, multi-pane layouts, and comprehensive history tracking.

#### Core Features

**Rendering & Color**
- True 24-bit color (16M colors) with full ANSI/VT100 support
- Metal-accelerated rendering with CoreText fallback
- Theme system with 6 built-in themes (Dracula, Nord, Gruvbox Dark, Tokyo Night, Catppuccin Mocha, Terminal Default)
- Theme import from iTerm2 (.itermcolors), Windows Terminal, and Ghostty
- Bold, italic, underline, strikethrough, inverse, and dim text attributes
- SGR 58 underline colors

**Layout & Tabs**
- Multi-pane grid layouts with 7 presets: Auto, Single, Columns, Rows, Grid, Main Left, Main Right
- Drag borders to resize panes
- Zoom/unzoom panes with ⌘↩
- Equalize all panes
- Native macOS tab groups with drag-out support
- Focus navigation: ⌥↑/↓/←/→

**History & Search**
- Full-text search across command history with FTS5
- Per-pane search (⌃R) and global search (⌘⇧R)
- Fuzzy ranking for command suggestions
- Exit code and duration tracking
- Privacy redaction patterns for sensitive data
- SQLite-backed storage with < 7ms query time for 100k entries

**Persistence**
- Workspace restoration: tabs, panes, layouts, shell states, and working directories
- Output journal: append-only scrollback per pane with atomic rotation
- Graceful degradation on partial load (never destructive)
- Orphan cleanup on startup

**Shell Integration**
- Plugin installer for zsh, bash, fish, and PowerShell
- OSC 133 prompt marking for accurate history capture
- OSC 7 directory tracking
- Git branch detection in pane labels
- SSH host detection

**Settings**
- Comprehensive settings UI: Appearance, Behavior, Keybindings, History, Shell Integration, Advanced
- Font selection with SF Mono default
- Cursor styles: block, underline, vertical bar
- Cursor blink toggle
- Window opacity control

**OS Integration**
- URL handler: `yoloterm://open?dir=/path/to/folder`
- Finder Service: "New YOLOTerm Tab Here" context menu
- Dock menu with recent directories
- Drag & drop files/folders with automatic path escaping

**Performance**
- Cold start: < 300ms (measured)
- Cat throughput: baseline established
- Memory per pane: ~10-20MB incremental

#### Developer Features

**Testing**
- Color golden corpus with 13 fixtures (GATE 1 passed)
- Layout engine tests against fixtures
- PTY lifecycle tests with dedicated `pty-probe` test binary
- History store performance benchmarks
- Workspace restoration test suite

**CI/CD**
- GitHub Actions workflows for macOS track
- Contracts enforcement (interfaces v1 frozen)
- Automated release pipeline (code signing, notarization, DMG creation)
- Performance regression detection

**Documentation**
- Comprehensive SPEC.md (§1-§10)
- Implementation plan with milestones and gates
- Release guide with signing/notarization instructions
- Benchmark suite with performance budgets

#### Known Limitations

- macOS only (Windows and Linux support planned)
- Manual memory measurement required for per-pane metrics
- Sparkle auto-updates require user-configured EdDSA keys

#### Technical Details

- **Platform:** macOS 14.0+
- **Architecture:** Universal binary (Apple Silicon + Intel)
- **Language:** Swift 6.0
- **Rendering:** SwiftTerm 1.13.0 with Metal
- **Database:** GRDB 6.29.0+
- **License:** MIT

---

## Versioning

YOLOTerm follows semantic versioning:
- **Major:** Breaking changes, incompatible API changes
- **Minor:** New features, backwards-compatible
- **Patch:** Bug fixes, performance improvements

---

## Unreleased

Track upcoming features and fixes here.

### Planned for v0.2.0

- Windows support (Track B)
- Configurable keybindings editor
- Split-view horizontal/vertical presets
- Advanced theme editor
- Import/export settings

---

[0.1.0]: https://github.com/yourusername/yoloterm/releases/tag/v0.1.0
