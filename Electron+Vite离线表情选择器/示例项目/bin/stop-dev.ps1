$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot

# This project's own products only, matched by where the image actually lives:
# the dev electron.exe under this repo's node_modules, and EmojiPicker.exe under
# this repo's release dir. Never a bare process-name match.
$targets = Get-CimInstance Win32_Process | Where-Object {
  $_.ExecutablePath -and (
    $_.ExecutablePath.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)
  )
}

if (-not $targets) { Write-Host 'killed=0 remaining=0'; exit 0 }
foreach ($p in $targets) {
  Write-Host ("killing pid={0} name={1}" -f $p.ProcessId, $p.Name)
  Stop-Process -Id $p.ProcessId -Force -ErrorAction SilentlyContinue
}
Start-Sleep -Seconds 2

$after = @(Get-CimInstance Win32_Process | Where-Object {
  $_.ExecutablePath -and
  $_.ExecutablePath.StartsWith($root, [StringComparison]::OrdinalIgnoreCase)
}).Count
Write-Host ("killed={0} remaining={1}" -f @($targets).Count, $after)
