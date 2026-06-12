# YOLOTermKit (Track A — macOS)

Swift Package Manager package implementing the macOS track of YOLOTerm.

## Structure

- `Sources/YOLOTermKit/` — Core library (interfaces, services, models)
- `Sources/pty-probe/` — Test binary for PTY lifecycle tests
- `YOLOTermApp/Sources/` — Single-pane terminal app
- `Tests/YOLOTermKitTests/` — Test suite including GATE 1 color corpus

## Building

```bash
swift build
swift test  # Runs GATE 1 color tests
swift run YOLOTerm  # Launch app (CLI only in debug)
```

## Dependencies

- SwiftTerm 1.13.0 (exact) — Terminal emulation + Metal renderer
- GRDB.swift 6.29+ — SQLite + FTS5 for history (future milestone)

## M2 Status (Track A Phase 1: Rendering Core)

✅ **A1.1** Project scaffold — SPM package + Xcode app target  
✅ **A1.2** TerminalSurface — Headless wrapper over SwiftTerm  
✅ **A1.3** PTY interfaces — (integrated via LocalProcessTerminalView)  
✅ **A1.4** pty-probe — Swift CLI test binary  
✅ **A1.5** ThemeSource — Theme loading from `contracts/themes/`  
✅ **A1.6** Golden test runner — **GATE 1 PASSED** (13/13 color fixtures green)  
✅ **A1.7** Single-pane app — Basic AppKit app with Metal renderer  
⏸️  **A1.8** Visual harness — (deferred to manual verification)

## GATE 1: Color Rendering ✅

All 13 color fixtures pass:
- ANSI-16 foreground/background colors
- 256-color palette + grayscale ramp
- Truecolor gradients
- Text attributes (bold, italic, underline, strikethrough, dim, inverse)
- Bold does NOT brighten color

Run tests: `swift test --filter ColorGoldenTests`

## Next Steps (M3 — Track A Phase 2)

- Layout engine + tiling view
- Native NSWindow tabs
- Pane labels + metadata
- Keymap + menu bar integration
