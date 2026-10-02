<#
  Portable-artifact runtime proof via marker files.

  The portable host only unpacks to temp and spawns the real process, so stdout does NOT
  bubble back and --selftest lines are unreadable here. main.ts therefore writes markers
  into -MarkDir (window-shown / probed / checks-done); this script times them and cleans up.

  Usage:
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\smoke-marked.ps1
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\smoke-marked.ps1 -Exe release\MacOSDesktopDemo_portable.exe
#>

param(
  [string]$Exe = 'release\MacOSDesktopDemo_portable.exe',
  [string]$MarkDir = '',
  [int]$TimeoutSec = 150
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if ([string]::IsNullOrWhiteSpace($MarkDir)) { $MarkDir = Join-Path $root 'mark-probe' }
# The portable host unpacks to temp and starts the child with a different working directory,
# so a relative -MarkDir would silently write the markers somewhere else (measured: marks=0/3).
if (-not [IO.Path]::IsPathRooted($MarkDir)) { $MarkDir = Join-Path $root $MarkDir }
if (-not (Test-Path $Exe)) { Write-Output ("!! missing artifact {0}" -f $Exe); exit 1 }
Remove-Item -Recurse -Force $MarkDir -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Path $MarkDir -Force | Out-Null

$t0 = Get-Date
# -ArgumentList is space-joined without auto-quoting, so quote the path ourselves.
$p = Start-Process -FilePath (Join-Path $root $Exe) -WorkingDirectory $root `
  -ArgumentList @('--selftest', '--mark-dir', ('"{0}"' -f $MarkDir)) -PassThru

$names = @('window-shown', 'probed', 'checks-done')
$seen = @{}
$deadline = $t0.AddSeconds($TimeoutSec)
while ((Get-Date) -lt $deadline) {
  foreach ($name in $names) {
    if (-not $seen.ContainsKey($name) -and (Test-Path (Join-Path $MarkDir $name))) {
      $ms = ((Get-Date) - $t0).TotalMilliseconds
      $seen[$name] = $ms
      Write-Output ("{0} after {1:N0} ms" -f $name, $ms)
    }
  }
  if ($seen.Count -eq $names.Count) { break }
  Start-Sleep -Milliseconds 250
}

Write-Output ("marks={0}/{1}" -f $seen.Count, $names.Count)
if ($seen.ContainsKey('window-shown') -and $seen.ContainsKey('checks-done')) {
  Write-Output ("shown-to-checks-done={0:N0} ms" -f ($seen['checks-done'] - $seen['window-shown']))
}

Write-Output '--- marker files ---'
Get-ChildItem $MarkDir -File | ForEach-Object { Write-Output ("  {0} {1} bytes" -f $_.Name, $_.Length) }

# The app quits itself at the end of --selftest; kill only this project's process names.
$left = @()
foreach ($name in @('MacOSDesktopDemo', 'MacOSDesktopDemo_portable')) {
  $left += @(Get-Process -Name $name -ErrorAction SilentlyContinue)
}
foreach ($proc in $left) {
  Write-Output ("  killing leftover {0} PID={1}" -f $proc.ProcessName, $proc.Id)
  Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
}
# Stop-Process returns before the object disappears; poll so the reported count is the truth,
# not a snapshot taken mid-teardown.
$remain = 0
$settle = (Get-Date).AddSeconds(10)
while ((Get-Date) -lt $settle) {
  $remain = @(Get-Process -Name 'MacOSDesktopDemo*' -ErrorAction SilentlyContinue).Count
  if ($remain -eq 0) { break }
  Start-Sleep -Milliseconds 300
}
Write-Output ("remaining-processes={0}" -f $remain)
