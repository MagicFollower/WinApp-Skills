<#
  Launches the app, waits for its main window, captures it, then stops only the
  process it started. Used to eyeball the native menu bar, which capturePage()
  cannot see because it is drawn by the OS, not by Chromium.
#>

param(
  [string]$Exe = 'node_modules\electron\dist\electron.exe',
  [string]$OutFile = 'window.png'
)
$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$exePath = Join-Path $root $Exe
if (-not (Test-Path -LiteralPath $exePath)) { Write-Host "!! not found: $exePath"; exit 1 }

$p = Start-Process -FilePath $exePath -ArgumentList '.' -WorkingDirectory $root -PassThru
Write-Host ("started pid={0}" -f $p.Id)

$h = [IntPtr]::Zero
for ($i = 0; $i -lt 40; $i++) {
  Start-Sleep -Milliseconds 500
  $p.Refresh()
  if ($p.HasExited) { Write-Host '!! process exited early'; exit 1 }
  if ($p.MainWindowHandle -ne [IntPtr]::Zero) { $h = $p.MainWindowHandle; break }
}
if ($h -eq [IntPtr]::Zero) { Write-Host 'no main window after 20s'; Stop-Process -Id $p.Id -Force; exit 1 }

# Give Chromium a moment to paint the client area.
Start-Sleep -Seconds 2
& (Join-Path $root 'bin\capture-screen.ps1') -Pid2 $p.Id -OutFile $OutFile

Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
Start-Sleep -Seconds 1
$alive = @(Get-Process -Id $p.Id -ErrorAction SilentlyContinue).Count
Write-Host ("stopped pid={0} stillRunning={1}" -f $p.Id, $alive)
