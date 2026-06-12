# YOLOTerm

Native auto-tiling terminal where **colors, rendering, and input are correct by construction** — not by workaround.

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

## What is YOLOTerm?

YOLOTerm is the native successor to [TermGrid](https://github.com/YOLOVibeCode/termgrid), built as **independent platform-native tracks** that share contracts but no UI code:

- **Track A — macOS**: Swift 6 / AppKit / SwiftTerm / Metal GPU rendering
- **Track B — Windows**: C# / .NET 9 / WPF / Windows Terminal control / ConPTY
- **Track C — Linux**: GTK4 / VTE (dormant until activated)

Each track implements the same small, role-specific interfaces ([ISP](https://en.wikipedia.org/wiki/Interface_segregation_principle)), passes the same conformance fixtures, and ships independently.

**Core features (v1):**
- Flawless truecolor/256-color/ANSI-16 rendering (GPU accelerated)
- Auto-tiling layout with presets, drag-resize, zoom
- Native tabs per platform
- Session restore with scrollback replay
- Unified command history (SQLite + FTS5)
- Per-pane shells, live theme switching
- Find-in-terminal, native clipboard, drag-and-drop paste

See [`SPEC.md`](SPEC.md) for the full product specification and [`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md) for the development roadmap.

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

## Building

### Track A (macOS)

**Requirements:** macOS 14+, Xcode 26+, Swift 6.3+

```bash
cd macos
swift build
# or open in Xcode:
open YOLOTerm.xcodeproj
```

**Run tests:**
```bash
swift test
```

### Track B (Windows)

**Requirements:** Windows 10 1903+ (ConPTY), .NET 9 SDK

```powershell
cd windows
dotnet restore
dotnet build
```

**Run tests:**
```powershell
dotnet test
```

### Conformance

All active tracks must pass the shared fixture suites:

```bash
# Fixture lint (validates JSON against schemas)
./tools/lint-fixtures

# Per-track conformance (color golden corpus, layout, env, etc.)
cd macos && swift test
cd windows && dotnet test
```

---

## Contributing

### Contracts rule

**Changes to `contracts/` require all active tracks to pass conformance in the same PR.**

This is enforced by CI once contracts v1 freeze (after Track A Phase 2). Until then, Track A may reshape interfaces freely.

### Pull request checklist

- [ ] Docs updated in the same PR as the feature (no drift)
- [ ] Tests green on all active tracks if `contracts/` changed
- [ ] Feature table in this README matches shipped functionality

See [`.github/PULL_REQUEST_TEMPLATE/default.md`](.github/PULL_REQUEST_TEMPLATE/default.md) for the full checklist.

---

## Development status

**Current milestone:** M0/M1 — Bootstrap & contracts seed

| Track | Status | Phase |
|-------|--------|-------|
| macOS | 🚧 In progress | M0: spikes + skeleton |
| Windows | 🔜 Starts after contracts v1 freeze | — |
| Linux | 💤 Dormant | — |

See [`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md) for the full timeline and gates.

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
