param(
    [int]$DevPort = 5173,
    [string]$NodeDir = 'C:\Program Files\nodejs'
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

$root = Split-Path -Parent $PSScriptRoot

try {
    Set-Location -LiteralPath $root
    Write-Step ('starting ChatAPP dev from {0}' -f $root)

    # This shell's PATH may predate the Node install; npm child processes need node resolvable.
    if ($env:Path -notlike ('*' + $NodeDir + '*')) {
        $env:Path = $NodeDir + [IO.Path]::PathSeparator + $env:Path
        Write-Info ('prepended PATH entry: {0}' -f $NodeDir)
    }

    if (-not (Test-PortFree -Port $DevPort)) {
        $owner = @(Get-NetTCPConnection -LocalPort $DevPort -State Listen -ErrorAction SilentlyContinue |
            Select-Object -ExpandProperty OwningProcess -Unique)
        Write-Info ('port {0} is already served by pid {1} - run bin\stop.bat first, --strictPort will otherwise fail' -f $DevPort, ($owner -join ','))
    }

    Write-Step 'npm run dev'
    & npm.cmd run dev
    exit $LASTEXITCODE
} catch {
    Write-Fail ('start failed: {0}' -f $_.Exception.Message)
}
