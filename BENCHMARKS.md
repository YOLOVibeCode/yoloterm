# YOLOTerm Performance Benchmarks

**Status:** Initial baseline to be established  
**Platform:** macOS  
**Last Updated:** 2026-06-12

---

## Overview

This document tracks performance metrics for YOLOTerm per SPEC §6.5 and IMPLEMENTATION_PLAN M5 A4.5.

## Performance Budgets

| Metric | Target | Status |
|--------|--------|--------|
| Cold Start | < 300ms | ⏸ To be measured |
| Cat Throughput | Baseline | ⏸ To be measured |
| Memory per Pane | Baseline | ⏸ To be measured |

---

## Cold Start Time

**Target:** < 300ms (from process spawn to first prompt)  
**Current:** To be measured  
**Method:** Launch YOLOTerm.app, measure time to first shell prompt  

Run `scripts/benchmark.sh` to establish baseline.

---

## Cat Throughput

**Target:** Establish baseline MB/s  
**Current:** To be measured  
**Method:** `cat` large file, measure bytes rendered per second  

This measures the maximum throughput for rendering streamed output.

---

## Memory Per Pane

**Target:** Establish baseline and per-pane increment  
**Current:** To be measured  
**Method:** Manual measurement via Activity Monitor  

### Expected Budget

| Panes | Expected Memory |
|-------|-----------------|
| 1     | 50-100 MB       |
| 2     | 60-120 MB       |
| 4     | 80-160 MB       |
| 8     | 120-240 MB      |

---

## Running Benchmarks

```bash
# Run automated benchmarks
./scripts/benchmark.sh

# Results will be written to this file
```

---

## CI Integration

Benchmarks run in CI via `.github/workflows/macos-track.yml`.

Regression threshold: > 20% slowdown fails the build.

---

## Notes

- Benchmarks run on Release build configuration
- Results may vary by hardware
- Manual testing required for memory measurements
- Cold start includes app launch overhead

Run benchmarks after significant changes to rendering, persistence, or startup code.
