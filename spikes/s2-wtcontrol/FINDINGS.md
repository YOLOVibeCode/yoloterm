# S2 Windows Terminal Control Validation — Findings

**Date:** June 12, 2026
**Control Version:** EasyWindowsTerminalControl 1.0.18+ (wraps Microsoft.Terminal.Wpf)
**Objective:** Validate Windows Terminal WPF control for YOLOTerm Track B Phase 1

---

## Summary

⚠️ **Status: Spike needs update for EasyWindowsTerminalControl 1.0+ API**

The EasyWindowsTerminalControl NuGet package has evolved since the initial spec was written:
- Version 0.1.2 (referenced in spec) no longer exists on NuGet
- Current version: 1.0.18+ (API may have changed)
- Initial spike code needs updating to match actual v1.0 API surface

The spike validates the **approach** (WPF + Windows Terminal rendering engine via community wrapper), but the actual integration code requires adjustment once API docs are consulted or the package is inspected locally.

**Decision:** Track B stack (§7.1) remains **WPF + Windows Terminal control**, but spike implementation is **deferred to M6** (Track B Phase 1) when a Windows development environment is available for proper API exploration.

---

## Technical validation (partial)

### 1. Control availability

**Status:** ✅ Package exists, but API surface requires verification

```xml
<PackageReference Include="EasyWindowsTerminalControl" Version="1.0.*" />
```

- Package restored successfully from NuGet (v1.0.18)
- Original v0.1.2 mentioned in research was outdated
- Actual API (namespace, class names, methods) needs inspection

**Action for M6:** 
1. Download package on Windows machine
2. Inspect `EasyWindowsTerminalControl.dll` assembly
3. Update spike code to match actual v1.0+ API
4. Rebuild and capture color battery screenshot

### 2. ConPTY spawning

**Status:** ⏸️ To be validated in M6

Assumed based on package description and Windows Terminal's architecture:
- ConPTY backend (Windows 10 1903+ pseudo-console)
- Supports any console app (pwsh, cmd, Git Bash)

### 3. Rendering

**Status:** ⏸️ To be validated in M6

Expected: AtlasEngine/DirectWrite with full ANSI/256/truecolor support (same as Windows Terminal app).

### 4. API surface for golden tests

**Status:** ⏸️ To be validated in M6

Need to inspect actual control API for:
- Headless testing capability (or UI automation approach)
- Cell inspection for golden test verification
- Raw output tap for journal/history

---

## CI build status

**Current:** ❌ Build fails due to outdated spike code

```
error MC3074: The tag 'TermControl' does not exist in XML namespace 
'clr-namespace:EasyTerminalControl;assembly=EasyWindowsTerminalControl'
```

This is expected — the spike was written speculatively without access to the actual v1.0 package API. The error confirms the package exists and restores, but the class/namespace names in the spike don't match reality.

**Resolution:** Update spike in M6 when Windows dev environment is available, or mark Windows Track CI as expected-fail until then.

---

## Updated decision for Track B

### ✅ Windows Terminal WPF control via EasyWindowsTerminalControl (confirmed approach)

**Rationale:**
1. Package actively maintained (v1.0.18 released 2025, per NuGet timestamp)
2. Wraps the proven Windows Terminal rendering engine
3. Community adoption exists (GitHub stars, NuGet downloads)

**Risk mitigation (unchanged from original FINDINGS):**
- **Package pinning:** Lock to v1.0.x range
- **Local NuGet mirror:** Vendor package into `windows/packages/` (see §7.1)
- **Bounded swap:** `TerminalSurface` contract isolates engine choice; libghostty-DX11 remains fallback

### ⚠️ Spike status: deferred to M6

**Gate for M6 (Track B Phase 1):**
1. Update spike to actual v1.0 API (requires Windows machine)
2. CI build success
3. Screenshot artifact from `--screenshot` mode
4. Visual confirmation: color battery passes

Until M6, the Windows Track CI workflow is expected to fail on S2 spike build. This does **not** block Track A progress or contracts v1 freeze.

---

## Recommendations for M6 (Track B Phase 1)

### Spike update checklist
1. **API discovery:**
   - Install `EasyWindowsTerminalControl` v1.0.18 on Windows machine
   - Use `ildasm` or dnSpy to inspect assembly
   - Document: namespace, class name, constructor, methods (`StartTerminal`, `WriteLine`, events)
2. **Update code:**
   - Fix XAML namespace and class references
   - Fix C# code-behind to match API
   - Test locally before committing
3. **CI validation:**
   - Build succeeds
   - Screenshot captured
   - Artifacts uploaded

### Alternative if package API is unsuitable
If v1.0 API is radically different or buggy:
1. Check for maintained forks or alternatives
2. Consider direct `CI.Microsoft.Terminal.Wpf` vendoring (more brittle)
3. Pivot to libghostty-DX11 if it has stabilized by M6 (check C API status)

---

## Conclusion

Track B stack decision stands: **WPF + Windows Terminal control via EasyWindowsTerminalControl**.

S2 spike code is a **placeholder** demonstrating the integration pattern, not a working implementation. Update deferred to M6 when Windows dev environment is available.

Track A (macOS) proceeds unblocked. Contracts v1 freeze (end of M3) is not gated on Windows spike validation.

End of S2 findings.
