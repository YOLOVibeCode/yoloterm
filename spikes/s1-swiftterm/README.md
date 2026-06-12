# S1 — SwiftTerm Metal Validation Spike

**Objective:** Validate SwiftTerm ≥1.13 Metal renderer for YOLOTerm Track A (macOS).

## What this tests

1. **Metal vs CoreText rendering** — visual comparison
2. **Color correctness** — ANSI-16, 256-color, truecolor
3. **Text attributes** — bold, italic, underline styles, dim, inverse, strikethrough
4. **Headless Terminal API** — feasibility for golden tests
5. **Raw output tap** — for history/journal capture

## Build & run

```bash
# Metal renderer (default)
swift build
swift run

# CoreText renderer
swift run SwiftTermSpike -- --coretext
```

## Color battery

Inside the running terminal:

```bash
./test-colors.sh
```

Visual inspection checklist:
- [ ] All ANSI-16 colors distinct and vivid (not washed out)
- [ ] 256-color cube shows full spectrum without gaps
- [ ] Truecolor gradient is smooth (no banding or quantization)
- [ ] Text attributes render correctly (bold, italic, underline, etc.)
- [ ] `ls -G` colors display correctly

## Manual tests

1. Run `vim` with a truecolor scheme — verify colors
2. Run `htop` or `btop` — verify TUI colors
3. Launch Claude Code CLI — **founding bug check** (must show full color, not monochrome)

## Comparison

Run twice: once with Metal, once with `--coretext`. Screenshot both and compare.

## Findings

See `FINDINGS.md` for the decision: Metal default on/off for Phase 1, any gaps to upstream or work around.
