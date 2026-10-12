param(
    [ValidateSet('all', 'portable', 'nsis', 'dir')]
    [string]$Target = 'all',
    [string]$CacheDir = '',
    [int]$LockAttempts = 4,
    [int]$LockDelaySeconds = 3,
    [switch]$SkipInstall
)

$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot '_common.ps1')

function Seed-BuilderCache {
    param([string]$Root, [string]$CacheDir)

    # Why this exists: extracting winCodeSign-2.6.0.7z needs the "create symbolic links"
    # privilege, which a normal (non-admin, no Developer Mode) session lacks. Only two
    # darwin *.dylib symlinks fail, so a partial extraction is still complete for Windows
    # packaging. Seed a project-local cache and point electron-builder at it.
    $wcsTarget = Join-Path $CacheDir 'winCodeSign\winCodeSign-2.6.0'
    $probe = Join-Path $wcsTarget 'rcedit-x64.exe'

    $env:ELECTRON_BUILDER_CACHE = $CacheDir
    Write-Info ('ELECTRON_BUILDER_CACHE = {0}' -f $CacheDir)

    if (Test-Path -LiteralPath $probe) {
        Write-Step 'local electron-builder cache already seeded'
        return
    }

    $appdataCache = Join-Path $env:LOCALAPPDATA 'electron-builder\Cache\winCodeSign'
    $donor = $null
    if (Test-Path -LiteralPath $appdataCache) {
        $donor = Get-ChildItem -LiteralPath $appdataCache -Directory -ErrorAction SilentlyContinue |
            Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'rcedit-x64.exe') } |
            Select-Object -First 1
    }

    if (-not $donor) {
        Write-Info 'no usable winCodeSign copy found; packaging may fail on symlink privilege.'
        Write-Info 'fix options: enable Windows Developer Mode, or run this script elevated once.'
        return
    }

    Write-Step ('seeding local electron-builder cache from {0}' -f $donor.FullName)
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $wcsTarget) | Out-Null
    # robocopy reports via bit flags (0-7 all mean success), so $? cannot judge it.
    & robocopy $donor.FullName $wcsTarget /E /NFL /NDL /NJH /NJS /NP | Out-Null
    if ($LASTEXITCODE -gt 7) { Write-Fail ('robocopy exited with {0}' -f $LASTEXITCODE) }
    $global:LASTEXITCODE = 0
}

function Test-ReleaseWritable {
    param([string]$Root, [int]$Attempts, [int]$DelaySeconds)

    # electron-builder wipes release\win-unpacked before packing. If an outside process
    # (running app, AV or file indexer) holds app.asar, that wipe fails deep inside the
    # builder with a Go stack trace - so probe it here, retrying for transient AV scans.
    $asar = Join-Path $Root 'release\win-unpacked\resources\app.asar'
    if (-not (Test-Path -LiteralPath $asar)) {
        Write-Step 'no previous release payload, output dir is clear'
        return $true
    }

    for ($i = 1; $i -le $Attempts; $i++) {
        if (-not (Test-FileLocked -Path $asar)) {
            Write-Step ('release\win-unpacked is writable (probe attempt {0})' -f $i)
            return $true
        }
        if ($i -lt $Attempts) {
            Write-Info ('app.asar locked, attempt {0}/{1} - waiting {2}s' -f $i, $Attempts, $DelaySeconds)
            Start-Sleep -Seconds $DelaySeconds
        }
    }
    return $false
}

$root = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($CacheDir)) {
    $CacheDir = Join-Path $root '.cache\electron-builder'
}

# Packaging runs for minutes, so record the last completed stage. A rerun after an
# interruption can then say where it stopped. Payload is stage name + timestamp only.
$checkpoint = Join-Path $CacheDir 'dist-checkpoint.json'

function Get-CheckpointStage {
    if (-not (Test-Path -LiteralPath $checkpoint)) { return '(none)' }
    try {
        $state = Get-Content -LiteralPath $checkpoint -Raw -Encoding UTF8 | ConvertFrom-Json
        return $state.stage
    } catch {
        return '(unreadable)'
    }
}

function Set-CheckpointStage {
    param([string]$Stage)
    $state = [pscustomobject]@{
        target = $Target
        stage  = $Stage
        at     = (Get-Date).ToString('yyyy-MM-dd HH:mm:ss')
    }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $checkpoint) | Out-Null
    $state | ConvertTo-Json -Compress | Out-File -LiteralPath $checkpoint -Encoding UTF8
}

try {
    Write-Info ('last completed stage from previous run: {0}' -f (Get-CheckpointStage))
    Seed-BuilderCache -Root $root -CacheDir $CacheDir
    Set-CheckpointStage -Stage 'cache-seeded'

    if (-not (Test-ReleaseWritable -Root $root -Attempts $LockAttempts -DelaySeconds $LockDelaySeconds)) {
        Set-CheckpointStage -Stage 'blocked-by-file-lock'
        Write-Fail @'
release\win-unpacked\resources\app.asar is locked by another process.
    Close the running ChatAPP window, or add release\ to the AV/indexer exclusion list,
    or reboot and run bin\dist.bat again. To build elsewhere in the meantime:
    npx electron-builder --win --config.directories.output=release-check
'@
    }
    Set-CheckpointStage -Stage 'preflight-ok'

    $build = Join-Path $PSScriptRoot 'electron-win-build.ps1'
    # NB: splatting an ARRAY binds positionally; use a hashtable to bind by name.
    $buildParams = @{ Target = $Target }
    if ($SkipInstall) { $buildParams['SkipInstall'] = $true }
    & $build @buildParams
    if ($LASTEXITCODE -eq 0) {
        Set-CheckpointStage -Stage 'packaged'
        Remove-Item -LiteralPath $checkpoint -Force -ErrorAction SilentlyContinue
    } else {
        Set-CheckpointStage -Stage ('builder-exited-' + $LASTEXITCODE)
    }
    exit $LASTEXITCODE
} catch {
    Set-CheckpointStage -Stage 'failed'
    Write-Fail ('packaging failed: {0}' -f $_.Exception.Message)
}
