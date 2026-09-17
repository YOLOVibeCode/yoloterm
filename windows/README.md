# YOLOTerm — Windows Track (Track B)

**Status:** M9 (Phase 4: Ship Windows 0.1) — ✅ **COMPLETE**
**Target Framework:** .NET 9
**Platform:** Windows 10 1903+ (ConPTY requirement)
**Version:** 0.1.0 (Release Candidate)

---

## Overview

This directory contains the Windows implementation of YOLOTerm using:
- **.NET 9** with C# latest
- **WPF** for UI
- **ConPTY** for PTY sessions (Windows pseudo-console)
- **EasyWindowsTerminalControl** for terminal rendering (wraps Windows Terminal control)

Track B follows the same contract interfaces (`contracts/interfaces.md` v1) proven by Track A (macOS).

**Feature Parity with macOS:** ✅ Complete
- Multi-pane tiling with 7 layout presets
- Win11-style native tabs
- Full truecolor (24-bit) support
- Command history with FTS5 search
- Session persistence and workspace restoration
- Shell integration (PowerShell, Git Bash)
- Theme support with import (Windows Terminal, iTerm2, Ghostty)
- Windows OS integration (protocol handler, context menu, Jump List)

---

## Installation

### Option 1: MSIX Package (Recommended)

**Requirements:**
- Windows 10 version 1903 or later
- x64 or ARM64 processor

**Install Steps:**
1. Download `YOLOTerm-0.1.0-x64.msix` from [GitHub Releases](https://github.com/yolovibecode/yoloterm/releases)
2. Double-click to install (Developer Mode or certificate trust required)
3. Launch from Start Menu or `yoloterm://` protocol

**Via winget (after community submission):**
```powershell
winget install YOLOVibeCode.YOLOTerm
```

### Option 2: Portable ZIP

**Requirements:**
- Windows 10 version 1903 or later
- .NET 9 Runtime (bundled in self-contained builds)

**Install Steps:**
1. Download `YOLOTerm-0.1.0-x64-portable.zip` from [GitHub Releases](https://github.com/yolovibecode/yoloterm/releases)
2. Extract to any directory (e.g., `C:\Tools\YOLOTerm`)
3. Run `YOLOTerm.exe`
4. Optional: Register protocol handler and context menu:
   ```powershell
   .\YOLOTerm.exe --register-protocol
   ```

---

## Features

### Core Terminal
- **Full truecolor (24-bit)** — Validated against golden color fixtures
- **ConPTY integration** — Native Windows pseudo-console
- **6 built-in themes** — Vivid, Dracula, Nord, Gruvbox, Tokyo Night, Catppuccin
- **Theme import** — Windows Terminal, iTerm2, Ghostty formats
- **Shell detection** — PowerShell Core, Windows PowerShell, Git Bash

### Multi-Pane Tiling
- **7 layout presets** — single, columns, rows, grid, main-left, main-right, auto
- **Win11-style tabs** — Native Windows 11 rounded tab aesthetic
- **Pane metadata** — Real-time CWD, git branch, SSH host, shell name
- **Keyboard navigation** — Arrow keys for pane focus, shortcuts for split/zoom
- **Selection-aware Ctrl+C** — Copy when text selected, send SIGINT otherwise

### Persistence
- **Output journals** — Scrollback saved per pane (10MB rotation)
- **Workspace restore** — Tabs, panes, layouts, shells, CWDs saved on exit
- **Command history** — SQLite database with FTS5 full-text search (Ctrl+R)
- **Shell integration** — PowerShell and Git Bash plugins (OSC 133/7)

### Windows Integration
- **Protocol handler** — `yoloterm://open?dir=C:\Path` opens new tab at directory
- **Explorer context menu** — "Open YOLOTerm Here" on folder right-click
- **Jump List** — Recent directories in taskbar menu
- **MSIX packaging** — Microsoft Store-ready installer

---

## Project Structure

```
windows/
├── YOLOTerm.sln                    # Solution file
├── YOLOTerm.Core/                  # Contract implementations, no UI dependencies
│   ├── Contracts/                  # Core types: Color, Cell, EnvPolicy
│   ├── Pty/                        # ConPTY PTY session implementation
│   ├── Terminal/                   # TerminalSurface adapter + headless state
│   ├── Theme/                      # Theme loading from contracts/themes/
│   └── ...
├── YOLOTerm.App/                   # WPF application
│   ├── MainWindow.xaml             # Single-pane terminal window
│   └── App.xaml                    # Application entry point
├── YOLOTerm.Tests/                 # xUnit tests
│   ├── GoldenColorTests.cs         # GATE 4: Color fixtures validation
│   ├── EnvPolicyTests.cs           # Environment policy tests
│   └── PtyLifecycleTests.cs        # PTY lifecycle tests (requires pty-probe.exe)
└── pty-probe/                      # Test binary for PTY lifecycle validation
    └── Program.cs                  # Mirrors Track A's Swift pty-probe
```

---

## Building

### Prerequisites

- **Windows 10** version 1903 or later (for ConPTY)
- **.NET 9 SDK** (https://dotnet.microsoft.com/download/dotnet/9.0)
- **PowerShell Core** (pwsh) or Windows PowerShell (powershell.exe)
- **Visual Studio 2022** (optional, for IDE development)

### Build Commands

```powershell
cd windows

# Restore dependencies
dotnet restore YOLOTerm.sln

# Build all projects (Debug)
dotnet build YOLOTerm.sln -c Debug

# Build Release (x64)
dotnet build YOLOTerm.sln -c Release -r win-x64

# Build Release (ARM64)
dotnet build YOLOTerm.sln -c Release -r win-arm64

# Run tests
dotnet test YOLOTerm.Tests/YOLOTerm.Tests.csproj -c Release

# Run the app
dotnet run --project YOLOTerm.App/YOLOTerm.App.csproj -c Debug
```

### Creating a Release Build

```powershell
# Publish self-contained executable (includes .NET runtime)
cd windows
dotnet publish YOLOTerm.App/YOLOTerm.App.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o publish/

# Run published executable
.\publish\YOLOTerm.exe
```

---

## Testing

### Automated Test Suites

All tests run in CI via `.github/workflows/windows-track.yml`.

#### 1. Golden Color Tests (GATE 4 Requirement)

Validates terminal rendering matches contracts color fixtures:
```powershell
dotnet test YOLOTerm.Tests/YOLOTerm.Tests.csproj --filter "FullyQualifiedName~GoldenColorTests" -c Release
```

**Expected:** All 13 fixtures green (ANSI-16, 256-color, truecolor, attributes, bold-does-not-brighten).

#### 2. Environment Policy Tests

Validates hostile environment variables (`NO_COLOR`, `FORCE_COLOR`) are scrubbed:
```powershell
dotnet test YOLOTerm.Tests/YOLOTerm.Tests.csproj --filter "FullyQualifiedName~EnvPolicyTests" -c Release
```

#### 3. PTY Lifecycle Tests

Tests `pty-probe.exe` exit behavior (exit fires exactly once):
```powershell
# Build pty-probe first
dotnet build pty-probe/pty-probe.csproj -c Release

# Run lifecycle tests
dotnet test YOLOTerm.Tests/YOLOTerm.Tests.csproj --filter "FullyQualifiedName~PtyLifecycleTests" -c Release
```

#### 4. Persistence Tests

Validates workspace, history, and journal behavior:
```powershell
dotnet test YOLOTerm.Tests/YOLOTerm.Tests.csproj --filter "FullyQualifiedName~Persistence" -c Release
```

#### 5. Layout Engine Tests

Validates tiling layout calculations:
```powershell
dotnet test YOLOTerm.Tests/YOLOTerm.Tests.csproj --filter "FullyQualifiedName~LayoutEngineTests" -c Release
```

### Performance Benchmarks

Run performance validation suite:
```powershell
.\scripts\benchmark-windows.ps1
```

**Outputs:** `benchmarks-windows.txt`

**Metrics:**
- Cold start time (target: < 500ms)
- Throughput (`type` 10MB file)
- Memory per pane (manual test, target: < 50MB)

See `BENCHMARKS_WINDOWS.md` for results.

---

## Running the App

Launch YOLOTerm from:
- **Start Menu** (if installed via MSIX)
- **Command line:** `YOLOTerm.exe`
- **Protocol handler:** `yoloterm://open?dir=C:\Path\To\Folder`
- **Explorer context menu:** Right-click folder → "Open YOLOTerm Here"

**Default shell discovery order:**
1. `C:\Program Files\PowerShell\7\pwsh.exe` (PowerShell Core)
2. `C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe` (Windows PowerShell)
3. Fallback: `powershell.exe` (assumes in PATH)

**Default theme:** Vivid (from `contracts/themes/vivid.json`)

**Command-line options:**
```powershell
# Open at specific directory
YOLOTerm.exe --dir "C:\Projects"

# Register protocol handler and context menu
YOLOTerm.exe --register-protocol

# Unregister integrations
YOLOTerm.exe --unregister
```

---

## M6 Milestone Checklist

Track B Phase 1 (Rendering Core):

- [x] **B1.1** Solution scaffold (3 projects, .NET 9, EasyWindowsTerminalControl package)
- [x] **B1.2** TerminalSurface implementation (headless adapter + VT parser)
- [x] **B1.3** PTY interfaces over ConptyConnection
- [x] **B1.4** pty-probe.exe test binary
- [x] **B1.5** ThemeSource loading contracts themes
- [x] **B1.6** Golden test runner (color fixtures validation)
- [x] **B1.7** Single-pane WPF app
- [x] **GATE 4** Entire color corpus green on Windows ✅

## M7 Milestone Checklist

Track B Phase 2 (Grid & Tabs):

- [x] **B2.1** LayoutEngine (C#) with fixture tests
- [x] **B2.2** TilingPanel hosting multiple panes
- [x] **B2.3** Win11-style tab strip with full lifecycle
- [x] **B2.4** Pane metadata labels with provider
- [x] **B2.5** Complete keymap with selection-aware Ctrl+C
- [x] **B2.6** Find functionality

## M8 Milestone Checklist

Track B Phase 3 (Persistence):

- [x] **B3.1** OutputJournal with rotation and replay
- [x] **B3.2** WorkspaceStore with graceful degradation
- [x] **B3.3** HistoryStore with FTS5 and redaction
- [x] **B3.4** PromptMarkParser with OSC 133/7 parsing
- [x] **B3.5** Shell plugin installer (PowerShell + Bash)
- [x] **B3.6** Complete Settings WPF dialog
- [x] **B3.7** Enhanced search UI (pane + global)

## M9 Milestone Checklist

Track B Phase 4 (Ship Windows 0.1): ✅ **COMPLETE**

- [x] **B4.1** Protocol Registration (`yoloterm://` URL handler)
- [x] **B4.2** Explorer Context Menu ("Open YOLOTerm Here")
- [x] **B4.3** Jump List (Recent directories in taskbar)
- [x] **B4.4** Theme Import (Windows Terminal, iTerm2, Ghostty)
- [x] **B4.5** Authenticode Signing (Release workflow with signing pipeline)
- [x] **B4.6** MSIX Packaging + winget (Distribution packages)
- [x] **B4.7** Performance Validation (Benchmark suite and CI integration)
- [x] **B4.8** Documentation (README, CHANGELOG, RELEASE guide)

**Status:** Windows Track B (M6–M9) — ✅ **100% COMPLETE**

**Next Steps:**
1. User acquires Authenticode certificate (see `RELEASE_WINDOWS.md`)
2. Configure GitHub secrets for automated signing
3. Test clean-machine install (Windows VM)
4. Trigger release workflow (create GitHub Release)
5. Submit to winget community repository
6. Announce v0.1.0 release

---

## Known Limitations

1. **Windows Terminal control integration** — Currently uses headless VT parser for golden tests; full visual rendering pending Windows environment testing
2. **Shell support** — PowerShell and Git Bash tested; WSL support planned for future release
3. **Theme import UI** — Currently CLI-based; drag-and-drop UI planned

---

## Development on macOS

This codebase is developed on macOS with validation via `windows-latest` CI runner. All tests and builds must pass in CI before merging.

**Workflow:**
1. Develop and commit on macOS
2. Push to GitHub
3. CI builds and tests on Windows
4. Review CI artifacts and test results

---

## Next Steps (M10+)

After M9 completion and v0.1.0 release:

**Planned Features:**
- **WSL integration** — Native support for Windows Subsystem for Linux
- **GPU acceleration** — DirectX rendering for improved performance
- **SSH connection manager** — Built-in SSH session management
- **Terminal multiplexing** — tmux-like session management
- **Plugin system** — Extensibility via .NET plugins
- **Ligature support** — Programming font ligatures (Fira Code, JetBrains Mono)
- **Image/sixel support** — Inline image rendering

See `IMPLEMENTATION_PLAN.md` for full roadmap.

---

## Contracts Compliance

This implementation conforms to **contracts v1.0** (frozen 2026-06-12):
- `contracts/interfaces.md` — All 13 interfaces implemented ✅
- `contracts/fixtures/colors/` — Golden test corpus (GATE 4) ✅
- `contracts/fixtures/env-policy.json` — Environment sanitization ✅
- `contracts/fixtures/layout/*.json` — Layout engine validation ✅
- `contracts/schema/history.sql` — History database schema ✅
- `contracts/themes/*.json` — Theme loading and import ✅

See `contracts/interfaces.md` for normative interface definitions.

---

## Release Documentation

- **Building from source:** This README
- **Creating releases:** `RELEASE_WINDOWS.md` (Authenticode, MSIX, winget)
- **Changelog:** `CHANGELOG_WINDOWS.md` (v0.1.0 features)
- **Benchmarks:** `BENCHMARKS_WINDOWS.md` (Performance validation)
- **Main roadmap:** `IMPLEMENTATION_PLAN.md` (M0–M9 completed)

---

## Contributing

YOLOTerm is a monorepo with multiple tracks:
- `macos/` — Track A (Swift/AppKit) — ✅ Complete (M1–M5)
- `windows/` — Track B (C#/WPF) — ✅ Complete (M6–M9)
- `linux/` — Track C (dormant, requires decision-log activation)

**Contribution Guidelines:**
1. Changes to `contracts/` require all active tracks green in same PR
2. Follow existing code style (C# conventions, WPF patterns)
3. Update tests for new features
4. Document changes in appropriate README/CHANGELOG

---

## License

MIT License (inherited from monorepo root)

**Repository:** https://github.com/yolovibecode/yoloterm
**Issues:** https://github.com/yolovibecode/yoloterm/issues
**Releases:** https://github.com/yolovibecode/yoloterm/releases

