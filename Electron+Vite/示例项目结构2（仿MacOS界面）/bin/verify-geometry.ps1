<#
  Negative test for geometry restore: write an off-screen window.json (as if a second
  monitor was unplugged), start the app, and check it clamps back into a work area.
  The previous window.json is restored afterwards.
#>

param(
  [string]$Exe = 'release\win-unpacked\MacOSDesktopDemo.exe',
  [string]$Bogus = '{"x":-24000,"y":-24000,"width":9000,"height":9000}'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$file = Join-Path $env:APPDATA 'MacOSDesktopDemo\window.json'
$dir = Split-Path -Parent $file
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }

$backup = $null
if (Test-Path $file) { $backup = Get-Content $file -Raw }

Set-Content -Path $file -Value $Bogus -Encoding ASCII -NoNewline
Write-Output ("wrote-saved={0}" -f $Bogus)

try {
  $out = Join-Path $root 'geometry-out.txt'
  Remove-Item $out -ErrorAction SilentlyContinue
  $p = Start-Process -FilePath (Join-Path $root $Exe) -ArgumentList '--selftest' -WorkingDirectory $root `
    -PassThru -RedirectStandardOutput $out

  $deadline = (Get-Date).AddSeconds(90)
  while ((Get-Date) -lt $deadline) {
    $p.Refresh()
    if ($p.HasExited) { break }
    Start-Sleep -Milliseconds 300
  }
  if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue }

  Write-Output '--- ready-to-show ---'
  if (Test-Path $out) { Get-Content $out | Where-Object { $_ -match 'ready-to-show' } }
  Write-Output ("after-run-window.json={0}" -f (Get-Content $file -Raw))
}
finally {
  if ($null -ne $backup) {
    Set-Content -Path $file -Value $backup -Encoding ASCII -NoNewline
    Write-Output 'restored original window.json'
  } else {
    Remove-Item $file -ErrorAction SilentlyContinue
    Write-Output 'removed the test window.json'
  }
}

$left = @(Get-Process -Name 'MacOSDesktopDemo' -ErrorAction SilentlyContinue)
if ($left.Count -gt 0) {
  $left | ForEach-Object { Stop-Process -Id $_.Id -Force -ErrorAction SilentlyContinue }
  Write-Output ("killed {0} leftover process(es) this test started" -f $left.Count)
}
