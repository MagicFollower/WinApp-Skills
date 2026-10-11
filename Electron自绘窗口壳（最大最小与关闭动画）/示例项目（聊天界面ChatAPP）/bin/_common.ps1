# Shared helpers for bin/*.ps1. Dot-source it:
#   . (Join-Path $PSScriptRoot '_common.ps1')
# ASCII only: Windows PowerShell 5.1 decodes BOM-less files as ANSI.

function Write-Step {
    param([string]$Message)
    Write-Host ('==> {0}' -f $Message)
}

function Write-Info {
    param([string]$Message)
    Write-Host ('    {0}' -f $Message)
}

function Write-Fail {
    param([string]$Message)
    Write-Host ('!! {0}' -f $Message) -ForegroundColor Red
    exit 1
}

# Retry a flaky command (process kill, exclusive file open) with a fixed backoff.
function Invoke-Retry {
    param(
        [scriptblock]$Command,
        [string]$Label = 'command',
        [int]$MaxAttempts = 3,
        [int]$DelaySeconds = 2
    )

    for ($attempt = 1; $attempt -le $MaxAttempts; $attempt++) {
        try {
            return & $Command
        } catch {
            if ($attempt -eq $MaxAttempts) {
                Write-Info ('{0}: still failing after {1} attempt(s): {2}' -f $Label, $MaxAttempts, $_.Exception.Message)
                throw
            }
            Write-Info ('{0}: attempt {1}/{2} failed, retrying in {3}s' -f $Label, $attempt, $MaxAttempts, $DelaySeconds)
            Start-Sleep -Seconds $DelaySeconds
        }
    }
}

# True when no process listens on the port any more.
function Test-PortFree {
    param([int]$Port)
    $listeners = @(Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue)
    return ($listeners.Count -eq 0)
}

# Exclusive-open probe: detects an outside handle (AV / indexer / running app) on a file.
function Test-FileLocked {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) { return $false }
    $stream = $null
    try {
        $stream = [System.IO.File]::Open($Path, 'Open', 'ReadWrite', 'None')
        return $false
    } catch {
        return $true
    } finally {
        if ($stream) { $stream.Dispose() }
    }
}
