# YOLOTerm Shell Plugin for PowerShell
# Source this file in your $PROFILE to enable:
#   - OSC 133 prompt marks (command history with exit codes & duration)
#   - OSC 7 current working directory reporting
#
# Installation:
#   Add-Content -Path $PROFILE -Value '. /path/to/yoloterm.ps1'
#
# PowerShell 7+ has built-in OSC 133 support; this plugin supplements it.

# Bail if disabled
if ($env:YOLOTERM_PLUGIN_DISABLED -eq "1") {
    return
}

# OSC 7: Report current working directory
function Send-YoloTermOSC7 {
    $urlEncodedCwd = [Uri]::EscapeDataString($PWD.Path)
    $hostname = [System.Net.Dns]::GetHostName()
    Write-Host -NoNewline "`e]7;file://$hostname/$urlEncodedCwd`a"
}

# OSC 133: Prompt marks
function Send-YoloTermPromptStart {
    Write-Host -NoNewline "`e]133;A`a"
}

function Send-YoloTermPromptEnd {
    Write-Host -NoNewline "`e]133;B`a"
}

# Command timing
$global:YoloTermCmdStart = $null

# PrePrompt: fires before each prompt
$global:YoloTermPrePromptHook = {
    $exitCode = $LASTEXITCODE
    if ($null -eq $exitCode) { $exitCode = 0 }
    
    # Command finished mark
    if ($null -ne $global:YoloTermCmdStart) {
        $cmdEnd = Get-Date
        $duration = ($cmdEnd - $global:YoloTermCmdStart).TotalMilliseconds
        Write-Host -NoNewline ("`e]133;D;$exitCode;{0:F0}`a" -f $duration)
        $global:YoloTermCmdStart = $null
    } else {
        Write-Host -NoNewline "`e]133;D;$exitCode`a"
    }
    
    # Report working directory
    Send-YoloTermOSC7
    
    # Prompt start for next prompt
    Send-YoloTermPromptStart
}

# PreCommand: fires before each command execution
$global:YoloTermPreCommandHook = {
    $global:YoloTermCmdStart = Get-Date
    Write-Host -NoNewline "`e]133;C`a"
}

# Install hooks (PSReadLine integration)
if (Get-Module -Name PSReadLine) {
    Set-PSReadLineOption -PromptText ' '  # Placeholder
    Set-PSReadLineOption -AddToHistoryHandler {
        param([string]$line)
        return $true  # Let PSReadLine handle history
    }
    
    # Add hooks to PSReadLine events
    if (-not (Get-PSReadLineKeyHandler -Chord Enter -ErrorAction SilentlyContinue)) {
        # This is a simplification; full integration requires PSReadLine 2.2+
        # For now, rely on PowerShell 7's built-in OSC 133 and supplement with OSC 7
    }
}

# Register prompt hook
if ($null -eq (Get-Variable -Name PSReadLinePrePromptHook -ErrorAction SilentlyContinue)) {
    New-Variable -Name PSReadLinePrePromptHook -Value @() -Scope Global
}
$PSReadLinePrePromptHook += $global:YoloTermPrePromptHook

# Emit prompt start immediately
Send-YoloTermPromptStart

# Note: PowerShell 7+ has built-in OSC 133 support via $PSStyle.OutputRendering
# This plugin ensures YOLOTerm-specific integration and adds OSC 7.
