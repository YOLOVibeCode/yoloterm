# YOLOTerm Windows Performance Benchmarks

**Platform:** Windows 11 (windows-latest CI runner)
**Build:** Release, x64
**Date:** 2026-06-12 (M9 completion)

---

## Benchmark Targets (§7.5)

| Metric | Target | Rationale |
|--------|--------|-----------|
| **Cold start time** | < 500ms | Windows allows slightly more latency than macOS (< 300ms) due to .NET runtime initialization |
| **Throughput** | N/A (informational) | Measure `type` (cat) command throughput on 10MB file |
| **Memory per pane** | < 50MB | Additional memory per pane beyond base app memory |

---

## Results

### Cold Start Time

**Method:** Launch YOLOTerm.exe, measure time until main window handle is ready.

| Run | Time (ms) | Status |
|-----|-----------|--------|
| 1   | TBD       | ⏳     |
| 2   | TBD       | ⏳     |
| 3   | TBD       | ⏳     |
| 4   | TBD       | ⏳     |
| 5   | TBD       | ⏳     |
| **Average** | **TBD** | ⏳ Pending CI |

**Target:** < 500ms ✅/❌ (TBD)

**Notes:**
- Includes .NET runtime initialization
- Measured on clean process (no cached JIT)
- Windows Defender real-time protection enabled

---

### Throughput Test

**Method:** Measure `type` (Get-Content) command throughput on 10MB test file.

| Run | Throughput (MB/s) | Time (s) |
|-----|-------------------|----------|
| 1   | TBD               | TBD      |
| 2   | TBD               | TBD      |
| 3   | TBD               | TBD      |
| **Average** | **TBD MB/s** | **TBD s** |

**Baseline:** This measures PowerShell throughput, not terminal rendering. Future benchmarks will measure terminal-specific throughput with VT escape sequences.

---

### Memory Per Pane

**Method:** Manual test with Task Manager.

**Procedure:**
1. Launch YOLOTerm with 1 pane
2. Note memory usage (Working Set) in Task Manager
3. Split to 4 panes
4. Calculate: (4-pane memory - 1-pane memory) / 3

| Configuration | Memory (MB) | Notes |
|---------------|-------------|-------|
| 1 pane (baseline) | TBD | Initial terminal session |
| 4 panes (total) | TBD | After 3 splits |
| **Per additional pane** | **TBD MB** | Target: < 50MB |

**Target:** < 50MB per pane ✅/❌ (TBD)

**Notes:**
- Memory includes terminal buffer (10,000 lines scrollback)
- Does not include shell process memory (separate process)

---

## Historical Results

### v0.1.0 (Release Candidate)

**Status:** Pending CI validation

Results will be populated after GitHub Actions `windows-track.yml` runs benchmarks in CI.

---

## Benchmark Script

Automated benchmarks are executed via:
```powershell
.\scripts\benchmark-windows.ps1
```

**Output:** `benchmarks-windows.txt`

**CI Integration:** `.github/workflows/release-windows.yml` runs benchmarks on every release.

---

## Comparison with Track A (macOS)

| Metric | macOS (M5) | Windows (M9) | Delta |
|--------|------------|--------------|-------|
| Cold start | ~250ms | TBD | TBD |
| Memory/pane | ~30MB | TBD | TBD |

**Note:** Direct comparison is challenging due to different terminal control implementations (SwiftTerm vs. Windows Terminal control).

---

## Regression Policy

**CI Fail Threshold:**
- Cold start > 500ms → ❌ Build fails
- Memory > 50MB/pane → ⚠️ Warning (manual review)

**Tracking:** Benchmark results are committed to this file on each release. PRs that introduce > 20% performance regression require justification.

---

## Future Improvements

1. **GPU acceleration** — DirectX rendering (planned for M10+)
2. **Terminal throughput test** — Measure VT sequence processing (e.g., 1M escape codes/sec)
3. **Startup profiling** — Identify .NET initialization bottlenecks
4. **Memory leak detection** — Long-running session tests (24+ hours)

---

**Last Updated:** 2026-06-12 (M9 implementation)
**Next Review:** After CI validation and clean-machine testing
