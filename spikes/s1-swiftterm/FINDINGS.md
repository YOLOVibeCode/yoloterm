# S1 SwiftTerm Metal Validation — Findings

**Date:** June 12, 2026
**SwiftTerm Version:** 1.13.0
**Objective:** Validate SwiftTerm Metal renderer for YOLOTerm Track A Phase 1

---

## Summary

✅ **Decision: Metal renderer ON by default for Phase 1**

SwiftTerm 1.13.0 ships a production-ready Ghostty-inspired Metal GPU renderer with:
- Full ANSI-16, 256-color, and truecolor support
- All text attributes (bold, italic, underline styles, dim, inverse, strikethrough)
- Dirty-row caching for performance
- Automatic fallback to CoreText on initialization failure

The spike app builds and links successfully. Manual testing (required by user) will verify color correctness visually.

---

## Technical validation

### 1. Metal renderer availability

**Status:** ✅ Available and enabled

```swift
try terminalView.setUseMetal(true)
```

- Method exists and throws on failure (graceful fallback path)
- Renderer selection is per-view, not global
- CoreText remains the proven fallback

### 2. API surface for golden tests

**Status:** ✅ Feasible

SwiftTerm exposes:
- **Headless `Terminal` class** — UI-agnostic engine for fixture-driven tests
- `Terminal.feed(_ data: ArraySlice<UInt8>)` — feed raw PTY bytes
- `Terminal.getCharacter(col:row:)` — inspect cell content + attributes
- `Buffer` access for color/attribute inspection

This enables the §5.7 golden corpus pattern:
```swift
let terminal = Terminal(delegate: self, cols: 80, rows: 24)
terminal.feed(fixtureBytes)
let cell = terminal.getCharacter(col: 10, row: 5)
assert(cell.foreground == expectedColor)
```

### 3. Raw output tap

**Status:** ✅ Available via `TerminalViewDelegate`

```swift
func send(source: TerminalView, data: ArraySlice<UInt8>)
```

This delegate method fires on every PTY read, providing raw VT bytes before parsing — exactly what `OutputJournal` and `HistoryStore` need.

### 4. Cell metrics

**Status:** ✅ Native

```swift
let cellWidth = terminalView.cellDimension.width
let cellHeight = terminalView.cellDimension.height
```

No `(terminal as any)._core` hacks required (TermGrid lesson #6).

---

## Color rendering

### Tested configurations

**Metal renderer:**
- Build: ✅ Success
- Link: ✅ Success
- Launch: 🔲 Manual verification required (see below)

**CoreText renderer:**
- Build: ✅ Success (via `--coretext` flag)
- Link: ✅ Success
- Launch: 🔲 Manual verification required

### Color battery checklist

The following tests must be run **manually** in the spike app once launched:

```bash
./test-colors.sh
```

Expected results (per SPEC §5.1):
- [ ] All ANSI-16 colors distinct and vivid (not washed out)
- [ ] 256-color cube shows full spectrum without gaps
- [ ] Truecolor gradient is smooth (no banding/quantization)
- [ ] Text attributes render: bold, italic, single/double/curly underline, strikethrough, dim, inverse
- [ ] `ls -G` colors correct
- [ ] vim/neovim truecolor schemes render correctly
- [ ] htop/btop TUI colors correct
- [ ] **Claude Code TUI in full color** (founding bug — must verify)

### Metal vs CoreText comparison

Run the spike twice and compare:
```bash
# Terminal 1: Metal
swift run SwiftTermSpike

# Terminal 2: CoreText
swift run SwiftTermSpike -- --coretext
```

Expected: functionally identical rendering. Metal should be faster under heavy load (`cat` large file), but both should pass the color battery.

---

## Known SwiftTerm characteristics (from upstream)

### Strengths
- Fuzzed and Valgrind-tested codebase
- Ships in commercial SSH clients (La Terminal, Secure Shellfish, CodeEdit)
- Synchronized output (CSI 2026) support — prevents TUI flicker
- Kitty keyboard protocol, graphics protocols (Sixel, Kitty)
- Custom block glyph renderer for crisp box-drawing

### Metal renderer specifics (added March 2026)
- Ghostty-inspired architecture
- Per-row dirty tracking — only rebuilds changed rows
- CoreText used for glyph rasterization, Metal for compositing
- Supports all underline styles, images, strikethrough

### Potential gaps vs xterm.js
- Smaller user base than xterm.js (though proven in prod)
- Some exotic sequences may differ from xterm.js behavior

**Mitigation:** The §5.7 golden corpus will surface any divergence; fixtures are the truth, not either engine. Upstream fixes via MIT license if needed.

---

## Phase 1 decision

### ✅ Metal renderer ON by default

**Rationale:**
1. Production-ready as of v1.13.0 (3 months old)
2. Graceful fallback to CoreText on init failure (covered in spike code)
3. Performance benefit under load (dirty-row caching)
4. Avoids the CoreText-only path being the only test path

**Fallback policy:**
- User setting to toggle Metal off (per-user, persistent)
- Automatic fallback if `setUseMetal` throws
- CI visual suite runs both renderers (§M0.4, later M2)

### ⚠️ Manual verification gate

**Required before M2 (Track A Phase 1) proceeds:**

User must run the spike app and visually confirm:
1. Metal renderer: all color battery tests pass
2. CoreText renderer: all color battery tests pass
3. Claude Code TUI: renders in **full color**, not monochrome (the founding bug check)

**If any test fails:** document in this file, decide Metal-off default, file upstream SwiftTerm issue.

---

## API recommendations for Phase 1

### TerminalSurface protocol
```swift
protocol TerminalSurface {
    func feed(_ data: ArraySlice<UInt8>)
    func getCellMetrics() -> (width: CGFloat, height: CGFloat)
    func getCell(col: Int, row: Int) -> Cell  // color, attrs, char
    // ... search, select, serialize
}
```

Wrap both `LocalProcessTerminalView` (production) and headless `Terminal` (tests).

### PTY lifecycle
Use SwiftTerm's `LocalProcessTerminalView.startProcess` with:
- Shell as login shell: `/bin/zsh -l`
- Env policy applied before spawn (see `contracts/fixtures/env-policy.json`)
- `processTerminated` delegate for the natural-exit test (TermGrid lesson #8)

### Theme application
SwiftTerm `TerminalView.setColor(_ color: NSColor, at: AnsiColor)` — map `contracts/themes/*.json` → SwiftTerm's color table. Vivid theme = don't call `setColor` (use built-in palette).

---

## Upstream liaison

**SwiftTerm repo:** https://github.com/migueldeicaza/SwiftTerm
**License:** MIT
**Maintainer:** @migueldeicaza (responsive, active)

If the golden corpus surfaces behavior divergence:
1. Document in `contracts/fixtures/colors/RULINGS.md`
2. File upstream issue with fixture case attached
3. PR fix if straightforward, or document as known delta

---

## Conclusion

SwiftTerm 1.13.0 meets all Phase 1 technical requirements. Metal renderer ships by default with fallback. **User verification of color battery required before M2.**

End of S1 spike.
