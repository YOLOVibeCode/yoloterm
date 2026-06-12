# Color Golden Corpus — Rulings & Methodology

**Version:** 1.0.0
**Date:** June 12, 2026
**Corpus:** `color-fixtures.json`

---

## Purpose

This corpus defines **the truth** for ANSI/256/truecolor rendering correctness in YOLOTerm (all tracks). When SwiftTerm, Windows Terminal control, or VTE disagree with these fixtures, the fixtures win — engines must be investigated or worked around to match.

---

## Methodology

### 1. Fixture generation

- **Source:** `generate-corpus.py` (Python 3, no dependencies)
- **Format:** JSON array of test cases, each with:
  - `name`: human-readable description
  - `input_b64`: base64-encoded raw PTY bytes (ANSI escapes preserved)
  - `grid`: expected cell grid after parsing (minimal region, not full 80×24)
    - Each cell: `{ch: "character", fg: "#rrggbb", bg: "#rrggbb", attrs: ["bold", "italic", ...]}`

### 2. Color palette source of truth

**ANSI-16 palette:** Standard xterm vivid colors (from TermGrid `themes.ts` `DEFAULT_ANSI`, which itself matches xterm.js built-in):

| Color | Hex | Notes |
|-------|-----|-------|
| Black | `#000000` | SGR 30 / 40 |
| Red | `#cd0000` | SGR 31 / 41 |
| Green | `#00cd00` | SGR 32 / 42 |
| Yellow | `#cdcd00` | SGR 33 / 43 |
| Blue | `#0000ee` | SGR 34 / 44 |
| Magenta | `#cd00cd` | SGR 35 / 45 |
| Cyan | `#00cdcd` | SGR 36 / 46 |
| White | `#e5e5e5` | SGR 37 / 47 |
| Bright Black | `#7f7f7f` | SGR 90 / 100 |
| Bright Red | `#ff0000` | SGR 91 / 101 |
| Bright Green | `#00ff00` | SGR 92 / 102 |
| Bright Yellow | `#ffff00` | SGR 93 / 103 |
| Bright Blue | `#5c5cff` | SGR 94 / 104 |
| Bright Magenta | `#ff00ff` | SGR 95 / 105 |
| Bright Cyan | `#00ffff` | SGR 96 / 106 |
| Bright White | `#ffffff` | SGR 97 / 107 |

**256-color palette:** Standard xterm 216-color cube (16 + 36r + 6g + b, each component 0..5 mapped to 0,51,102,153,204,255) + 24-shade grayscale ramp (232-255, 8 + (n-232)*10).

**Truecolor:** RGB values used verbatim (`SGR 38;2;R;G;B`).

### 3. Cross-validation plan (not yet executed)

Per `IMPLEMENTATION_PLAN.md` M1.7, fixtures should be validated against **two independent oracles**:
1. **SwiftTerm headless `Terminal`** (Track A engine)
2. **xterm.js headless** (TermGrid's existing engine, dev-time only)

**Process:**
1. Feed each fixture's `input_b64` bytes into both engines
2. Inspect resulting cell grid (color, attrs)
3. Compare to fixture's `expected` grid
4. Investigate disagreements: is the fixture wrong, or does an engine have a bug?

**Status as of bootstrap:** Generator created; cross-validation deferred to M2 (Track A Phase 1) when SwiftTerm integration lands. Fixtures are **currently spec-based, not empirically validated**. This is acceptable for bootstrap; empirical validation gates M2.

---

## Coverage

### SPEC §5.1 requirements

| Requirement | Fixture(s) | Status |
|-------------|-----------|--------|
| SGR 30–37 / 40–47 / 90–97 / 100–107 (ANSI-16) | `ANSI-16 foreground colors`, `ANSI-16 background colors` | ✅ |
| SGR 38;5;N / 48;5;N (256-color) | `256-color palette`, `256-color grayscale ramp` | ✅ |
| SGR 38;2;R;G;B / 48;2;R;G;B (truecolor) | `Truecolor red gradient` | ✅ |
| Bold (SGR 1) | `Bold text`, `Bold + red foreground` | ✅ |
| Italic (SGR 3) | `Italic text` | ✅ |
| Underline (SGR 4, single) | `Underline text` | ✅ |
| Underline styles (double/curly/dotted/dashed) | — | ⚠️ TODO: add in Phase 1 |
| SGR 58 underline colors | — | ⚠️ TODO: add in Phase 1 |
| Strikethrough (SGR 9) | `Strikethrough text` | ✅ |
| Dim/faint (SGR 2) | `Dim/faint text` | ✅ |
| Inverse (SGR 7) | `Inverse text` | ✅ |
| Bold-is-not-bright | `Bold does NOT brighten color` | ✅ (critical TermGrid lesson) |
| OSC 4 / 10 / 11 / 12 (dynamic color query) | — | ⚠️ TODO: add in Phase 1 |
| Combined attributes | `Bold + red foreground` | ✅ (example) |

**Target breadth for Phase 1:** ~60-100 fixtures covering all bullets. Current count: 13 (bootstrap seed). Expand during M2 golden-test implementation.

---

## Rulings

### R1: Bold does NOT brighten colors

**Context:** Some terminals historically mapped `SGR 1` (bold) to bright color variants (e.g., `SGR 1;31` → bright red instead of bold normal red).

**YOLOTerm policy:** Bold is a **weight attribute**, not a color transform. `SGR 1;31` renders bold normal red (`#cd0000`), not bright red (`#ff0000`). Tracks may optionally offer a "bold-as-bright" user setting (default off), but fixtures always test the no-bright behavior.

**Evidence:** TermGrid's `useDefaultAnsi` lesson — remapping ANSI-16 broke `ls`/git color expectations. The vivid palette is sacred.

**Fixture:** `Bold does NOT brighten color (SGR 1;31 same hue as 31)`

---

### R2: Attribute precedence and reset

**Context:** When multiple SGR codes conflict or overlap, parsing order matters.

**YOLOTerm policy:**
- Attributes accumulate left-to-right: `SGR 1;3` = bold + italic
- `SGR 0` resets all (colors + attrs)
- `SGR 22` cancels bold + dim (but not italic/underline/etc.)
- `SGR 24` cancels underline only
- `SGR 27` cancels inverse only

Tracks must match this standard VT behavior. Fixtures test reset scenarios.

---

### R3: 256-color cube formula

**Context:** Different terminals have slightly different 256-color cube implementations.

**YOLOTerm policy:** Use the **standard xterm formula**:
- Indices 0-15: ANSI-16 palette (§R1 colors)
- Indices 16-231: 216-color cube: `16 + 36*r + 6*g + b`, where r,g,b ∈ {0,1,2,3,4,5} map to {0, 51, 102, 153, 204, 255}
- Indices 232-255: grayscale ramp: `gray = 8 + (n - 232) * 10`

No track may deviate from this (it's what every CLI tool expects).

**Fixture:** `256-color palette (first 16)`, `256-color grayscale ramp`

---

### R4: Truecolor fidelity

**Context:** Some terminals quantize truecolor to 256-color internally.

**YOLOTerm policy:** `SGR 38;2;R;G;B` must render the exact RGB value, not a closest-match from the 256 palette. Tracks using renderers that quantize (rare) must document the limitation in their FINDINGS.md and consider it a blocker for Phase 1 ship.

**Fixture:** `Truecolor red gradient` — smooth gradient with no banding.

---

### R5: Underline styles (future)

**Context:** Modern terminals support extended underline styles (double, curly, dotted, dashed) via `SGR 4:N` and colors via `SGR 58`.

**YOLOTerm policy:** Phase 1 fixtures test single underline (`SGR 4`). Extended styles (`SGR 4:2`, etc.) and `SGR 58` will be added after basic underline passes. Tracks may render unsupported styles as single underline (graceful degradation), but full support is the eventual goal.

**Status:** TODO — fixtures deferred to M2.

---

## Future expansion

### Additional test categories for Phase 1+

- **Underline styles:** `SGR 4:1` (single), `SGR 4:2` (double), `SGR 4:3` (curly), `SGR 4:4` (dotted), `SGR 4:5` (dashed)
- **Underline colors:** `SGR 58;5;N` / `SGR 58;2;R;G;B`
- **OSC sequences:** `OSC 4` (set ANSI color), `OSC 10/11/12` (fg/bg/cursor query), `OSC 52` (clipboard, not color but fixture-testable)
- **Edge cases:** incomplete sequences, invalid params, interleaved escapes, zero-width chars, double-width chars (CJK)
- **Reset scenarios:** `SGR 0`, `SGR 22`, `SGR 24`, etc.
- **256-color full cube sample:** beyond first 16, test mid-range and high-range indices
- **Truecolor edge cases:** (0,0,0), (255,255,255), (128,128,128)

### Cross-validation automation

M2 should add a `validate-corpus` script that:
1. Launches headless SwiftTerm `Terminal` (or WT control / VTE)
2. Feeds each fixture's `input_b64`
3. Asserts cell grid matches `expected`
4. Reports pass/fail + diffs

This becomes a CI-blocking test once Track A Phase 1 implements `TerminalSurface`.

---

## Corpus versioning

Fixtures are versioned alongside contracts. Breaking changes to fixture format or semantics bump the corpus version (currently `1.0.0`). Tracks must test against the same corpus version to be conformant.

---

## Conclusion

This corpus is **the truth** for color rendering in YOLOTerm. Engines are oracles during cross-validation, but fixtures have final say. All rulings above are normative.

Empirical validation (SwiftTerm + xterm.js cross-check) gates M2 (Track A Phase 1). Until then, fixtures are spec-derived and subject to refinement.

End of corpus rulings.
