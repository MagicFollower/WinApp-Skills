<#
  Verifies `npm run dev` really reaches a loaded, self-tested window, then tears down exactly
  what it started (npm, the Electron it spawned, and the Vite child).

  Usage:
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\verify-dev.ps1
#>

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$node = 'C:\Program Files\nodejs\node.exe'
$npmCli = 'C:\Program Files\nodejs\node_modules\npm\bin\npm-cli.js'
if (-not (Test-Path $node)) {
  $cmd = Get-Command node.exe -ErrorAction SilentlyContinue
  if ($cmd) { $node = $cmd.Source }
}
$env:Path = 'C:\Program Files\nodejs;' + $env:Path

$out = Join-Path $root 'dev-out.txt'
$errFile = Join-Path $root 'dev-err.txt'
Remove-Item $out, $errFile -ErrorAction SilentlyContinue

# Start-Process joins ArgumentList with spaces without quoting, so quote the spaced paths.
$npm = Start-Process -FilePath "`"$node`"" `
  -ArgumentList "`"$npmCli`" run dev -- --selftest" `
  -WorkingDirectory $root -PassThru `
  -RedirectStandardOutput $out -RedirectStandardError $errFile

$deadline = (Get-Date).AddSeconds(120)
$hit = $false
while ((Get-Date) -lt $deadline) {
  if ((Test-Path $out) -and ((Get-Content $out -Raw) -match '\[desktop\] flip-dark')) { $hit = $true; break }
  if ($npm.HasExited) { break }
  Start-Sleep -Milliseconds 500
}

Write-Output ("dev-reached-full-selftest={0}" -f $hit)
Write-Output '--- key lines ---'
if (Test-Path $out) {
  Get-Content $out | Where-Object {
    $_ -like '*ready in*' -or $_ -like '*Local:*' -or $_ -like '*adaptive*' -or
    $_ -like '*flip-*' -or $_ -like '*notes*' -or $_ -like '*finder*'
  } | Select-Object -First 10
}
if ((Get-Content $errFile -Raw -ErrorAction SilentlyContinue)) {
  Write-Output '--- stderr (first 6) ---'
  Get-Content $errFile | Select-Object -First 6
}

$killed = @()
# Kill only Electron whose binary lives inside this project - a plain `electron` name filter
# would take down unrelated Electron apps running on this machine.
foreach ($proc in @(Get-CimInstance Win32_Process -Filter "Name='electron.exe'" |
    Where-Object { $_.ExecutablePath -like "$root*" -or $_.CommandLine -like "*$root*" })) {
  $killed += "electron PID=$($proc.ProcessId)"
  Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue
}
if (-not $npm.HasExited) {
  $killed += "npm PID=$($npm.Id)"
  Stop-Process -Id $npm.Id -Force -ErrorAction SilentlyContinue
}
foreach ($proc in @(Get-CimInstance Win32_Process -Filter "Name='node.exe'" | Where-Object { $_.CommandLine -like '*vite*' -or $_.CommandLine -like '*dev.mjs*' })) {
  $killed += "vite/dev PID=$($proc.ProcessId)"
  Stop-Process -Id $proc.ProcessId -Force -ErrorAction SilentlyContinue
}
Write-Output ("torn-down: " + ($killed -join ', '))
