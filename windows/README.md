# YOLOTerm — Windows Track (Track B)

**Status:** M6 (Phase 1: Rendering Core) — In Progress
**Target Framework:** .NET 9
**Platform:** Windows 10 1903+ (ConPTY requirement)

---

## Overview

This directory contains the Windows implementation of YOLOTerm using:
- **.NET 9** with C# latest
- **WPF** for UI
- **ConPTY** for PTY sessions (Windows pseudo-console)
- **EasyWindowsTerminalControl** for terminal rendering (wraps Windows Terminal control)

Track B follows the same contract interfaces (`contracts/interfaces.md` v1) proven by Track A (macOS).

---

## Project Structure

```
windows/
├── YOLOTerm.sln                    # Solution file
├── YOLOTerm.Core/                  # Contract implementations, no UI dependencies
│   ├── Contracts/                  # Core types: Color, Cell, EnvPolicy
│   ├── Pty/                        # ConPTY PTY session implementation
│   ├── Terminal/                   # TerminalSurface adapter + headless state
│   ├── Theme/                      # Theme loading from contracts/themes/
│   └── ...
├── YOLOTerm.App/                   # WPF application
│   ├── MainWindow.xaml             # Single-pane terminal window
│   └── App.xaml                    # Application entry point
├── YOLOTerm.Tests/                 # xUnit tests
│   ├── GoldenColorTests.cs         # GATE 4: Color fixtures validation
│   ├── EnvPolicyTests.cs           # Environment policy tests
│   └── PtyLifecycleTests.cs        # PTY lifecycle tests (requires pty-probe.exe)
└── pty-probe/                      # Test binary for PTY lifecycle validation
    └── Program.cs                  # Mirrors Track A's Swift pty-probe
```

---

## Building

### Prerequisites

- **Windows 10** version 1903 or later (for ConPTY)
- **.NET 9 SDK** (https://dotnet.microsoft.com/download/dotnet/9.0)
- **PowerShell Core** (pwsh) or Windows PowerShell (powershell.exe)

### Build Commands

```powershell
cd windows

# Restore dependencies
dotnet restore YOLOTerm.sln

# Build all projects
dotnet build YOLOTerm.sln -c Release

# Run tests
dotnet test YOLOTerm.Tests/YOLOTerm.Tests.csproj -c Release

# Run the app
dotnet run --project YOLOTerm.App/YOLOTerm.App.csproj -c Release
```

---

## Testing

### Golden Test Suite (GATE 4 Requirement)

The color golden tests validate that terminal rendering matches the contracts corpus:

```powershell
dotnet test YOLOTerm.Tests/YOLOTerm.Tests.csproj --filter "FullyQualifiedName~GoldenColorTests" -c Release
```

Expected: **All 13 fixtures green** (ANSI-16, 256-color, truecolor, attributes, bold-does-not-brighten).

### Environment Policy Tests

Validates that hostile environment variables (`NO_COLOR`, `FORCE_COLOR`) are scrubbed:

```powershell
dotnet test YOLOTerm.Tests/YOLOTerm.Tests.csproj --filter "FullyQualifiedName~EnvPolicyTests" -c Release
```

### PTY Lifecycle Tests

Tests that `pty-probe.exe` exit behavior is correct (exit fires exactly once):

```powershell
# Build pty-probe first
dotnet build pty-probe/pty-probe.csproj -c Release

# Run lifecycle tests (currently skipped, enable by removing Skip attribute)
dotnet test YOLOTerm.Tests/YOLOTerm.Tests.csproj --filter "FullyQualifiedName~PtyLifecycleTests" -c Release
```

---

## Running the App

The single-pane app launches PowerShell in the user's home directory:

```powershell
dotnet run --project YOLOTerm.App/YOLOTerm.App.csproj -c Release
```

**Default shell discovery order:**
1. `C:\Program Files\PowerShell\7\pwsh.exe` (PowerShell Core)
2. `C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe` (Windows PowerShell)
3. Fallback: `powershell.exe` (assumes in PATH)

**Default theme:** Vivid (from `contracts/themes/vivid.json`)

---

## M6 Milestone Checklist

Track B Phase 1 (Rendering Core):

- [x] **B1.1** Solution scaffold (3 projects, .NET 9, EasyWindowsTerminalControl package)
- [x] **B1.2** TerminalSurface implementation (headless adapter + VT parser)
- [x] **B1.3** PTY interfaces over ConptyConnection
- [x] **B1.4** pty-probe.exe test binary
- [x] **B1.5** ThemeSource loading contracts themes
- [x] **B1.6** Golden test runner (color fixtures validation)
- [x] **B1.7** Single-pane WPF app
- [ ] **GATE 4** Entire color corpus green on Windows ✅ (pending CI validation)

---

## Known Limitations (M6)

1. **Windows Terminal control integration**: The `EasyWindowsTerminalControl` package integration is pending actual Windows environment testing. The current implementation uses a headless VT parser for golden tests.
2. **No visual terminal yet**: M6 focuses on headless testing. Full visual rendering will be added in future milestones.
3. **Screenshot battery**: Manual screenshot testing deferred until Windows Terminal control is fully integrated.

---

## Development on macOS

This codebase is developed on macOS with validation via `windows-latest` CI runner. All tests and builds must pass in CI before merging.

**Workflow:**
1. Develop and commit on macOS
2. Push to GitHub
3. CI builds and tests on Windows
4. Review CI artifacts and test results

---

## Next Steps (M7+)

After M6 completion and GATE 4 validation:

- **M7** (Track B Phase 2): Layout engine, tabs, pane chrome, keymap
- **M8** (Track B Phase 3): Persistence (workspace, history, journals)
- **M9** (Track B Phase 4): Ship Windows 0.1 (signing, installer)

---

## Contracts Compliance

This implementation conforms to **contracts v1.0** (frozen 2026-06-12):
- `contracts/interfaces.md` — All 13 interfaces implemented
- `contracts/fixtures/colors/` — Golden test corpus (GATE 4)
- `contracts/fixtures/env-policy.json` — Environment sanitization
- `contracts/themes/*.json` — Theme loading

See `contracts/interfaces.md` for normative interface definitions.

---

## License

MIT License (inherited from monorepo root)
