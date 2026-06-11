# YOLOTerm — Product & Technical Specification

**Version:** 2.0 (draft)
**Date:** June 10, 2026
**Status:** Approved direction — multi-track native, shared contracts, core-excellence scope

---

## 1. Mission

YOLOTerm is the native successor to TermGrid: an auto-tiling terminal where
**colors, rendering, and input are correct by construction** — not by workaround.

TermGrid proved the product (tiling, tabs, restore, history). It also proved that a
WebView is the wrong substrate for a terminal. YOLOTerm keeps the product ideas and
replaces the foundation with **fully native technology per platform**, organized as
independent tracks in one codebase:

- **Track A — macOS** (Swift / AppKit / SwiftTerm / Metal) — pathfinder track
- **Track B — Windows** (C# / WPF / Windows Terminal control / ConPTY)
- **Track C — Linux** (GTK4 / VTE) — dormant until explicitly activated

Cross-platform frameworks are **not** a goal. Native functionality **is**. The
tracks share no UI code; they share **contracts**: a single set of small,
role-specific interfaces (interface segregation principle), one product spec, one
conformance fixture corpus, and one data model. Every track implements the same
interfaces 1:1 in its native language, and passes the same fixture tests.

### Guiding principles

1. **Native by default, per platform.** AppKit/Metal on macOS; WPF/DirectWrite on
   Windows; GTK4/VTE on Linux. No WebView anywhere in any rendering path.
2. **Colors are a correctness requirement, not a feature.** Truecolor, 256-color,
   and the standard ANSI-16 palette must render exactly as programs intend, with
   shared regression fixtures guarding every track.
3. **Same interfaces everywhere.** Contracts are defined once (§3); each track
   mirrors them exactly — same names, same semantics, same conformance tests.
   Interfaces are small and role-specific (ISP), never god-objects.
4. **Tracks ship independently.** macOS leads; Windows follows once contracts v1
   freeze; Linux activates on demand. No track waits on another's release train.
5. **Small, fast, boring core.** Defer everything that isn't core terminal
   excellence (adoption, relay, AI) to later phases.
6. **Every TermGrid bug becomes a YOLOTerm test.** The lessons section below is a
   test plan, not history.

---

## 2. Lessons from TermGrid (root-cause analysis)

TermGrid (Tauri 2 + SolidJS + xterm.js + portable-pty) suffered a class of failures
that all trace to one architectural decision: rendering terminals inside a WebView.

| # | TermGrid failure | Root cause | YOLOTerm answer |
|---|------------------|-----------|------------------|
| 1 | WebGL renderer painted glyph foreground colors wrong on macOS | xterm.js WebGL addon vs WKWebView quirks | Native GPU text rendering per platform; no browser GPU stack |
| 2 | Forced fallback to Canvas 2D addon (slower, still WebView) | Same | Same |
| 3 | Monochrome TUIs when launched from IDEs/CI | Inherited `NO_COLOR` / `FORCE_COLOR` env | Env-scrub policy is a shared contract (§5.4) with fixture tests on every track |
| 4 | Washed-out `ls` / git colors under themes | xterm `ITheme` remapped all 16 ANSI colors; needed `useDefaultAnsi` escape hatch | Default palette is the standard vivid ANSI-16; themes are opt-in overlays (§5.2) — shared theme JSON, one semantic |
| 5 | Remote/secondary views rendered with different renderer + hardcoded palette | Two render paths drifted | One renderer per track, one `ThemeSource` contract for every surface |
| 6 | Cell-size measurement reached into `(terminal as any)._core...` | xterm.js hides metrics | Native controls expose cell metrics; `TerminalSurface` contract requires it |
| 7 | Slow startup needed a dedicated PERFORMANCE_IMPROVEMENTS.md | WebView boot + JS bundle | Native binaries; per-track startup budgets (§6/§7) |
| 8 | PTY natural-exit tests disabled (`/bin/sh` stdin flake) | Cross-platform test harness fragility | Per-track harness with a tiny test binary we control; lifecycle tests CI-blocking on each track |
| 9 | Scrollback restore truncated vs live buffer; stale snapshot orphans | Serialize-addon snapshots diverged from prefs | Single output journal per pane (§5.6); one cap, one owner — same contract on all tracks |
| 10 | README/SPEC drift (removed features still documented) | Docs not tied to releases | Docs updated in the same PR as the feature; per-track feature/doc table checked in CI |

**Conclusion:** every color bug was either (a) the WebView renderer or (b) shell env
hygiene. (a) is eliminated by going native per platform; (b) is a shared policy
contract with tests on every track.

---

## 3. Track model & shared contracts (ISP)

### 3.1 Repository layout (one codebase, independent tracks)

```
yoloterm/
├── SPEC.md                      # this document — the product source of truth
├── contracts/                   # SHARED — the only coupling between tracks
│   ├── interfaces.md            # normative interface definitions (§3.2)
│   ├── fixtures/                # conformance corpus, consumed by every track's tests
│   │   ├── colors/              # SGR/OSC byte sequences → expected cell grids (JSON)
│   │   ├── env-policy.json      # vars to set / vars to scrub
│   │   ├── layout/              # tiling inputs → expected pane rects (JSON)
│   │   └── restore/             # workspace state round-trip cases
│   ├── themes/                  # theme definitions as JSON — one format, all tracks
│   ├── schema/history.sql       # SQLite schema + FTS5, identical on all tracks
│   └── keymap.json              # semantic actions (NewTab, SplitRight, …); chrome
│                                #   bindings are per-track (§6.4 / §7.4)
├── shared/
│   └── shell-plugins/           # OSC 133 prompt-mark plugins: zsh, bash, fish, pwsh
├── macos/                       # Track A — Swift package + Xcode project
├── windows/                     # Track B — .NET solution
└── linux/                       # Track C — dormant; activated by decision log entry
```

Rules:

- A track may only depend on `contracts/` and `shared/`. Tracks never import each
  other.
- `contracts/` changes require updating **all active tracks in the same PR** (or
  explicitly versioning the contract). This is the price of "interfaces exactly
  the same," paid where it's visible.
- Fixtures are language-neutral (JSON / raw byte files / SQL) so each track's
  native test runner consumes them directly.

### 3.2 The interface set

Interfaces are deliberately small and role-specific — the ISP split TermGrid
already validated in its Rust traits (`PtySpawner`, `PtyWriter`, `PtyReader`,
`PtyResizer`, `PtyLifecycle`, `PtyIntrospect`, `PtyExitObserver`). Each is defined
normatively in `contracts/interfaces.md` and mirrored 1:1 as a Swift `protocol`,
a C# `interface`, and (later) the Linux track's equivalent — **same names, same
method semantics, same error contracts**.

| Interface | Role (single responsibility) |
|-----------|------------------------------|
| `PtySpawning` | Spawn a shell with cwd, size, and the env policy applied |
| `PtyWriting` | Write user input bytes to a session |
| `PtyResizing` | Propagate row/col changes |
| `PtyLifecycle` | Kill, observe natural exit (fires exactly once), introspect liveness |
| `TerminalSurface` | Feed bytes, report cell metrics, select/search, serialize visible state — wraps SwiftTerm / Windows Terminal control / VTE |
| `ThemeSource` | Resolve current theme (bg/fg/cursor/selection + optional ANSI-16 overlay) for **every** surface; emits change events |
| `LayoutEngine` | Pure function: (pane set, preset, container size, drag deltas) → pane rects. No UI types. |
| `WorkspaceStore` | Persist/restore tabs, panes, layout, CWDs; non-destructive on partial load |
| `OutputJournal` | Append-only capped raw-byte journal per pane; replay on restore; orphan purge |
| `HistoryStore` | Insert/search commands (FTS5), redaction before insert |
| `PromptMarkParser` | OSC 133 / OSC 7 sniffing shared semantics for history + pane metadata |
| `PaneMetadataProvider` | cwd, git branch, shell name, SSH-descendant detection |
| `DeepLinkHandler` | `yoloterm://` URL → workspace action |

Notes:

- `LayoutEngine`, `PromptMarkParser`, redaction rules, and the env policy are
  **pure logic** — their conformance fixtures fully define behavior, so the tracks
  cannot drift on product-visible semantics.
- `TerminalSurface` is also each track's emulator escape hatch: SwiftTerm,
  the Windows Terminal control, or VTE can each be swapped (e.g. for libghostty
  later) without touching anything above the interface.

### 3.3 Conformance

- Every interface ships with fixture-driven tests in `contracts/fixtures/`.
- A track is "conformant" when all fixture suites pass in its native test runner.
  CI runs each active track's conformance job on every PR touching `contracts/`
  or that track.
- The §5.7 color suite is part of conformance — identical input bytes, identical
  expected cell colors, on macOS and Windows alike.

---

## 4. Scope

### v1 per track (core excellence — identical product surface)

- Flawless color rendering: ANSI-16, 256-color, truecolor, attributes (bold,
  italic, underline styles, dim, inverse, strikethrough)
- Auto-tiling pane layout with drag-resize, zoom, and presets
- Tabs with per-tab pane grids (native idiom per track: §6.2 / §7.2)
- Session restore: tabs, panes, layout, CWDs, scrollback replay
- Unified command history: SQLite + FTS5, fuzzy search, per-pane + global
- Shell picker — per-pane shells:
  - macOS/Linux: zsh, bash, fish, nushell, anything installed
  - Windows: PowerShell 7, Windows PowerShell, cmd, Git Bash, nushell — native,
    no WSL required (WSL shells appear as ordinary picker entries)
- Find-in-terminal, native selection/clipboard, drag-and-drop paste of files/text
- Pane labels: cwd · git branch · shell · SSH host pill
- Settings UI (theme, font, default shell, scrollback, keybindings)
- `yoloterm://` deep links + file-manager integration (Finder / Explorer)

### Explicitly deferred (Phase 5+, per track, each needs a go decision)

- Session adoption from other terminal apps
- Cross-device mirroring / relay
- AI command bar / agent mode
- Command interception hooks
- Inter-pane pipes/broadcast
- iOS/iPadOS companion (SwiftTerm supports it; not v1)

### Out of scope

- Cross-platform UI frameworks (Electron, Tauri, Avalonia, Flutter, Qt) — the
  whole point is native per platform
- Sharing UI or rendering code between tracks

---

## 5. Common product specification (all tracks)

Everything in this section is platform-neutral and enforced by shared fixtures.

### 5.1 Color correctness requirements

All MUST render correctly under each track's renderer(s):

- SGR 30–37 / 40–47 / 90–97 / 100–107 (ANSI-16 + bright)
- SGR 38;5;N / 48;5;N (256-color, standard xterm cube + grayscale ramp)
- SGR 38;2;R;G;B / 48;2;R;G;B (truecolor)
- Attributes: bold (with optional bright-mapping toggle, default **off** —
  bold stays the same color, heavier weight only), italic, dim/faint,
  inverse, strikethrough, underline (single/double/curly/dotted/dashed)
  with SGR 58 underline colors
- OSC 4 / 10 / 11 / 12 dynamic color set/query — programs that query palette
  colors (e.g. vim `bg` detection, modern TUIs) must get truthful answers
- Default ANSI-16 palette = standard vivid xterm values, **never** silently
  remapped by themes

### 5.2 Theme system

- Themes live in `contracts/themes/*.json` — one format, rendered identically by
  every track's `ThemeSource`.
- Built-ins: **Vivid (default)**, Catppuccin Mocha, Dracula, Nord, Tokyo Night,
  Gruvbox Dark — ported from TermGrid's `themes.ts`.
- A theme is: background, foreground, cursor, selection, **and optionally** a
  16-color ANSI overlay. The overlay is explicit per theme; "Vivid" defines none.
  (TermGrid's `useDefaultAnsi` lesson, baked in as the default rather than the
  escape hatch.)
- Live theme switching re-renders all panes from the single `ThemeSource` — no
  per-surface palettes, ever (TermGrid RemoteViewer lesson).
- Import: iTerm2 `.itermcolors`, Windows Terminal JSON schemes, Ghostty format —
  all normalized into the shared JSON format.
- Optional minimum-contrast setting (Ghostty-style), default off.

### 5.3 Text rendering requirements

- GPU-accelerated text rendering with a proven software/system fallback per track.
- User-selectable monospace font with ligature support; platform default
  (SF Mono / Cascadia Code / system mono).
- Crisp box-drawing/block-element rendering (custom glyph rasterization, no
  font-dependent gaps).
- Platform-native font rendering semantics (CoreText on macOS, DirectWrite on
  Windows, Pango/Cairo-or-GPU via VTE on Linux).

### 5.4 Shell environment policy (shared contract: `contracts/fixtures/env-policy.json`)

Set on every spawn:

```
TERM=xterm-256color        (POSIX tracks; informational on Windows — many
                            cross-platform CLIs honor it under ConPTY too)
COLORTERM=truecolor
TERM_PROGRAM=YOLOTerm
TERM_PROGRAM_VERSION=<app version>
```

Removed on every spawn (hostile inherited vars — TermGrid commit `444302f`):

```
NO_COLOR, FORCE_COLOR, CLICOLOR_FORCE
```

Notes:
- Do **not** set `CLICOLOR_FORCE` (corrupts piped output — TermGrid learned this).
- POSIX shells spawn as **login shells** so user dotfiles set `LSCOLORS` /
  `LS_COLORS` themselves; we don't inject opinionated values like TermGrid did.
- Windows: ConPTY itself provides VT capability; the vars above exist for the
  large population of Node/Rust/Go CLIs that gate color on them.
- A custom `yoloterm` terminfo entry is a Phase 5 consideration;
  `xterm-256color` maximizes SSH compatibility for v1.

### 5.5 Tiling layout (pure-logic contract)

Port TermGrid's proven model, specified entirely in `LayoutEngine` fixtures:

- Presets: `auto`, `single`, `columns`, `rows`, `grid`, `main-left`, `main-right`
- Auto placement: 1=full, 2=side-by-side, 3=one-left/two-right, 4=grid, …
- Drag pane borders to resize; double-click border to re-equalize
- Zoom toggle: temporarily maximize one pane
- The engine is a pure function with no UI types — identical fixture-tested
  behavior on every track; views are dumb consumers

### 5.6 Session restore & output journals

- Workspace state (tabs, panes, layout, shells, CWDs) saved via `WorkspaceStore`,
  debounced on change + on quit
- **Output journal** per pane (`OutputJournal`): append-only raw byte file,
  capped at N KiB (sized from the scrollback setting — one cap, one owner),
  atomically rotated. On restore, journal replays into the emulator before the
  new PTY attaches. Renderer-agnostic; fixes TermGrid's 10k/100k drift and
  stale-snapshot orphans
- Orphan journal purge on startup
- Restore failures must never delete tabs/panes (TermGrid `c65fef8` lesson):
  reconcile-on-save, never destructive on partial load

### 5.7 Color regression test suite (CI-blocking conformance, all tracks)

1. **Engine golden tests** — feed `contracts/fixtures/colors/` byte sequences
   into each track's headless `TerminalSurface`, assert per-cell color/attribute
   values. Covers every item in §5.1. Same fixtures, every track.
2. **Env policy tests** — spawn PTY with hostile `NO_COLOR=1` parent env, assert
   child env scrubbed (port of TermGrid's `test_hostile_color_vars_scrubbed...`).
3. **Visual acceptance checklist** (scripted, screenshot-diffed per release,
   per track):
   - 24-bit gradient script (smooth, no banding/quantization)
   - 256-color cube chart
   - `ls` colors, `git diff`, `git log --graph --oneline`
   - vim/neovim with a truecolor scheme
   - htop/btop (macOS/Linux), ntop/PowerShell `Get-Process` formatting (Windows)
   - Claude Code / other agent TUIs (the original TermGrid bug report)
4. **vttest** pass for core VT100/VT220 screens per track.

### 5.8 Unified command history

- Schema: `contracts/schema/history.sql` — TermGrid's SQLite model (command,
  shell, cwd, pane, exit code, duration, timestamp, project, favorite, note,
  redacted) + FTS5; byte-identical schema on all tracks
- Capture: OSC 133 prompt marks via `shared/shell-plugins/` (zsh, bash, fish,
  **pwsh** — PowerShell 7 supports OSC 133 prompt integration) with heuristic
  fallback via `PromptMarkParser`
- Per-pane fuzzy search + global search (bindings per track)
- Privacy: redaction patterns (`*_TOKEN`, `password=`, etc.) before insert —
  patterns are shared fixtures

### 5.9 Pane metadata

- Label bar per pane: cwd (OSC 7 with fallback) · git branch · shell name
- Host pill: gray local / yellow `ssh → user@host` when an ssh/mosh descendant is
  detected in the pane's process tree (3 s poll, same as TermGrid)

### 5.10 Semantic keymap

`contracts/keymap.json` defines **actions** (NewTab, NewPaneRight, NewPaneDown,
ClosePane, FindInTerminal, HistorySearchPane, HistorySearchGlobal,
CommandPalette, FocusPaneDirection, SelectPaneN, ZoomPane, FontBigger/Smaller/
Reset, CopySelection, Paste). Each track binds them to platform-idiomatic chrome
(§6.4, §7.4). The action set is shared; the chords are not.

### 5.11 Performance requirements (shared targets, per-track budgets in §6/§7)

| Metric | Target | TermGrid baseline |
|--------|--------|-------------------|
| Memory, 1 pane | < 60 MB | WebView floor ~150 MB+ |
| Memory, per extra pane | < 15 MB | — |
| Throughput (`cat` large file) | ≥ 100 MB/s sustained, UI responsive | bottlenecked by base64+IPC |
| Scroll/resize | display refresh rate, no tearing | 60 fps target, jank on resize |
| History search (100k rows) | < 50 ms | < 100 ms |

Synchronized output (CSI 2026) honored on every track to prevent TUI flicker.

---

## 6. Track A — macOS (pathfinder)

### 6.1 Technology stack

| Component | Choice | Rationale |
|-----------|--------|-----------|
| Language | Swift 6 (strict concurrency) | Native, matches yolowhisp precedent |
| Minimum OS | macOS 14 (Sonoma) | SwiftTerm + Metal renderer support; broad reach |
| App chrome | AppKit (NSWindow, custom tiling view) | Precise control; SwiftTerm's view is an NSView |
| Secondary UI | SwiftUI via NSHostingView (settings, welcome, palette) | Fast iteration where pixel control doesn't matter |
| Terminal emulation + view | **SwiftTerm ≥ 1.13** (MIT) | Mature VT100/xterm engine; ANSI/256/truecolor; selection, search, mouse, sixel/Kitty graphics, Kitty keyboard protocol, synchronized output; proven in La Terminal, Secure Shellfish, CodeEdit |
| Renderer | SwiftTerm **Metal GPU renderer** (`setUseMetal(true)`), CoreText fallback | Ghostty-inspired GPU engine with dirty-row caching; CoreText is the long-proven fallback |
| PTY | SwiftTerm `LocalProcessTerminalView` (forkpty-based) | Native Darwin PTY; no IPC hop, no base64 bridge |
| History DB | SQLite via **GRDB.swift** + FTS5 | Shared schema from `contracts/schema/` |
| Persistence | Codable state + journals in `~/Library/Application Support/YOLOTerm/` | Native, simple, inspectable |
| Packaging | SPM; notarized DMG via GitHub Actions | Signing + notarization from day one |
| Updates | Sparkle 2 | Standard native macOS updater |

### 6.2 Architecture

```
+----------------------------------------------------------------------+
|  YOLOTerm.app (single process, Swift)                                 |
|                                                                       |
|  NSWindow (native tab groups = our tabs)                              |
|  +------------------------------------------------------------------+|
|  | TilingView (dumb consumer of shared LayoutEngine)                 ||
|  |  +-------------------+  +-------------------+                     ||
|  |  | PaneView          |  | PaneView          |                     ||
|  |  |  TerminalView     |  |  TerminalView     |  <- SwiftTerm,      ||
|  |  |  (Metal renderer) |  |  (Metal renderer) |     PTY built in    ||
|  |  |  label bar        |  |  label bar        |                     ||
|  |  +-------------------+  +-------------------+                     ||
|  +------------------------------------------------------------------+|
|                                                                       |
|  Services (Swift actors implementing the shared interfaces)           |
|   - WorkspaceStore / OutputJournal                                    |
|   - HistoryStore (GRDB + FTS5) / PromptMarkParser                     |
|   - ThemeSource / PaneMetadataProvider / DeepLinkHandler              |
+----------------------------------------------------------------------+
```

- **No IPC boundary.** TermGrid shipped every PTY byte through
  read-thread → base64 → Tauri event → JS decode → xterm parse. Here the hot
  path is `read(2) → SwiftTerm.feed → Metal`, in-process.
- **Tabs are NSWindow tab groups.** Free native behavior: ⌘⇧[ ], tab overview,
  drag tab to new window, system appearance. (Ghostty validates this approach.)

### 6.3 Pane lifecycle

1. `PaneController` creates `LocalProcessTerminalView`, applies theme + font.
2. Spawn: user's shell as a **login shell** (`zsh -l`) with env policy (§5.4),
   starting CWD from context (inherit pane / deep link / restore).
3. Output: SwiftTerm parses in-process; a `TerminalViewDelegate` tap feeds
   `HistoryStore` (OSC 133) and the pane's `OutputJournal`.
4. Resize: TilingView layout → `TerminalView` autosizes → `TIOCSWINSZ`.
5. Exit: termination delegate fires exactly once; pane shows exit status and
   offers restart/close. **Deterministic lifecycle tests, CI-blocking — the
   tests TermGrid had to disable.**

### 6.4 Input bindings (macOS chrome for the shared keymap)

⌘ shortcuts never collide with terminal control codes — the Ctrl+C copy-vs-SIGINT
problem doesn't exist on this track:

| Action | Shortcut |
|--------|----------|
| Copy / Paste | ⌘C / ⌘V (copy-on-select optional) |
| New tab / pane | ⌘T / ⌘D (split right), ⌘⇧D (split down) |
| Close pane / tab | ⌘W / ⌘⇧W |
| Find in terminal | ⌘F (SwiftTerm built-in find bar) |
| History search | ⌃R (pane) / ⌘⇧R (global) |
| Command palette | ⌘⇧P |
| Pane navigation | ⌘⌥arrows; ⌘1–9 select pane |
| Zoom pane | ⌘⏎ |
| Font size | ⌘+/− / ⌘0 |

Plus: full menu bar with every action, Services integration, drag-and-drop of
files/folders/text pastes escaped paths (TermGrid `fccc4cf` feature).

### 6.5 OS integration & distribution

- `yoloterm://open?dir=...` deep links; Finder "New YOLOTerm Tab Here" Service;
  Dock menu of recent directories
- Signed + notarized from the first public build (Developer ID); Sparkle 2
  updates; GitHub Actions build/sign/notarize/staple; Homebrew cask once stable
- Startup budget: **< 300 ms** cold start → first prompt; bundle < 20 MB

### 6.6 Track A phases

1. **Rendering core (the proof).** Single window, single pane, SwiftTerm +
   Metal, login-shell spawn, env policy, ThemeSource, fonts. **Exit criterion:
   entire §5.7 color suite green; Claude Code TUI renders in full color.**
2. **Grid & tabs.** TilingView + LayoutEngine, native window tabs, pane labels,
   keymap + menu bar, find-in-terminal. **Contracts v1 freeze at the end of
   this phase** — interfaces extracted and ratified into `contracts/`.
3. **Persistence.** WorkspaceStore, OutputJournal, HistoryStore + shell
   plugins, search, settings UI.
4. **Polish & ship.** Deep links, Finder service, theme import, copy-on-select,
   drag-and-drop paste, signing/Sparkle, visual test harness, docs.

---

## 7. Track B — Windows

### 7.1 Technology stack

| Component | Choice | Rationale |
|-----------|--------|-----------|
| Language | C# / .NET 9 | Native Windows toolchain; productive; first-class ConPTY interop |
| Minimum OS | Windows 10 1903+ (ConPTY), Windows 11 styling | ConPTY floor; Mica/Win11 chrome where available |
| App chrome | **WPF** with Windows 11 visual styling | Mature HwndHost path required by the terminal control; WinUI 3 lacks HwndHost and its terminal-control story is alpha (re-evaluate later) |
| Terminal emulation + view | **Windows Terminal WPF control** (`Microsoft.Terminal.Wpf`, consumed via the maintained `EasyWindowsTerminalControl` NuGet, or vendored from microsoft/terminal) | The literal Windows Terminal renderer: AtlasEngine/DxEngine, DirectWrite, full 24-bit color, mouse, GPU-accelerated — same engine Visual Studio embeds |
| PTY | **ConPTY** (`CreatePseudoConsole` via the control's `ConptyConnection`) | The native Windows pseudo-console; no WSL dependency |
| Renderer | DirectWrite/Direct3D (AtlasEngine) via the control | GPU text rendering with correct color by construction |
| History DB | `Microsoft.Data.Sqlite` + FTS5 | Shared schema from `contracts/schema/` |
| Persistence | JSON state + journals in `%LOCALAPPDATA%\YOLOTerm\` | Native, simple, inspectable |
| Packaging | Signed MSIX + winget; NSIS fallback for non-Store environments | Authenticode from day one (TermGrid's SmartScreen lesson) |
| Updates | MSIX/winget channel; Velopack for the NSIS channel | Standard native Windows update paths |

**Fallback/alternative (tracked):** libghostty's DirectX 11 renderer path
(community `GhosttyWin32` proves WinUI3 + SwapChainPanel feasibility). If the
Windows Terminal control's "not yet productized" status becomes a problem, the
`TerminalSurface` contract bounds the swap.

### 7.2 Architecture

```
+----------------------------------------------------------------------+
|  YOLOTerm.exe (single process, .NET 9)                                 |
|                                                                       |
|  Main window (custom Win11-style tab strip — Windows has no native    |
|  window tab groups; tab strip follows Windows Terminal conventions)   |
|  +------------------------------------------------------------------+|
|  | TilingPanel (dumb consumer of shared LayoutEngine)                ||
|  |  +-------------------+  +-------------------+                     ||
|  |  | PaneControl       |  | PaneControl       |                     ||
|  |  |  TerminalControl  |  |  TerminalControl  |  <- WT WPF control, ||
|  |  |  (AtlasEngine/DX) |  |  (AtlasEngine/DX) |     ConPTY built in ||
|  |  |  label bar        |  |  label bar        |                     ||
|  |  +-------------------+  +-------------------+                     ||
|  +------------------------------------------------------------------+|
|                                                                       |
|  Services (C# classes implementing the same shared interfaces)        |
|   - WorkspaceStore / OutputJournal                                    |
|   - HistoryStore (Microsoft.Data.Sqlite + FTS5) / PromptMarkParser    |
|   - ThemeSource / PaneMetadataProvider / DeepLinkHandler              |
+----------------------------------------------------------------------+
```

- Hot path: `ConPTY pipe → TerminalConnection → AtlasEngine`, in-process. No
  IPC, no base64, no WebView — structurally identical to Track A.
- Output tap for history/journal via the control's raw output event (the WPF
  control exposes both raw VT and plain-text streams).

### 7.3 Pane lifecycle

1. `PaneController` creates the terminal control + `ConptyConnection`, applies
   theme + font.
2. Spawn: selected shell (`pwsh.exe`, `powershell.exe`, `cmd.exe`, Git Bash,
   nushell) with env policy (§5.4), starting directory from context.
3. Output: control parses in-process; raw-output event feeds `HistoryStore`
   (OSC 133 — pwsh plugin in `shared/shell-plugins/`) and `OutputJournal`.
4. Resize: TilingPanel layout → control resize → ConPTY `ResizePseudoConsole`.
5. Exit: connection-closed event fires exactly once; pane shows exit status and
   offers restart/close. Deterministic lifecycle tests with a controlled test
   binary, CI-blocking.

### 7.4 Input bindings (Windows chrome for the shared keymap)

Windows terminals must arbitrate Ctrl+C — we adopt the Windows Terminal
convention (selection-aware copy), which TermGrid already validated:

| Action | Shortcut |
|--------|----------|
| Copy / Paste | Ctrl+C copies **iff** selection exists, else SIGINT-equivalent; Ctrl+V paste; Ctrl+Shift+C/V always copy/paste |
| New tab / pane | Ctrl+T / Ctrl+Shift+D (split right), Ctrl+Shift+E (split down) |
| Close pane / tab | Ctrl+Shift+W / Ctrl+W |
| Find in terminal | Ctrl+Shift+F |
| History search | Ctrl+R (pane) / Ctrl+Shift+R (global) |
| Command palette | Ctrl+Shift+P |
| Pane navigation | Alt+arrows; Ctrl+Alt+1–9 select pane |
| Zoom pane | Ctrl+Shift+Enter |
| Font size | Ctrl+= / Ctrl+- / Ctrl+0 |

Same semantic actions as Track A (`contracts/keymap.json`); chords follow
Windows Terminal muscle memory.

### 7.5 OS integration & distribution

- `yoloterm://` protocol registration; Explorer context menu "Open in YOLOTerm";
  Jump List of recent directories
- Authenticode-signed MSIX + winget manifest from the first public build;
  NSIS+Velopack channel for unmanaged installs
- Startup budget: **< 500 ms** cold start → first prompt (ReadyToRun/AOT
  trimming as needed); installer < 40 MB

### 7.6 Track B phases

Track B starts when **contracts v1 freeze** (end of Track A Phase 2):

1. **Rendering core (the proof).** Single window, single pane, WT WPF control +
   ConPTY, env policy, ThemeSource. **Exit criterion: §5.7 color suite green
   with the same fixtures Track A passes; Claude Code TUI in full color under
   pwsh.**
2. **Grid & tabs.** TilingPanel consuming LayoutEngine fixtures, Win11 tab
   strip, pane labels, keymap.
3. **Persistence.** WorkspaceStore, OutputJournal, HistoryStore + pwsh/Git-Bash
   plugins, search, settings UI.
4. **Polish & ship.** Protocol handler, Explorer integration, theme import
   (incl. Windows Terminal schemes), signing, MSIX/winget, visual harness.

---

## 8. Track C — Linux (dormant)

Activated only by an explicit decision-log entry. Sketch so the contracts stay
honest about a third implementor:

- **UI:** GTK4 (+ libadwaita) — the native Linux desktop idiom
- **Terminal emulation + view:** **VTE** (the GNOME Terminal widget: truecolor,
  GPU rendering in recent versions, the de-facto native Linux terminal control)
- **Language:** decided at activation (C, Vala, or Rust via gtk4-rs + vte4 —
  whichever best mirrors the contracts at that time)
- **PTY:** VTE's built-in PTY handling (forkpty)
- **Distribution:** Flatpak first; X11 + Wayland both supported (VTE handles
  both — no TermGrid-style Wayland gap because v1 has no foreign-window
  introspection)
- Same contracts, same fixtures, same conformance bar.

---

## 9. Settings & configuration (per track, shared semantics)

- Native settings UI per track (SwiftUI scene / WPF dialog): Appearance (theme,
  font, cursor), Behavior (default shell, scrollback, copy-on-select, bell),
  Keybindings (editable table over the semantic keymap), History (retention,
  redaction patterns), Advanced (renderer toggle, env policy view)
- Storage: UserDefaults / `settings.json` in `%LOCALAPPDATA%` — export/import as
  the same JSON shape on both tracks
- Per-directory overrides via `.yoloterm.json` (Phase 5)

---

## 10. Testing strategy

| Layer | Approach | Gate |
|-------|----------|------|
| Emulation/colors | Shared golden fixtures (§5.7.1) run by each track's headless runner | CI-blocking, all active tracks |
| PTY lifecycle | Deterministic spawn/exit/kill/resize per track with a controlled test binary (no `/bin/sh` flake) | CI-blocking |
| Env policy | Hostile-env scrub tests from shared fixture | CI-blocking |
| Layout engine | Shared layout fixtures vs each track's `LayoutEngine` | CI-blocking |
| History | In-memory SQLite tests against shared schema, FTS queries, redaction fixtures | CI-blocking |
| Restore | Round-trip workspace + journal replay, partial-load non-destructive tests | CI-blocking |
| Contract drift | CI job: `contracts/` change requires all active tracks' conformance green in the same PR | CI-blocking |
| Visual | Screenshot-diff checklist (§5.7.3) per track | Per-release |
| UI smoke | XCUITest (macOS) / WinAppDriver or FlaUI (Windows): launch, spawn, type, split, restore | Per-release |

---

## 11. Phased delivery (program view)

```
Track A (macOS)    : P1 render ─ P2 grid+tabs ─ P3 persist ─ P4 ship ─ P5+ ports
                                   │
                          contracts v1 freeze
                                   │
Track B (Windows)  :               P1 render ─ P2 grid+tabs ─ P3 persist ─ P4 ship
Track C (Linux)    :               (dormant — activation requires decision-log entry)
```

- macOS is the pathfinder: it discovers the right interface shapes, which are
  then ratified into `contracts/` and frozen.
- Windows implements against frozen contracts — no churn tax.
- Phase 5+ feature ports (adoption, relay, AI, hooks, inter-pane comms) are
  specified as contract extensions first, then implemented per track, each
  behind an explicit go decision.

---

## 12. Risks & mitigations

| Risk | Likelihood | Mitigation |
|------|-----------|------------|
| SwiftTerm Metal renderer is new (Mar 2026) — edge-case bugs | Medium | CoreText fallback is one toggle; both renderers in CI visual suite; upstream fixes (MIT, active maintainer) |
| `Microsoft.Terminal.Wpf` is "supported but not productized" (maintainer's words) | Medium | Consume via maintained `EasyWindowsTerminalControl` NuGet **and** pin/vendor the control build; `TerminalSurface` bounds a swap to libghostty-DX11 if needed |
| WinUI 3 pressure (WPF perceived as legacy) | Low | WPF is fully supported on .NET 9 and is what the terminal control targets; revisit WinUI 3 when its terminal-control story leaves alpha |
| Contract churn after v1 freeze taxes both tracks | Medium | Pathfinder model: freeze only after Track A Phase 2 proves the shapes; contract changes require same-PR conformance on all active tracks |
| Emulation behavior drift between SwiftTerm / WT control / VTE | Medium | Shared color + vttest fixtures define the floor; divergences become upstream bugs or documented deltas |
| Custom tiling complexity | Medium | `LayoutEngine` is pure logic with shared fixtures; views are dumb consumers on every track |
| Scope creep toward TermGrid parity | High | §4 deferred list is contractual; Phase 5 items each need an explicit go decision |
| libghostty matures and outclasses per-track emulators | Medium | `TerminalSurface` keeps the swap bounded on every track; revisit at libghostty's first stable tag |

---

## 13. Decision log

| Date | Decision | Why |
|------|----------|-----|
| 2026-06-10 | Native per platform over cross-platform Rust GPU | "As native as possible"; color correctness by construction; yolowhisp precedent |
| 2026-06-10 | v1 = core excellence; adoption/relay/AI deferred | TermGrid's SPEC overreach left phases 2–5 unbuilt while core had bugs |
| 2026-06-10 | SwiftTerm ≥1.13 with Metal renderer for Track A | libghostty API unstable (alpha); SwiftTerm proven + GPU path shipped |
| 2026-06-10 | Native NSWindow tabs on macOS | Free native behavior; validated by Ghostty |
| 2026-06-10 | Keep TermGrid env-scrub policy verbatim, as a shared contract | Hard-won fix (`444302f`); becomes a fixture test on every track |
| 2026-06-10 | Default palette = vivid ANSI-16, themes opt-in overlays | Inverts TermGrid's `useDefaultAnsi` escape hatch into the default |
| 2026-06-10 | Multi-track monorepo: macOS + Windows tracks, Linux dormant; shared ISP contracts, no shared UI code | User decision: native functionality required, cross-platform not; identical interfaces across tracks |
| 2026-06-10 | Track B = WPF + Windows Terminal control (`Microsoft.Terminal.Wpf` via EasyWindowsTerminalControl) + ConPTY | The actual Windows Terminal renderer (AtlasEngine/DirectWrite, full truecolor), embeddable today; WinUI 3 terminal story still alpha |
| 2026-06-10 | Track C sketch = GTK4 + VTE, language decided at activation | VTE is the de-facto native Linux terminal widget; no commitment until activated |
| 2026-06-10 | Pathfinder model: contracts v1 freeze after Track A Phase 2; Track B starts then | Avoid interface churn tax on the second implementor |
