# YOLOTerm

Native auto-tiling terminal where **colors, rendering, and input are correct by construction** — not by workaround.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![macOS CI](https://github.com/yourusername/yoloterm/actions/workflows/macos-track.yml/badge.svg)](https://github.com/yourusername/yoloterm/actions/workflows/macos-track.yml)

## What is YOLOTerm?

YOLOTerm is the native successor to [TermGrid](https://github.com/YOLOVibeCode/termgrid), built as **independent platform-native tracks** that share contracts but no UI code:

- **Track A — macOS**: Swift 6 / AppKit / SwiftTerm / Metal GPU rendering ✅ **v0.1.0 released**
- **Track B — Windows**: C# / .NET 9 / WPF / Windows Terminal control / ConPTY (planned)
- **Track C — Linux**: GTK4 / VTE (dormant until activated)

Each track implements the same small, role-specific interfaces ([ISP](https://en.wikipedia.org/wiki/Interface_segregation_principle)), passes the same conformance fixtures, and ships independently.

---

## Features

| Feature | macOS | Windows | Linux |
|---------|-------|---------|-------|
| **Rendering** | | | |
| True 24-bit color (16M colors) | ✅ | 🔜 | — |
| Metal/GPU acceleration | ✅ | 🔜 | — |
| Bold, italic, underline, strikethrough | ✅ | 🔜 | — |
| SGR 58 underline colors | ✅ | 🔜 | — |
| **Layout & Tabs** | | | |
| Multi-pane grid layouts | ✅ | 🔜 | — |
| 7 layout presets | ✅ | 🔜 | — |
| Drag borders to resize | ✅ | 🔜 | — |
| Zoom/unzoom panes | ✅ | 🔜 | — |
| Native tab groups | ✅ | 🔜 | — |
| **Themes** | | | |
| Built-in themes (6) | ✅ | 🔜 | — |
| Import iTerm2 themes | ✅ | 🔜 | — |
| Import Windows Terminal themes | ✅ | 🔜 | — |
| Import Ghostty themes | ✅ | 🔜 | — |
| **History & Search** | | | |
| Full-text search (FTS5) | ✅ | 🔜 | — |
| Per-pane search (⌃R) | ✅ | 🔜 | — |
| Global search (⌘⇧R) | ✅ | 🔜 | — |
| Exit code tracking | ✅ | 🔜 | — |
| Duration tracking | ✅ | 🔜 | — |
| Privacy redaction | ✅ | 🔜 | — |
| **Persistence** | | | |
| Workspace restoration | ✅ | 🔜 | — |
| Output journal | ✅ | 🔜 | — |
| Graceful degradation | ✅ | 🔜 | — |
| **Shell Integration** | | | |
| OSC 133 prompt marking | ✅ | 🔜 | — |
| OSC 7 directory tracking | ✅ | 🔜 | — |
| Git branch detection | ✅ | 🔜 | — |
| SSH host detection | ✅ | 🔜 | — |
| Plugin installer (zsh/bash/fish/pwsh) | ✅ | 🔜 | — |
| **OS Integration** | | | |
| URL handler (yoloterm://) | ✅ | 🔜 | — |
| Context menu integration | ✅ | 🔜 | — |
| Dock/taskbar menu | ✅ | 🔜 | — |
| Drag & drop files | ✅ | 🔜 | — |
| **Settings** | | | |
| Comprehensive settings UI | ✅ | 🔜 | — |
| Font selection | ✅ | 🔜 | — |
| Cursor styles | ✅ | 🔜 | — |
| Window opacity | ✅ | 🔜 | — |

See [`SPEC.md`](SPEC.md) for the full product specification and [`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md) for the development roadmap.

---

## Installation

### macOS

**System Requirements:**
- macOS 14.0 or later
- Apple Silicon or Intel processor

**Download:**
1. Download the latest DMG from [Releases](https://github.com/yourusername/yoloterm/releases)
2. Open the DMG and drag YOLOTerm to Applications
3. Launch YOLOTerm from Applications
4. On first launch, right-click and select "Open" to bypass Gatekeeper (if unsigned)

**Verify download:**
```bash
# Check SHA-256 checksum
shasum -a 256 YOLOTerm-0.1.0.dmg
```

### Building from Source

See [Building](#building-from-source) section below.

---

## Repository structure

```
yoloterm/
├── contracts/          # SHARED — interfaces, fixtures, themes, schema
│   ├── interfaces.md   # Normative interface definitions (ISP split)
│   ├── fixtures/       # Conformance corpus (colors, layout, restore)
│   ├── themes/         # Theme JSON (one format, all tracks)
│   └── schema/         # SQLite schema (history.sql)
├── shared/
│   └── shell-plugins/  # OSC 133 prompt-mark plugins (zsh, bash, fish, pwsh)
├── macos/              # Track A — Swift package + Xcode project
├── windows/            # Track B — .NET solution
├── linux/              # Track C — dormant
└── spikes/             # Throwaway validation code
```

**Coupling rule:** Tracks depend only on `contracts/` and `shared/`; tracks never import each other.

---

## Building from Source

### Track A (macOS)

**Requirements:**
- macOS 14.0 or later
- Xcode 16.0 or later
- Swift 6.0 or later

**Build steps:**

```bash
# Clone the repository
git clone https://github.com/yourusername/yoloterm.git
cd yoloterm/macos

# Build
swift build

# Run
swift run YOLOTerm

# Or open in Xcode
open Package.swift
```

**Run tests:**
```bash
cd macos
swift test
```

**Build for release:**
```bash
cd macos
swift build -c release

# App bundle at: .build/release/YOLOTerm
```

### Track B (Windows)

**Status:** Planned for v0.2.0

**Requirements:** Windows 10 1903+ (ConPTY), .NET 9 SDK

```powershell
cd windows
dotnet restore
dotnet build
```

---

## Quick Start

### First Launch

1. Launch YOLOTerm
2. The app opens with a single pane running your default shell
3. Press `⌘T` to create a new tab
4. Press `⌘D` to split right, `⌘⇧D` to split down
5. Press `⌘↩` to zoom the current pane

### Shell Integration (Optional but Recommended)

YOLOTerm includes shell plugins for enhanced functionality:

1. Open Settings (⌘,)
2. Go to "Shell Integration" tab
3. Click "Install for [your shell]"
4. Restart your terminal

**What you get:**
- Accurate command history with exit codes
- Working directory tracking (OSC 7)
- Git branch detection in pane labels
- Prompt markers (OSC 133)

### Keyboard Shortcuts

| Action | Shortcut |
|--------|----------|
| New Tab | ⌘T |
| Close Tab | ⌘W |
| Split Right | ⌘D |
| Split Down | ⌘⇧D |
| Close Pane | ⌘W |
| Zoom Pane | ⌘↩ |
| Focus Pane | ⌥↑/↓/←/→ |
| Find in Terminal | ⌘F |
| Pane Search | ⌃R |
| Global Search | ⌘⇧R |
| Settings | ⌘, |

---

## Contributing

Contributions are welcome! Please read the guidelines below.

### Development Setup

1. **Fork and clone** the repository
2. **Build** the project (see [Building from Source](#building-from-source))
3. **Run tests** to ensure everything works
4. **Create a feature branch** for your changes

### Pull Request Guidelines

**Before submitting:**

- [ ] All tests pass (`swift test` for macOS)
- [ ] Code follows Swift style guidelines
- [ ] Documentation updated in the same PR as the feature
- [ ] Performance benchmarks run if rendering/persistence changed
- [ ] Changelog updated (for features/fixes)

**Contracts rule:**

Changes to `contracts/` require all active tracks to pass conformance in the same PR. This is enforced by CI after contracts v1 freeze.

**Commit messages:**

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
feat: add theme import from iTerm2
fix: correct path escaping for drop-to-paste
docs: update installation instructions
test: add coverage for workspace restoration
```

### Testing

```bash
# Run all tests
cd macos && swift test

# Run specific test suite
swift test --filter ColorGoldenTests

# Run with coverage
swift test --enable-code-coverage
```

### Code Structure

YOLOTerm follows a clean architecture:

- **`contracts/`**: Shared interfaces, fixtures, themes, schema (platform-agnostic)
- **`shared/`**: Shell plugins, utilities
- **`macos/Sources/YOLOTermKit/`**: Core logic (testable, no AppKit where avoidable)
  - `Interfaces/`: Protocol definitions
  - `Services/`: Business logic (themes, PTY, history, etc.)
  - `Persistence/`: Storage layer
  - `Layout/`: Tiling engine
  - `Views/`: UI components
- **`macos/YOLOTermApp/`**: App target (AppDelegate, UI glue)

**Principles:**
- Small, role-specific interfaces (ISP)
- Testable by default
- Minimal coupling between tracks

### Reporting Issues

**Before opening an issue:**

1. Check [existing issues](https://github.com/yourusername/yoloterm/issues)
2. Update to the latest version
3. Try a clean install

**Include in bug reports:**

- macOS version
- YOLOTerm version
- Steps to reproduce
- Expected vs. actual behavior
- Screenshots or recordings (if applicable)

**For performance issues:**

- Run `./scripts/benchmark.sh` and attach results
- Note any specific operations that are slow

### Feature Requests

Feature requests are welcome! Please:

1. Check [existing feature requests](https://github.com/yourusername/yoloterm/labels/enhancement)
2. Provide clear use cases
3. Explain why it's needed
4. Consider if it fits the project's goals

---

## Development Status

**Current milestone:** M5 complete — macOS v0.1.0 ready for release ✅

| Track | Status | Phase | Release |
|-------|--------|-------|---------|
| macOS | ✅ Complete | M5: Ship macOS 0.1 | v0.1.0 |
| Windows | 🔜 Planned | M6-M9 | v0.2.0 |
| Linux | 💤 Dormant | — | TBD |

**Gates passed:**
- ✅ **GATE 1** (M2): Color suite green on macOS, Claude Code renders in full color
- ✅ **GATE 2** (M3): Contracts v1 frozen, all interfaces documented
- ✅ **GATE 2.5** (M4): Full persistence layer, performance targets met
- ⏸ **GATE 3** (M5): Ready for release (awaiting user-configured signing)

See [`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md) for the full timeline and [`CHANGELOG.md`](CHANGELOG.md) for release notes.

---

## Performance

YOLOTerm is designed for speed:

- **Cold start:** < 300ms (from launch to first prompt)
- **Cat throughput:** Baseline measured (see `BENCHMARKS.md`)
- **Memory per pane:** ~10-20MB incremental
- **History search:** < 7ms for 100k entries (FTS5)

Run benchmarks:
```bash
./scripts/benchmark.sh
```

See [`BENCHMARKS.md`](BENCHMARKS.md) for detailed results.

---

## Why native per platform?

TermGrid (the predecessor) proved the product but suffered color-rendering bugs that all traced to one architectural decision: rendering terminals inside a WebView (Tauri + xterm.js).

- **WebGL renderer** painted glyph foreground colors wrong on macOS
- Forced fallback to **Canvas 2D** (slower, still WebView quirks)
- **Theme remapping** washed out standard ANSI colors
- **Startup cost** required a dedicated `PERFORMANCE_IMPROVEMENTS.md`

**YOLOTerm's answer:** native GPU text rendering per platform — Metal/CoreText on macOS, DirectWrite/D3D on Windows, VTE on Linux. Colors are correct by construction because we use each platform's proven terminal substrate, not a browser.

See [`SPEC.md` §2](SPEC.md#2-lessons-from-termgrid-root-cause-analysis) for the full root-cause analysis.

---

## License

[MIT](LICENSE) © 2026 YOLOVibeCode
