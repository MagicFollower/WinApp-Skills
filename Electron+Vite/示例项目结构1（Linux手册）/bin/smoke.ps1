<#
  Runs the packaged app with --selftest, captures its stdout, and prints the assertion
  lines this project cares about. Also reports the persisted window geometry it wrote.
#>

param(
  [string]$Exe = 'release\win-unpacked\LinuxCmdManual.exe'
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
if (Test-Path $out) {
  Get-Content $out | Where-Object {
    $_ -like '*ready-to-show*' -or $_ -like '*adaptive*' -or $_ -like '*storage*' -or
    $_ -like '*selftest-error*' -or $_ -like '*dom {"*'
  }
}
if ((Get-Content $errFile -Raw -ErrorAction SilentlyContinue)) {
  Write-Output '--- stderr ---'
  Get-Content $errFile | Select-Object -First 6
}

$settings = Join-Path $env:APPDATA 'LinuxCmdManual'
$boundsFile = Join-Path $settings 'window.json'
Write-Output ("userData={0}" -f $settings)
Write-Output ("window.json={0}" -f $(if (Test-Path $boundsFile) { (Get-Content $boundsFile -Raw) } else { 'missing' }))

$left = @(Get-Process -Name 'LinuxCmdManual' -ErrorAction SilentlyContinue)
Write-Output ("remaining-processes={0}" -f $left.Count)
if ($left.Count -gt 0) {
  $left | ForEach-Object { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }
  Write-Output '  (killed the leftovers this test started)'
}
