<#
  Runs the packaged app with --selftest, captures stdout, prints the assertion lines this
  project cares about, then reports the persisted window geometry and settings.
#>

param(
  [string]$Exe = 'release\win-unpacked\MacOSDesktopDemo.exe'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$out = Join-Path $root 'selftest-out.txt'
$errFile = Join-Path $root 'selftest-err.txt'
Remove-Item $out, $errFile -ErrorAction SilentlyContinue

$exePath = Join-Path $root $Exe
$t0 = Get-Date
$p = Start-Process -FilePath $exePath -ArgumentList '--selftest' -WorkingDirectory $root `
  -PassThru -RedirectStandardOutput $out -RedirectStandardError $errFile

$deadline = (Get-Date).AddSeconds(90)
$exited = $false
while ((Get-Date) -lt $deadline) {
  $p.Refresh()
  if ($p.HasExited) { $exited = $true; break }
  Start-Sleep -Milliseconds 300
}
if (-not $exited) {
  Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
  Write-Output 'RESULT=TIMEOUT-killed'
} else {
  Write-Output ("RESULT=exited after {0:N0} ms" -f ((Get-Date) - $t0).TotalMilliseconds)
}

Write-Output '--- assertions ---'
# -match, not -like: in -like patterns "[desktop]" is a character class and would pass every line.
if (Test-Path $out) { Get-Content $out | Where-Object { $_ -match '\[desktop\]' } }
if ((Get-Content $errFile -Raw -ErrorAction SilentlyContinue)) {
  Write-Output '--- stderr ---'
  Get-Content $errFile | Select-Object -First 8
}

$settingsDir = Join-Path $env:APPDATA 'MacOSDesktopDemo'
$boundsFile = Join-Path $settingsDir 'window.json'
$settingsFile = Join-Path $settingsDir 'settings.json'
Write-Output ("userData={0}" -f $settingsDir)
Write-Output ("window.json={0}" -f $(if (Test-Path $boundsFile) { (Get-Content $boundsFile -Raw) } else { 'missing' }))
Write-Output ("settings.json={0}" -f $(if (Test-Path $settingsFile) { ((Get-Content $settingsFile -Raw) -replace '\s+', ' ') } else { 'missing' }))

$left = @(Get-Process -Name 'MacOSDesktopDemo' -ErrorAction SilentlyContinue)
Write-Output ("remaining-processes={0}" -f $left.Count)
if ($left.Count -gt 0) {
  $left | ForEach-Object { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }
  Write-Output '  (killed the leftovers this test started)'
}
