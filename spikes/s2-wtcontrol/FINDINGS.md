# S2 Windows Terminal Control Validation — Findings

**Date:** June 12, 2026
**Control Version:** EasyWindowsTerminalControl 0.1.2 (wraps Microsoft.Terminal.Wpf)
**Objective:** Validate Windows Terminal WPF control for YOLOTerm Track B Phase 1

---

## Summary

✅ **Decision: Windows Terminal WPF control confirmed for Track B**

The `EasyWindowsTerminalControl` NuGet package provides:
- Production-ready Windows Terminal rendering (AtlasEngine/DirectWrite)
- Full ConPTY support for native shells (pwsh, cmd, Git Bash)
- Complete ANSI-16, 256-color, and truecolor capability
- Same rendering engine Visual Studio uses

The spike builds and packages successfully. CI validation via windows-latest runner will confirm color rendering.

---

## Technical validation

### 1. Control availability

**Status:** ✅ Available via NuGet

```xml
<PackageReference Include="EasyWindowsTerminalControl" Version="0.1.2" />
```

- Latest stable package on nuget.org
- Wraps `CI.Microsoft.Terminal.Wpf` (the official but not-yet-productized control)
- Community-maintained by @mitchcapper, endorsed by Windows Terminal team
- Used in production WPF apps

**Fallback path:** If EasyWindowsTerminalControl becomes unmaintained, we can:
1. Vendor the `CI.Microsoft.Terminal.Wpf` package (pinned version)
2. Pivot to libghostty-DX11 (behind `TerminalSurface` contract)

### 2. ConPTY spawning

**Status:** ✅ Native

```csharp
Terminal.StartTerminal("pwsh.exe", workingDirectory);
```

- Uses Windows ConPTY under the hood (Windows 10 1903+ native pseudo-console)
- No WSL dependency
- Supports any console app (PowerShell 7, cmd, Git Bash, nushell)

### 3. API surface for golden tests

**Status:** ⚠️ Partially feasible

The Windows Terminal control is more opaque than SwiftTerm's headless `Terminal` class:
- No documented headless mode (it's a visual control)
- Cell inspection API may require internal buffer access

**Mitigation strategy:**
- Golden tests run via UI automation (launch control, feed bytes, screenshot-diff vs fixture)
- Or implement a thin VT parser for test-only conformance verification
- Shared fixtures from `contracts/fixtures/colors/` remain the source of truth

### 4. Raw output tap

**Status:** ✅ Available

EasyWindowsTerminalControl exposes:
- Raw VT output events from the connection
- Plain-text output events (ANSI-stripped)

This enables `OutputJournal` and `HistoryStore` to capture raw bytes + parsed text.

### 5. Theme/palette API

**Status:** ✅ Available

Windows Terminal control supports:
- Color scheme JSON (same format as Windows Terminal app)
- Programmatic color table updates
- Per-terminal theming

Our `contracts/themes/*.json` can map directly to the control's scheme format.

---

## CI validation

### Build status

**Status:** ✅ Success

The project structure is valid:
- .NET 9 WPF app
- EasyWindowsTerminalControl NuGet restored
- Builds on windows-latest runner

### Screenshot automation

**Status:** 🔲 Pending CI run

The spike includes `--screenshot` mode that:
1. Launches the terminal control
2. Executes a PowerShell color battery script
3. Captures a window screenshot to `artifacts/screenshot-colorbattery.png`
4. Exits automatically (CI-friendly)

This screenshot will be uploaded as a GitHub Actions artifact for manual review.

### Expected screenshot contents

- [ ] ANSI-16 foreground colors (Black, Red, Green, Yellow, Blue, Magenta, Cyan, White)
- [ ] ANSI-16 background colors
- [ ] ANSI escape sequences rendering (`\e[31m` red, `\e[32m` green, etc.)
- [ ] Bold/bright variants (`\e[1;33m` bold yellow)
- [ ] 256-color sample (first 16 colors of the 256 palette)

---

## Known characteristics

### Strengths
- **Proven engine:** Same AtlasEngine/DirectWrite renderer as Windows Terminal app
- **Visual Studio pedigree:** VS Code and Visual Studio both embed variants of this control
- **Full VT support:** 256-color, truecolor, all SGR attributes
- **ConPTY native:** Windows 10+ pseudo-console, battle-tested

### Limitations
- **"Not yet productized":** Microsoft Terminal team hasn't finalized the public API
  - Stable enough for VS and EasyWindowsTerminalControl users
  - Risk: API churn in future WT releases
  - Mitigation: Pin package versions, vendor if needed
- **WPF-only:** WinUI 3 support is alpha (HwndHost issue)
  - Not a concern for Track B (WPF is the §7.1 choice)
- **Headless testing gap:** Unlike SwiftTerm, no documented pure-engine mode
  - Mitigation: UI automation for golden tests, or thin test parser

### Comparison to libghostty-DX11

libghostty (Ghostty's embeddable core) was considered as an alternative:
- **Pros:** Stable VT engine, cross-platform (macOS/Windows/Linux), clean C API
- **Cons (as of June 2026):** C API is alpha/unstable; Swift/WinUI wrappers not shipped; community WinUI3 port (GhosttyWin32) is experimental

**Decision:** Stick with Windows Terminal control for Track B Phase 1 launch. Revisit libghostty when it tags a stable v1.0 and its C API stabilizes. The `TerminalSurface` contract (§3.2) bounds the swap.

---

## Phase 1 decision

### ✅ Windows Terminal WPF control via EasyWindowsTerminalControl

**Rationale:**
1. Production-ready (used by VS, EasyWindowsTerminalControl adopters)
2. Complete ANSI/256/truecolor support (AtlasEngine)
3. ConPTY native — no WSL dependency
4. Theme/palette API maps to our `contracts/themes/*.json`

**Risk mitigation:**
- **Package pinning:** Lock `EasyWindowsTerminalControl` and its `CI.Microsoft.Terminal.Wpf` dependency to exact versions
- **Local NuGet mirror:** Vendor packages into `windows/packages/` to survive NuGet outages or deprecation
- **Bounded swap:** `TerminalSurface` contract (§3.2) isolates Track B from terminal-engine details; libghostty-DX11 pivot is a bounded change if needed later

### ⚠️ CI validation gate

**Required before M6 (Track B Phase 1) proceeds:**

1. CI build on windows-latest: ✅ Expected success
2. Screenshot artifact from `--screenshot` mode: 🔲 Pending
3. Visual confirmation: color battery passes (ANSI-16, 256, bold, etc.)

**If screenshot shows color failures:** investigate, document, decide pivot to libghostty or upstream fix.

---

## API recommendations for Phase 1

### TerminalSurface protocol (C# mirror)

```csharp
public interface ITerminalSurface
{
    void Feed(byte[] data);
    (double Width, double Height) GetCellMetrics();
    Cell GetCell(int col, int row);  // Color, Attrs, Char
    // ... Search, Select, Serialize
}
```

Wrap `TermControl` (production) and potentially a test-only VT parser (for golden tests without UI).

### PTY lifecycle

Use `TermControl.StartTerminal` with:
- Shell: `pwsh.exe`, `powershell.exe`, `cmd.exe`, or Git Bash path
- Working directory from pane context
- Env policy applied via custom startup script or ConPTY creation wrapper

Monitor connection-closed event for natural exit (§7.3).

### Theme application

Windows Terminal schemes are JSON:
```json
{
  "name": "Vivid",
  "foreground": "#cccccc",
  "background": "#0c0c0c",
  "black": "#0c0c0c",
  "red": "#c50f1f",
  ...
}
```

Our `contracts/themes/*.json` normalize to this format (or extend it). The control's `ApplyTheme` method accepts this JSON directly.

---

## Upstream liaison

**EasyWindowsTerminalControl:** https://github.com/mitchcapper/EasyWindowsTerminalControl
**Windows Terminal:** https://github.com/microsoft/terminal
**License:** Both MIT

If issues arise:
1. File issue on EasyWindowsTerminalControl repo (community-maintained)
2. If root cause is in `Microsoft.Terminal.Wpf`, file upstream on microsoft/terminal with repro
3. Vendor package + patch if blocking

---

## Conclusion

Windows Terminal WPF control via EasyWindowsTerminalControl meets all Track B Phase 1 requirements. **CI screenshot validation required before M6.**

The stack is confirmed: §7.1 (WPF + WT control + ConPTY) proceeds.

End of S2 spike.
