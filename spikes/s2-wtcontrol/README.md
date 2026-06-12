# S2 — Windows Terminal Control Validation Spike

**Objective:** Validate Windows Terminal WPF control + ConPTY for YOLOTerm Track B.

## What this tests

1. **Windows Terminal control** availability via `EasyWindowsTerminalControl` NuGet
2. **ConPTY** shell spawning (PowerShell 7)
3. **Color correctness** — ANSI-16, 256-color via PowerShell Write-Host and ANSI escapes
4. **Raw output tap** — for history/journal capture
5. **CI feasibility** — automated screenshot validation

## Build & run locally

**Requirements:** Windows 10 1903+, .NET 9 SDK, PowerShell 7

```powershell
dotnet restore
dotnet build
dotnet run
```

Interactive mode will open a terminal window. Close when done.

## CI mode (automated screenshot)

```powershell
dotnet run -- --screenshot
```

This will:
1. Launch the terminal
2. Run a PowerShell color battery script
3. Capture a screenshot to `artifacts/screenshot-colorbattery.png`
4. Exit automatically

The screenshot is uploaded as a CI artifact for manual review.

## Color battery checklist

Visual inspection from screenshot (CI) or live window:
- [ ] ANSI-16 foreground colors render distinctly
- [ ] ANSI-16 background colors render correctly
- [ ] ANSI escape sequences (`\e[31m` etc.) work
- [ ] Bold/bright variants render (e.g., `\e[1;33m` bold yellow)
- [ ] 256-color palette sample renders

## API validation

Code inspection checks:
- [ ] `Terminal.StartTerminal(shell, args)` spawns ConPTY successfully
- [ ] `Terminal.WriteLine(text)` sends input
- [ ] Control exposes raw VT output event (for journal/history)
- [ ] Theme/palette API available
- [ ] Cell metrics accessible

## Findings

See `FINDINGS.md` for the decision: confirm §7.1 stack or flag libghostty-DX11 pivot.
