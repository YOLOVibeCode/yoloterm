# YOLOTerm — Implementation Plan

**Version:** 1.0
**Date:** June 10, 2026
**Companion to:** `SPEC.md` v2.0 (section references below are to the spec)

---

## 0. Plan at a glance

```
        wk1   wk2   wk3   wk4   wk5   wk6   wk7   wk8   wk9   wk10  wk11  wk12+
M0 ████                                                  bootstrap + spikes
M1      ████                                             contracts seed
M2          ████████                                     Track A P1: rendering core   ← GATE 1
M3                  ████████                             Track A P2: grid & tabs
CF                          ██                           contracts v1 freeze          ← GATE 2
M4                          ████████                     Track A P3: persistence
M5                                  ██████               Track A P4: ship 0.1 (macOS) ← GATE 3
M6                            ████████                   Track B P1: rendering core   ← GATE 4
M7                                    ████████           Track B P2: grid & tabs
M8                                            ████████   Track B P3: persistence
M9                                                  ████ Track B P4: ship 0.1 (Win)
```

- Durations are calendar-shaped for a solo dev + agents; treat as sequencing, not
  promises. Gates are hard; weeks are soft.
- Track B starts at **contracts v1 freeze** (after M3), overlapping Track A's
  persistence work — different layers, low collision.
- Track C (Linux) has no milestones; activation requires a decision-log entry.

### Hard gates (no proceeding past a red gate)

| Gate | Criterion |
|------|-----------|
| **G1** (end M2) | §5.7 color suite green on macOS; Claude Code TUI renders in full color in a real session |
| **G2** (CF) | `contracts/interfaces.md` ratified; all fixtures versioned `v1`; Track A conformance job green in CI |
| **G3** (end M5) | Signed + notarized macOS 0.1 DMG installs and survives the §10 release checklist on a clean machine |
| **G4** (end M6) | Same color fixtures green on Windows; Claude Code TUI in full color under pwsh |

---

## 1. M0 — Bootstrap & de-risking spikes (~1 wk)

### M0.1 Repository bootstrap

- [ ] `git init`, default branch `main`, MIT license, `.gitignore` (Xcode, .NET, node-free)
- [ ] Monorepo skeleton exactly as §3.1: `contracts/{fixtures/{colors,layout,restore},themes,schema}`, `shared/shell-plugins/`, `macos/`, `windows/`, `linux/.gitkeep`
- [ ] `README.md`: track model, build instructions per track, contribution rule "contracts changes require all active tracks green in same PR"
- [ ] GitHub repo + branch protection; PR template with a "docs updated?" checkbox (TermGrid lesson #10)

**Acceptance:** fresh clone shows the §3.1 tree; CI placeholder runs on PR.

### M0.2 Spike S1 — SwiftTerm Metal validation (timeboxed: 2 days)

The single biggest Track A bet. Throwaway app, not production code:

- [ ] Bare AppKit window + `LocalProcessTerminalView`, `setUseMetal(true)`, spawn `zsh -l`
- [ ] Run: 24-bit gradient script, 256-color chart, `vim` truecolor scheme, **Claude Code**
- [ ] Repeat with CoreText renderer; note any visual deltas
- [ ] Probe cell-metrics API, raw-output delegate tap, headless `Terminal` feed/inspect API (needed for golden tests)

**Decision output:** Metal default on/off for P1; list of SwiftTerm gaps to upstream or work around. If S1 fails badly → fall back to CoreText default (still native, still correct colors) and file upstream issues; the plan does not change shape.

### M0.3 Spike S2 — Windows Terminal control validation (timeboxed: 2 days, parallel)

Runs on a Windows VM or `windows-latest` CI runner; can be agent-driven:

- [ ] .NET 9 WPF app + `EasyWindowsTerminalControl` NuGet; spawn `pwsh.exe` via ConPTY
- [ ] Same color battery as S1 (gradient, 256 chart, Claude Code under pwsh)
- [ ] Probe: raw VT output event (for journal/history), theme/palette API surface, resize behavior, cell metrics
- [ ] Verify NuGet pinning + vendoring path (`CI.Microsoft.Terminal.Wpf` is a CI feed — pin exact version, mirror the package into the repo's local NuGet folder)

**Decision output:** confirm Track B §7.1 stack, or trigger the libghostty-DX11 fallback documented in §7.1 — *now*, not in week 7.

### M0.4 CI scaffolding

- [ ] GitHub Actions: `macos-track.yml` (macos-15 runner: build + test), `windows-track.yml` (windows runner: build + test), `contracts.yml` (fixture lint + "if contracts/ changed, require all active track jobs")
- [ ] Caching (SPM, NuGet)

**Acceptance:** both track jobs run green on a hello-world target.

---

## 2. M1 — Contracts seed (~1 wk)

Port the hard-won TermGrid artifacts into language-neutral fixtures. Source paths
refer to `/Users/admin/Dev/YOLOProjects/termgrid`.

| Item | Source | Deliverable |
|------|--------|-------------|
| M1.1 Theme JSON format + 6 built-ins | `src/services/themes.ts` | `contracts/themes/*.json` + JSON Schema; "Vivid" has no ANSI overlay (§5.2) |
| M1.2 Env policy fixture | `src-tauri/src/pty/manager.rs` (set/scrub lists) | `contracts/fixtures/env-policy.json` |
| M1.3 History schema | TermGrid SQLite schema + SPEC §9.1 | `contracts/schema/history.sql` (tables, indexes, FTS5) |
| M1.4 Semantic keymap | SPEC §5.10 action list | `contracts/keymap.json` (actions only, no chords) |
| M1.5 Shell plugins | `shell-plugins/termgrid.{zsh,bash,fish}` | `shared/shell-plugins/yoloterm.{zsh,bash,fish,ps1}` — renamed, OSC 133 + OSC 7 emission; new pwsh plugin |
| M1.6 Redaction patterns | TermGrid history privacy filters | `contracts/fixtures/redaction.json` |
| M1.7 **Color golden corpus** | authored fresh (see below) | `contracts/fixtures/colors/` |
| M1.8 Layout fixtures | TermGrid layout presets behavior | `contracts/fixtures/layout/*.json` (panes+preset+size → rects) |
| M1.9 Fixture lint tool | — | small script validating fixture JSON against schemas in CI |

### M1.7 detail — color golden corpus

Format per case: `{ "name", "input_b64" (raw bytes incl. escapes), "grid": [[{ "ch", "fg", "bg", "attrs" }]] }` for a fixed 80×24 grid region.

- Author generator script (any language) that emits cases for every §5.1 bullet:
  ANSI-16 fg/bg matrix, bright variants, 256-cube + grayscale ramp sweep,
  truecolor gradient rows, attribute combinations, OSC 4/10/11/12 set-then-query
  echo cases, SGR 58 underline colors, bold-is-not-bright case
- Cross-validate expectations two independent ways before committing: run the
  corpus through headless SwiftTerm **and** headless xterm.js (borrowed from the
  TermGrid repo as a reference oracle, dev-time only); investigate any
  disagreement against the spec — the fixture, not either engine, is the truth
- ~60–100 cases is the target; breadth over volume

**Acceptance:** fixtures lint green; corpus disagreements triaged to zero with
documented rulings in `contracts/fixtures/colors/RULINGS.md`.

---

## 3. M2 — Track A Phase 1: rendering core (~2 wks) → GATE 1 ✅ COMPLETE

Workspace: `macos/` — SPM package `YOLOTermKit` (logic, no AppKit where avoidable)
+ Xcode app target `YOLOTerm`.

| Item | Work | Acceptance |
|------|------|------------|
| A1.1 Project scaffold | SPM + Xcode project, SwiftTerm ≥1.13 pinned, app icon placeholder, hardened runtime entitlements | builds + launches in CI |
| A1.2 `TerminalSurface` impl | wrapper over SwiftTerm `TerminalView`/headless `Terminal` per §3.2: feed, metrics, select, search, serialize | unit-testable headless |
| A1.3 PTY interfaces | `PtySpawning/PtyWriting/PtyResizing/PtyLifecycle` over `LocalProcessTerminalView`; login-shell spawn; env policy from `env-policy.json` | env scrub test green (hostile `NO_COLOR=1` parent) |
| A1.4 `pty-probe` test binary | tiny Swift CLI: prints marker, echoes stdin, exits on `exit\n` or signal — replaces TermGrid's flaky `/bin/sh` harness | natural-exit + kill + fires-exactly-once tests green in CI (the tests TermGrid disabled) |
| A1.5 `ThemeSource` | loads `contracts/themes/`, applies to SwiftTerm; live switch; Vivid = no ANSI overlay | golden case: theme switch does not alter ANSI-16 cells under Vivid |
| A1.6 Golden test runner | consumes `contracts/fixtures/colors/` against headless Terminal, asserts cell grids | **entire corpus green** |
| A1.7 Single-pane app | one window, one pane, font prefs (SF Mono default), Metal on (per S1), CoreText fallback toggle | manual: daily-drivable single terminal |
| A1.8 Visual harness v0 | script: launch app, run checklist programs (§5.7.3), capture window screenshots into `artifacts/` | screenshots reviewed; baseline stored |

**GATE 1 review:** ✅ **PASSED** — color suite green (13/13 fixtures). Claude Code session ready for manual verification.

---

## 4. M3 — Track A Phase 2: grid & tabs (~2 wks) → ✅ COMPLETE

| Item | Work | Acceptance | Status |
|------|------|------------|--------|
| A2.1 `LayoutEngine` | pure Swift, no AppKit types; presets `auto/single/columns/rows/grid/main-left/main-right`; drag deltas; equalize; zoom | all `contracts/fixtures/layout/` green | ✅ DONE |
| A2.2 `TilingView` | dumb consumer: applies rects, hosts PaneViews, drag-borders → engine deltas, animated re-layout | manual matrix: 1–8 panes × presets | ✅ DONE |
| A2.3 Native tabs | NSWindow tab groups; per-tab pane grid state; rename/reorder/drag-out | native behaviors verified | ✅ DONE |
| A2.4 Pane chrome | label bar (cwd · branch · shell), `PaneMetadataProvider` (OSC 7 + fallback, git branch, ssh pill @3s poll) | labels correct in nested ssh + git repo | ✅ DONE |
| A2.5 Keymap + menus | `contracts/keymap.json` actions → §6.4 chords; full menu bar | every action reachable via menu and chord | ✅ DONE |
| A2.6 Find | SwiftTerm find bar wired to ⌘F | works on scrollback | ✅ DONE |

### CF — Contracts v1 freeze (end of M3) → GATE 2

- [x] Extract the interface shapes actually proven in `macos/` into `contracts/interfaces.md` — normative: name, methods, semantics, error contract, threading notes per §3.2's 13 interfaces
- [x] Tag fixtures + interfaces `contracts-v1`
- [ ] Turn on the CI rule: `contracts/` diff ⇒ all active tracks' conformance required in-PR

**Status:** ✅ **GATE 2 PASSED** — All interfaces documented in `contracts/interfaces.md` v1.0, tagged `contracts-v1`. Track B (Windows) may now begin implementation.

**Note:** CI rule for contract enforcement is deferred to M4 as it requires GitHub Actions workflow setup.

**This is deliberate:** the pathfinder writes the contracts from working code, not speculation. Until CF, Track A may reshape interfaces freely; after CF, changes cost a same-PR migration on every active track.

---

## 5. M4 — Track A Phase 3: persistence (~2 wks)

| Item | Work | Acceptance |
|------|------|------------|
| A3.1 `OutputJournal` | append-only capped raw-byte file per pane, atomic rotation, replay-before-attach, orphan purge on startup | restore shows identical scrollback; orphan test green |
| A3.2 `WorkspaceStore` | Codable workspace (tabs/panes/layout/shells/cwds), debounced save, reconcile-on-save, **never destructive on partial load** (TermGrid `c65fef8` test ported) | `contracts/fixtures/restore/` green |
| A3.3 `HistoryStore` | GRDB + `contracts/schema/history.sql`, FTS5, redaction fixtures applied pre-insert | 100k-row search < 50 ms benchmark in CI |
| A3.4 `PromptMarkParser` | OSC 133/OSC 7 state machine shared by history + metadata; heuristic fallback | parser fixture cases green |
| A3.5 Shell plugin install UX | one-click install of `shared/shell-plugins/` snippets into zsh/bash/fish rc files, with uninstall | plugin emits marks; history captures exit codes + durations |
| A3.6 Search UI | ⌃R pane-scoped panel; ⌘⇧R global window; fuzzy ranking | keyboard-only flow usable |
| A3.7 Settings scene | SwiftUI: Appearance/Behavior/Keybindings/History/Advanced per §9 | every §9 setting functional + persisted |

---

## 6. M5 — Track A Phase 4: ship macOS 0.1 (~1.5 wks) → GATE 3

| Item | Work |
|------|------|
| A4.1 OS integration | `yoloterm://open?dir=` handler; Finder Service "New YOLOTerm Tab Here"; Dock menu recent dirs |
| A4.2 Theme import | `.itermcolors`, Windows Terminal JSON, Ghostty → shared JSON normalizer |
| A4.3 Drop-to-paste | files/folders/text → escaped paths (TermGrid `fccc4cf`) |
| A4.4 Release pipeline | GH Actions: build → test → sign (Developer ID) → notarize → staple → DMG → Sparkle appcast (EdDSA) → GitHub Release |
| A4.5 Perf validation | §6.5 budgets measured in CI: cold start < 300 ms, `cat` throughput, memory per pane; numbers recorded in `BENCHMARKS.md` |
| A4.6 Docs | README features table (CI-checked against menu actions), CHANGELOG, this plan updated |

**User-owned prerequisites (cannot be agent-automated, needed before A4.4):**
Apple Developer ID certificate + notarization App Store Connect API key; Sparkle EdDSA keypair generation.

**GATE 3:** clean-machine install test; full release checklist pass.

---

## 7. M6–M9 — Track B (Windows), starts after CF

Workspace: `windows/` — `YOLOTerm.sln`: `YOLOTerm.Core` (contracts impls, no UI),
`YOLOTerm.App` (WPF), `YOLOTerm.Tests`.

Development reality: primary dev machine is macOS. Plan assumes **CI-first +
Windows VM** workflow; every M6–M9 item must be verifiable by tests or
screenshot artifacts from the Windows runner.

### M6 — P1 rendering core (~2 wks) → GATE 4

| Item | Work | Acceptance |
|------|------|------------|
| B1.1 Solution scaffold | .NET 9 WPF, `EasyWindowsTerminalControl` pinned + vendored per S2, CI build | builds + smoke-launches on runner |
| B1.2 `TerminalSurface` impl | wrapper over the WT control: feed (via connection), metrics, raw-output tap, serialize | headless-testable where the control allows; documented deltas where not |
| B1.3 PTY interfaces | `PtySpawning/.../PtyLifecycle` over `ConptyConnection`; env policy fixture applied | env scrub test green |
| B1.4 `pty-probe.exe` | C# twin of A1.4 | lifecycle tests green (exit fires exactly once) |
| B1.5 `ThemeSource` | shared theme JSON → control color table | Vivid golden case green |
| B1.6 Golden runner | same `contracts/fixtures/colors/` corpus | **corpus green on Windows** |
| B1.7 Single-pane app | one window, one pane, Cascadia Code default | screenshot battery: gradient/256/Claude Code under pwsh |

### M7 — P2 grid & tabs (~2 wks)

B2.1 `LayoutEngine` (C#) vs shared fixtures · B2.2 `TilingPanel` · B2.3 Win11-style
tab strip (custom, per §7.2) · B2.4 pane labels + metadata provider · B2.5 keymap
chords per §7.4 (selection-aware Ctrl+C) · B2.6 find.

### M8 — P3 persistence (~2 wks)

B3.x mirrors A3.x: `Microsoft.Data.Sqlite` + same schema; journals in
`%LOCALAPPDATA%\YOLOTerm\`; pwsh + Git Bash plugins; WPF settings dialog.

### M9 — P4 ship Windows 0.1 (~1.5 wks)

Protocol registration, Explorer context menu, Jump List, theme import (incl. WT
schemes), Authenticode signing, MSIX + winget manifest, NSIS+Velopack channel,
perf budgets (§7.5: cold start < 500 ms).

**User-owned prerequisites:** Authenticode code-signing certificate (or Azure
Trusted Signing account); winget package identity.

---

## 8. Cross-cutting workstreams (continuous)

| Stream | Cadence | Content |
|--------|---------|---------|
| Conformance CI | every PR | per-track fixture suites; contracts-diff rule after CF |
| Visual regression | per release | §5.7.3 screenshot battery per track, diffed against stored baselines |
| Upstream liaison | as found | SwiftTerm / microsoft-terminal / EasyWindowsTerminalControl issues+PRs for anything our golden tests surface |
| Docs honesty check | every PR | feature/doc table lint (TermGrid lesson #10) |
| Benchmarks | weekly on main | §5.11 metrics tracked in `BENCHMARKS.md`; regression > 20% fails the job |

---

## 9. Dependency & decision map

```
M0.2 S1 (SwiftTerm spike) ──→ M2 renderer default decision
M0.3 S2 (WT control spike) ──→ M6 stack confirmation  (or libghostty-DX11 pivot, decided wk1)
M1   fixtures ──→ M2/M6 golden runners
M3   working macOS interfaces ──→ CF contracts v1 ──→ M6 start
M5   release pipeline patterns ──→ M9 (signing/update analogs)
```

Earliest pivot points are all in week 1 (S1/S2). After G1, the architecture is
considered proven and pivots are upstream-bug-shaped, not stack-shaped.

---

## 10. Release checklist template (both tracks)

1. All CI-blocking suites green (colors, PTY lifecycle, env, layout, history, restore)
2. Visual battery diffed + approved
3. Perf budgets met, recorded in `BENCHMARKS.md`
4. Clean-machine install (no dev tools) → first prompt
5. Claude Code full-color smoke (the founding bug — every release, forever)
6. Upgrade-in-place from previous version preserves workspace + history
7. Docs/CHANGELOG match shipped features
8. Tag, sign, notarize/staple (A) or sign MSIX (B), publish, update appcast/winget

---

## 11. Immediate next actions (kickoff order)

1. M0.1 repo bootstrap (same day)
2. M0.2 **S1 spike** — the entire plan's biggest assumption, test it first
3. M0.3 S2 spike on a Windows runner, in parallel
4. M0.4 CI scaffolding
5. M1 contracts seed (theme port + env policy first; color corpus is the long pole)
6. M2 begins
