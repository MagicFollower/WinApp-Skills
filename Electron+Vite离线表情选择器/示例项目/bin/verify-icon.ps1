<#
  Judges the icon by pulling it back out of the built exe and sampling pixels,
  rather than by looking at it. Requires bin/make-icon.ps1 to have run and the
  app to be packaged.
#>

param(
  [string]$Exe = 'release\win-unpacked\EmojiPicker.exe',
  [string]$BgHex = '#2F7A55'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
$exePath = Join-Path $root $Exe
if (-not (Test-Path -LiteralPath $exePath)) {
  Write-Host "!! not found: $exePath  (package first: -Target dir)" -ForegroundColor Red
  exit 1
}

function Hex-ToBgr([string]$Hex) {
  $h = $Hex.TrimStart('#')
  return @(
    [Convert]::ToInt32($h.Substring(0, 2), 16),
    [Convert]::ToInt32($h.Substring(2, 2), 16),
    [Convert]::ToInt32($h.Substring(4, 2), 16)
  )
}

$bg = Hex-ToBgr $BgHex
# 'E' on a 32px layer: cell = floor(32/11) = 2, glyph 10x14, origin (11, 9).
# Row 0 of E is all five dots; row 1 is '10000', so (15, 12) is off-dot.
$points = @(
  @{ Name = 'corner-bg';  X = 2;  Y = 2;  Want = 'bg' },
  @{ Name = 'top-bar';    X = 12; Y = 10; Want = 'fg' },
  @{ Name = 'off-dot';    X = 15; Y = 12; Want = 'bg' },
  @{ Name = 'mid-bar';    X = 14; Y = 16; Want = 'fg' }
)

$icon = [System.Drawing.Icon]::ExtractAssociatedIcon($exePath)
$bmp = $icon.ToBitmap()
Write-Host ("extracted={0}x{1}  from={2}" -f $bmp.Width, $bmp.Height, $Exe)

$fail = 0
foreach ($p in $points) {
  if ($p.X -ge $bmp.Width -or $p.Y -ge $bmp.Height) {
    Write-Host ("  {0,-10} SKIP out of bounds" -f $p.Name)
    continue
  }
  $c = $bmp.GetPixel($p.X, $p.Y)
  $isBg = ([math]::Abs($c.R - $bg[0]) -lt 24) -and ([math]::Abs($c.G - $bg[1]) -lt 24) -and ([math]::Abs($c.B - $bg[2]) -lt 24)
  $isFg = ($c.R -gt 200) -and ($c.G -gt 200) -and ($c.B -gt 200)
  $got = if ($isFg) { 'fg' } elseif ($isBg) { 'bg' } else { 'other' }
  $ok = ($got -eq $p.Want)
  if (-not $ok) { $fail++ }
  Write-Host ("  {0,-10} ({1,2},{2,2}) want={3} got={4} rgb=({5},{6},{7})  {8}" -f `
      $p.Name, $p.X, $p.Y, $p.Want, $got, $c.R, $c.G, $c.B, $(if ($ok) { 'ok' } else { 'MISMATCH' }))
}

$bmp.Dispose()
$icon.Dispose()

if ($fail -gt 0) {
  Write-Host "icon-check=FAIL ($fail mismatched)"
  exit 1
}
Write-Host 'icon-check=ok'
