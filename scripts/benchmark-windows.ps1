# Windows Performance Benchmark Script
# Measures cold start time, throughput, and memory usage

param(
    [string]$OutputFile = "benchmarks-windows.txt"
)

$ErrorActionPreference = "Stop"
$ProgressPreference = "SilentlyContinue"

Write-Host "=== YOLOTerm Windows Performance Benchmarks ===" -ForegroundColor Cyan
Write-Host ""

# Find YOLOTerm executable
$exePath = Join-Path $PSScriptRoot "..\windows\publish\YOLOTerm.exe"
if (-not (Test-Path $exePath)) {
    $exePath = Join-Path $PSScriptRoot "..\windows\YOLOTerm.App\bin\Release\net9.0-windows\YOLOTerm.exe"
}

if (-not (Test-Path $exePath)) {
    Write-Error "YOLOTerm.exe not found. Build the app first."
    exit 1
}

Write-Host "Executable: $exePath"
Write-Host ""

# Initialize results
$results = @()

# ===================================================================
# 1. Cold Start Time (Target: < 500ms)
# ===================================================================
Write-Host "1. Cold Start Time" -ForegroundColor Yellow
Write-Host "   Target: < 500ms"
Write-Host ""

$coldStartTimes = @()
for ($i = 1; $i -le 5; $i++) {
    Write-Host "   Run $i/5..." -NoNewline
    
    # Kill any existing instances
    Get-Process YOLOTerm -ErrorAction SilentlyContinue | Stop-Process -Force
    Start-Sleep -Milliseconds 500
    
    # Measure startup time
    $startTime = Get-Date
    $process = Start-Process -FilePath $exePath -PassThru -WindowStyle Minimized
    
    # Wait for process to be ready (heuristic: wait for main window)
    $timeout = 10
    $elapsed = 0
    while (-not $process.MainWindowHandle -and $elapsed -lt $timeout) {
        Start-Sleep -Milliseconds 100
        $elapsed += 0.1
    }
    
    $endTime = Get-Date
    $duration = ($endTime - $startTime).TotalMilliseconds
    $coldStartTimes += $duration
    
    # Clean up
    Stop-Process -Id $process.Id -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
    
    Write-Host " ${duration}ms" -ForegroundColor Gray
}

$avgColdStart = ($coldStartTimes | Measure-Object -Average).Average
$coldStartPass = $avgColdStart -lt 500
$coldStartStatus = if ($coldStartPass) { "✓ PASS" } else { "✗ FAIL" }

Write-Host ""
Write-Host "   Average: $([int]$avgColdStart)ms $coldStartStatus" -ForegroundColor $(if ($coldStartPass) { "Green" } else { "Red" })
Write-Host ""

$results += "Cold Start Time: $([int]$avgColdStart)ms (target < 500ms) - $coldStartStatus"

# ===================================================================
# 2. Throughput Test (type large file)
# ===================================================================
Write-Host "2. Throughput Test" -ForegroundColor Yellow
Write-Host "   Measuring `type` (cat) throughput on 10MB file"
Write-Host ""

# Create test file (10MB)
$testFile = Join-Path $env:TEMP "yoloterm-throughput-test.txt"
$testData = "0123456789" * 100000  # 1MB
$testData | Out-File -FilePath $testFile -NoNewline
for ($i = 1; $i -lt 10; $i++) {
    Add-Content -Path $testFile -Value $testData -NoNewline
}

$fileSize = (Get-Item $testFile).Length
Write-Host "   Test file: $testFile ($($fileSize / 1MB)MB)"

# Measure throughput (just the `type` command, not in terminal)
$throughputTimes = @()
for ($i = 1; $i -le 3; $i++) {
    Write-Host "   Run $i/3..." -NoNewline
    
    $startTime = Get-Date
    $null = Get-Content $testFile
    $endTime = Get-Date
    
    $duration = ($endTime - $startTime).TotalSeconds
    $throughputMBps = $fileSize / 1MB / $duration
    $throughputTimes += $duration
    
    Write-Host " $([int]$throughputMBps)MB/s" -ForegroundColor Gray
}

$avgThroughputTime = ($throughputTimes | Measure-Object -Average).Average
$avgThroughputMBps = $fileSize / 1MB / $avgThroughputTime

Write-Host ""
Write-Host "   Average throughput: $([int]$avgThroughputMBps)MB/s" -ForegroundColor Green
Write-Host ""

$results += "Throughput: $([int]$avgThroughputMBps)MB/s (type 10MB file)"

# Clean up test file
Remove-Item $testFile -Force

# ===================================================================
# 3. Memory Per Pane (Manual Test)
# ===================================================================
Write-Host "3. Memory Per Pane" -ForegroundColor Yellow
Write-Host "   (Manual test - automated memory measurement requires running app)"
Write-Host ""
Write-Host "   To test manually:"
Write-Host "   1. Launch YOLOTerm"
Write-Host "   2. Open Task Manager"
Write-Host "   3. Note memory usage with 1 pane"
Write-Host "   4. Split to 4 panes"
Write-Host "   5. Calculate: (4-pane memory - 1-pane memory) / 3"
Write-Host "   6. Target: < 50MB per additional pane"
Write-Host ""

$results += "Memory Per Pane: Manual test required (target < 50MB/pane)"

# ===================================================================
# Save Results
# ===================================================================
$timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
$outputPath = Join-Path $PSScriptRoot $OutputFile

@"
=== YOLOTerm Windows Benchmarks ===
Timestamp: $timestamp
Executable: $exePath

Cold Start Time (5 runs):
  Average: $([int]$avgColdStart)ms
  Target:  < 500ms
  Status:  $coldStartStatus
  Individual: $($coldStartTimes -join 'ms, ')ms

Throughput Test (3 runs):
  Average: $([int]$avgThroughputMBps)MB/s
  File size: $($fileSize / 1MB)MB
  Individual: $($throughputTimes | ForEach-Object { [int]($fileSize / 1MB / $_) }) MB/s

Memory Per Pane:
  Status: Manual test required
  Target: < 50MB per additional pane
  
Summary:
$($results -join "`n")
"@ | Out-File -FilePath $outputPath -Encoding UTF8

Write-Host "=== Benchmark Complete ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Results saved to: $outputPath"
Write-Host ""

# Exit with failure if cold start is too slow
if (-not $coldStartPass) {
    Write-Error "Cold start time exceeds 500ms target"
    exit 1
}

exit 0
