# YOLOTerm — Interface Contracts (v1)

**Version:** 1.0
**Date:** June 12, 2026
**Status:** Frozen
**Scope:** Normative definitions for all tracks

---

## Overview

This document defines the **13 core interfaces** that every YOLOTerm track (macOS, Windows, Linux) must implement with identical semantics. These interfaces are proven in Track A (macOS) Phase 2 and frozen as v1 for Track B (Windows) implementation.

### Design Principles

1. **Interface Segregation Principle (ISP)** — Each interface has a single, well-defined responsibility
2. **Same names, same semantics** — Interfaces mirror 1:1 across Swift protocols, C# interfaces, and future language bindings
3. **No leaky abstractions** — Platform-specific types stay in implementations; interfaces use primitives or shared types
4. **Testable via fixtures** — All interfaces have corresponding fixture-based conformance tests in `contracts/fixtures/`

---

## 1. PTY Interfaces

### 1.1 PtySpawning

**Role:** Spawn a shell process with environment policy applied

**Swift Protocol:**
```swift
protocol PtySpawning {
    func spawn(
        shell: String,
        cwd: URL,
        cols: Int,
        rows: Int,
        env: [String: String]
    ) throws -> any PtySession
}
```

**Semantics:**
- `shell`: Full path to shell executable (e.g., `/bin/zsh`, `C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe`)
- `cwd`: Starting working directory
- `cols`, `rows`: Initial terminal size
- `env`: Environment variables after policy applied (see `EnvPolicy`)
- Returns: A session implementing `PtyWriting`, `PtyResizing`, and `PtyLifecycle`
- Errors: Throws if shell not found, cwd doesn't exist, or spawn fails

**Threading:** Main thread / main actor only

---

### 1.2 PtyWriting

**Role:** Write user input bytes to a PTY session

**Swift Protocol:**
```swift
protocol PtyWriting {
    func write(_ data: Data) throws
}
```

**Semantics:**
- `data`: Raw bytes from user input (keyboard, paste, etc.)
- Errors: Throws if session closed or write fails
- Non-blocking: Implementation should buffer if needed

**Threading:** Main thread / main actor only

---

### 1.3 PtyResizing

**Role:** Propagate terminal size changes to PTY

**Swift Protocol:**
```swift
protocol PtyResizing {
    func resize(cols: Int, rows: Int) throws
}
```

**Semantics:**
- Sends `TIOCSWINSZ` (Unix) or `ResizePseudoConsole` (Windows)
- Errors: Throws if session closed or resize fails
- Must be called on every window/pane resize before feeding more data

**Threading:** Main thread / main actor only

---

### 1.4 PtyLifecycle

**Role:** Lifecycle management and exit observation

**Swift Protocol:**
```swift
protocol PtyLifecycle {
    func kill() throws
    var onExit: (@Sendable (Int32) -> Void)? { get set }
    var isAlive: Bool { get }
}
```

**Semantics:**
- `kill()`: Forcefully terminate the session (SIGKILL on Unix, TerminateProcess on Windows)
- `onExit`: Closure called **exactly once** when process exits naturally or via kill
  - Parameter: Exit code (0–255 on Unix, arbitrary on Windows)
  - Must fire even if kill() was called
- `isAlive`: Returns `true` if process running, `false` after exit

**Threading:** 
- `kill()`: Main thread / main actor only
- `onExit`: Callback may be on background thread; implementation must handle thread-safety
- `isAlive`: Any thread

**Error Contract:**
- `kill()` throws if already dead or kill fails
- Setting `onExit` after process exited is undefined behavior

---

### 1.5 PtySession

**Role:** Combined interface for a spawned PTY

**Swift Protocol:**
```swift
protocol PtySession: PtyWriting, PtyResizing, PtyLifecycle {
    var pid: Int32 { get }
}
```

**Semantics:**
- `pid`: Process ID of the spawned shell
- Combines all PTY interfaces into one session handle

**Threading:** As per constituent interfaces

---

## 2. TerminalSurface

**Role:** Wraps the terminal emulator (SwiftTerm, Windows Terminal control, VTE) for rendering, input, and golden tests

**Swift Protocol:**
```swift
protocol TerminalSurface {
    func feed(_ data: Data)
    func getCellMetrics() -> CellMetrics
    func getCell(col: Int, row: Int) -> Cell?
    var cols: Int { get }
    var rows: Int { get }
    func serialize() -> Data
}
```

**Semantics:**
- `feed(data)`: Parse VT sequences and update buffer
  - Non-blocking; buffers if needed
  - Must handle ANSI, 256-color, truecolor, OSC sequences
- `getCellMetrics()`: Returns cell width/height in pixels (for layout calculations)
- `getCell(col, row)`: Returns cell content for golden tests
  - 0-indexed: `col` ∈ [0, cols), `row` ∈ [0, rows)
  - Returns `nil` if out of bounds
  - Cell includes: character, fg/bg colors, attributes (bold, italic, etc.)
- `cols`, `rows`: Current terminal dimensions (updated after resize)
- `serialize()`: Snapshot visible buffer state (for session restore)
  - Format: track-specific; only requirement is round-trip restore works

**Threading:** Main thread / main actor only

**Error Contract:**
- No errors thrown; parsing failures are silently handled per VT spec

---

## 3. Layout Interfaces

### 3.1 LayoutEngine

**Role:** Pure function: (pane set, preset, container size, drag deltas) → pane rectangles

**Swift Struct:**
```swift
struct LayoutEngine {
    func calculate(
        paneIds: [String],
        preset: LayoutPreset,
        containerSize: ContainerSize,
        dragDeltas: [DragDelta],
        zoomedPane: String?
    ) -> [PaneRect]
    
    func equalize(
        paneIds: [String],
        preset: LayoutPreset,
        containerSize: ContainerSize
    ) -> [PaneRect]
}
```

**Semantics:**
- **Pure logic:** No UI types (CGRect, NSRect, etc.); uses `ContainerSize` and `PaneRect` structs
- `paneIds`: Ordered list of pane identifiers
- `preset`: One of `auto`, `single`, `columns`, `rows`, `grid`, `main-left`, `main-right`
- `containerSize`: Available width/height in pixels
- `dragDeltas`: User's border adjustments (empty = default sizing)
- `zoomedPane`: If set, returns single rect filling container for that pane
- `equalize()`: Reset sizing to default (ignore drag deltas)

**Output:**
- Array of `PaneRect` with `id`, `x`, `y`, `width`, `height`
- Order matches input `paneIds`
- All coordinates in pixels, origin top-left

**Presets:**
- `auto`: 1=full, 2=columns, 3=main-left, 4+=grid
- `single`: First pane fills container
- `columns`: N vertical columns, equal width
- `rows`: N horizontal rows, equal height
- `grid`: sqrt(N) × sqrt(N) grid
- `main-left`: First pane left 50%, rest stacked right
- `main-right`: First pane right 50%, rest stacked left

**Threading:** Any (pure function, no state)

**Fixture Tests:** `contracts/fixtures/layout/*.json` define all conformance cases

---

### 3.2 ContainerSize

```swift
struct ContainerSize {
    let width: Double
    let height: Double
}
```

---

### 3.3 PaneRect

```swift
struct PaneRect {
    let id: String
    let x: Double
    let y: Double
    let width: Double
    let height: Double
}
```

---

## 4. Theme Interfaces

### 4.1 Theme

**Role:** Definition matching `contracts/themes/*.json`

**Swift Struct:**
```swift
struct Theme {
    let id: String
    let name: String
    let description: String
    let background: String      // Hex color
    let foreground: String
    let cursor: String
    let cursorAccent: String
    let selectionBackground: String
    let useDefaultAnsi: Bool    // If true, ansiColors is nil
    let ansiColors: [String]?   // 16 colors if useDefaultAnsi=false
}
```

**Semantics:**
- Colors are hex strings: `"#RRGGBB"`
- `useDefaultAnsi=true`: Do not override ANSI-16 palette (Vivid default)
- `ansiColors`: Exactly 16 colors if provided [black, red, green, yellow, blue, magenta, cyan, white, bright variants]

---

### 4.2 ThemeSource

**Role:** Resolve current theme and emit change events

**Swift Protocol:**
```swift
protocol ThemeSource {
    var currentTheme: Theme { get async }
    func loadTheme(id: String) async throws
    func availableThemes() async -> [Theme]
    func onChange(_ handler: @escaping @Sendable (Theme) -> Void) async
}
```

**Semantics:**
- `currentTheme`: Returns active theme (async for initial load)
- `loadTheme(id)`: Switch to theme by ID; throws if not found
- `availableThemes()`: List all themes from `contracts/themes/`
- `onChange(handler)`: Subscribe to theme changes; handler called on **every** surface when theme switches

**Threading:** Main thread / main actor only (handler called on main)

**Error Contract:**
- `loadTheme` throws if ID not found or JSON invalid
- All surfaces must render from the single `ThemeSource` (no per-surface palettes)

---

## 5. Persistence Interfaces

### 5.1 WorkspaceStore

**Role:** Persist and restore tabs, panes, layout, CWDs

**Swift Protocol:**
```swift
protocol WorkspaceStore {
    func save(workspace: Workspace) async throws
    func restore() async throws -> Workspace?
}
```

**Types:**
```swift
struct Workspace {
    let tabs: [Tab]
}

struct Tab {
    let id: String
    let title: String?
    let panes: [Pane]
    let preset: LayoutPreset
    let zoomedPane: String?
}

struct Pane {
    let id: String
    let shell: String
    let cwd: String
}
```

**Semantics:**
- `save()`: Debounced write to disk (JSON or similar)
- `restore()`: Returns workspace if file exists, `nil` otherwise
- **Non-destructive:** Partial load failures must not delete tabs/panes (reconcile-on-save)
- File location: `~/Library/Application Support/YOLOTerm/workspace.json` (macOS), `%LOCALAPPDATA%\YOLOTerm\workspace.json` (Windows)

**Threading:** Any (async)

**Error Contract:**
- Throws on write failure (disk full, permissions)
- Throws on restore if JSON malformed (but returns `nil` if file missing)

**Fixture Tests:** `contracts/fixtures/restore/*.json` test round-trip + partial load

---

### 5.2 OutputJournal

**Role:** Append-only raw-byte journal per pane for restore

**Swift Protocol:**
```swift
protocol OutputJournal {
    func append(paneId: String, data: Data) async throws
    func replay(paneId: String) async throws -> Data
    func purgeOrphans(activePaneIds: Set<String>) async throws
}
```

**Semantics:**
- `append(paneId, data)`: Write raw PTY output to pane's journal
  - Capped at N KiB (from scrollback setting); atomically rotates when full
  - File: `journals/<paneId>.bin`
- `replay(paneId)`: Read full journal for replay into `TerminalSurface.feed()`
- `purgeOrphans(activePaneIds)`: Delete journals not in active set (on startup)

**Threading:** Background thread / async (high-frequency writes)

**Error Contract:**
- Throws on disk full or write failure
- `replay` throws if journal missing or corrupted (silent on corruption: return empty)

---

### 5.3 HistoryStore

**Role:** Insert and search command history (SQLite + FTS5)

**Swift Protocol:**
```swift
protocol HistoryStore {
    func insert(command: HistoryCommand) async throws
    func search(query: String, scope: SearchScope) async throws -> [HistoryCommand]
}
```

**Types:**
```swift
struct HistoryCommand {
    let id: UUID
    let command: String
    let shell: String
    let cwd: String
    let paneId: String
    let exitCode: Int32?
    let duration: TimeInterval?
    let timestamp: Date
}

enum SearchScope {
    case pane(String)
    case global
}
```

**Semantics:**
- `insert()`: Add command to SQLite; redact per `contracts/fixtures/redaction.json` before insert
- `search(query, scope)`: FTS5 fuzzy search; returns matches ranked by relevance
  - `scope.pane`: Search single pane's history
  - `scope.global`: Search all history
- Schema: `contracts/schema/history.sql` (byte-identical on all tracks)

**Threading:** Any (async)

**Error Contract:**
- Throws on DB corruption or write failure
- Search never throws; returns empty on error

**Fixture Tests:** `contracts/fixtures/redaction.json` validates redaction patterns

---

## 6. Metadata Interfaces

### 6.1 PaneMetadataProvider

**Role:** Extract pane metadata (CWD, git branch, shell, SSH host)

**Swift Protocol:**
```swift
protocol PaneMetadataProvider {
    func getMetadata() async -> PaneMetadata
}
```

**Types:**
```swift
struct PaneMetadata {
    let cwd: String?
    let gitBranch: String?
    let shell: String?
    let sshHost: String?
}
```

**Semantics:**
- `cwd`: OSC 7 preferred, fallback to process introspection
- `gitBranch`: `git rev-parse --abbrev-ref HEAD` in CWD
- `shell`: Process name
- `sshHost`: Walk process tree for ssh/mosh; extract host
- Poll every ~3 seconds

**Threading:** Background (async)

**Error Contract:**
- Never throws; returns `nil` fields on error

---

### 6.2 PromptMarkParser

**Role:** Parse OSC 133 / OSC 7 for history and metadata

**Swift Struct:**
```swift
struct PromptMarkParser {
    mutating func feed(data: Data) -> [PromptMark]
}

enum PromptMark {
    case promptStart
    case commandStart
    case commandEnd(exitCode: Int32)
    case cwd(String)
}
```

**Semantics:**
- OSC 133 A/B/C/D → prompt lifecycle
- OSC 7 → CWD update
- Shared state machine for history + metadata

**Threading:** Any (pure state machine)

---

## 7. Deep Link Interface

### 7.1 DeepLinkHandler

**Role:** Handle `yoloterm://` URLs

**Swift Protocol:**
```swift
protocol DeepLinkHandler {
    func handle(url: URL) async throws
}
```

**URL Schemes:**
- `yoloterm://open?dir=/path` → Open new tab in directory

**Semantics:**
- Parse URL and perform action (new tab, focus, etc.)
- Registered at OS level (Info.plist on macOS, Registry on Windows)

**Threading:** Main thread / main actor

**Error Contract:**
- Throws if URL malformed or directory doesn't exist

---

## 8. Environment Policy

### 8.1 EnvPolicy

**Role:** Apply env var policy from `contracts/fixtures/env-policy.json`

**Swift Struct:**
```swift
struct EnvPolicy {
    let set: [String: String]
    let remove: [String]
    
    func apply(to env: [String: String], version: String) -> [String: String]
}
```

**Semantics:**
- `set`: Inject these vars (e.g., `TERM=xterm-256color`, `COLORTERM=truecolor`)
- `remove`: Scrub hostile vars (e.g., `NO_COLOR`, `FORCE_COLOR`)
- `version`: Substitute into `TERM_PROGRAM_VERSION`

**Threading:** Any (pure function)

**Fixture Tests:** `contracts/fixtures/env-policy.json` + hostile-env test

---

## 9. Cell Types (for Golden Tests)

### 9.1 Cell

```swift
struct Cell {
    let char: String
    let fg: Color?
    let bg: Color?
    let attrs: Attributes
}
```

**Attributes:**
```swift
struct Attributes: OptionSet {
    static let bold
    static let italic
    static let underline
    static let strikethrough
    static let dim
    static let inverse
}
```

---

### 9.2 Color

```swift
struct Color {
    let r: UInt8
    let g: UInt8
    let b: UInt8
}
```

---

## 10. Conformance and Testing

All interfaces must pass fixture tests:

| Fixture Suite | Interfaces Tested |
|---------------|-------------------|
| `contracts/fixtures/colors/` | `TerminalSurface` |
| `contracts/fixtures/layout/` | `LayoutEngine` |
| `contracts/fixtures/env-policy.json` | `EnvPolicy`, `PtySpawning` |
| `contracts/fixtures/redaction.json` | `HistoryStore` |
| `contracts/fixtures/restore/` | `WorkspaceStore` |

---

## 11. Version History

| Version | Date | Track A Phase | Changes |
|---------|------|---------------|---------|
| **1.0** | 2026-06-12 | M3 complete | Initial freeze after macOS proof |

---

## 12. Change Policy (Post-v1)

After this freeze:

1. Interface changes require **all active tracks** to conform in the same PR
2. Fixture changes are versioned; old fixtures stay for backward compat
3. Breaking changes require major version bump and decision-log entry
4. Track B (Windows) starts implementation against this v1 contract

---

**Ratified by:** Track A (macOS) Phase 2 completion
**Contracts v1 Freeze Date:** June 12, 2026
