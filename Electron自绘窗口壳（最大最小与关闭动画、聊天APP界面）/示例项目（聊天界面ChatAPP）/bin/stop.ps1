param(
    [int]$DevPort = 5173,
    [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

$root = Split-Path -Parent $PSScriptRoot
$script:Killed = 0

# Only ever targets processes we just enumerated by pid; never kills by bare name.
function Stop-Target {
    param([int]$Id, [string]$Name, [string]$Why)

    if ($DryRun) {
        Write-Info ('dry-run pid={0} name={1} ({2})' -f $Id, $Name, $Why)
        return
    }
    try {
        Invoke-Retry -Label ('stop pid {0}' -f $Id) -MaxAttempts 3 -DelaySeconds 1 -Command {
            Stop-Process -Id $Id -Force -ErrorAction Stop
        } | Out-Null
        $script:Killed++
        Write-Info ('killed pid={0} name={1} ({2})' -f $Id, $Name, $Why)
    } catch {
        Write-Info ('pid={0} name={1} could not be stopped: {2}' -f $Id, $Name, $_.Exception.Message)
    }
}

function Show-Targets {
    param([string]$Label, [object[]]$Targets)

    $list = @($Targets | Where-Object { $_ })
    Write-Info ('{0}: {1} process(es)' -f $Label, $list.Count)
    foreach ($t in $list) {
        Stop-Target -Id $t.Id -Name $t.Name -Why $Label
    }
}

try {
    # 1) Whatever owns the dev port (vite / node bin\dev.mjs).
    $ownerPids = @(Get-NetTCPConnection -LocalPort $DevPort -State Listen -ErrorAction SilentlyContinue |
        Select-Object -ExpandProperty OwningProcess -Unique)
    if ($ownerPids.Count -gt 0) {
        $portTargets = @(Get-Process -Id $ownerPids -ErrorAction SilentlyContinue |
            ForEach-Object { [pscustomobject]@{ Id = $_.Id; Name = $_.ProcessName } })
        Show-Targets -Label ('port ' + $DevPort) -Targets $portTargets
    } else {
        Write-Info ('port {0}: free' -f $DevPort)
    }

    # 2) Electron + node children started from THIS project only (matched by path, not name).
    $electronExe = Join-Path $root 'node_modules\electron\*'
    $electronTargets = @(Get-CimInstance Win32_Process -Filter "Name='electron.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.ExecutablePath -like $electronExe } |
        ForEach-Object { [pscustomobject]@{ Id = $_.ProcessId; Name = $_.Name } })
    Show-Targets -Label 'electron (project path)' -Targets $electronTargets

    $devScript = Join-Path $root 'bin\dev.mjs*'
    $nodeTargets = @(Get-CimInstance Win32_Process -Filter "Name='node.exe'" -ErrorAction SilentlyContinue |
        Where-Object { $_.CommandLine -like $devScript } |
        ForEach-Object { [pscustomobject]@{ Id = $_.ProcessId; Name = $_.Name } })
    Show-Targets -Label 'node dev.mjs (project path)' -Targets $nodeTargets

    # 3) Confirm the port actually released; a killed process can take a moment.
    $free = $false
    try {
        Invoke-Retry -Label ('port ' + $DevPort + ' release') -MaxAttempts 5 -DelaySeconds 1 -Command {
            if (-not (Test-PortFree -Port $DevPort)) { throw 'still listening' }
        } | Out-Null
        $free = $true
    } catch {
        $free = $false
    }

    Write-Step ('stop done: killed={0} port{1}={2}' -f $script:Killed, $DevPort, $(if ($free) { 'free' } else { 'BUSY' }))
    if (-not $free) { Write-Fail ('port {0} is still served - inspect with: Get-NetTCPConnection -LocalPort {0} -State Listen' -f $DevPort) }
    exit 0
} catch {
    Write-Fail ('stop failed: {0}' -f $_.Exception.Message)
}
