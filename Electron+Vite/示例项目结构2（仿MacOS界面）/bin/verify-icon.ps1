<#
  Pixel-level proof that the built exe really carries the icon make-icon.ps1 drew.
  Sample points are the same rule coordinates, at the 32x32 layer Windows hands back.
#>

param(
  [string]$Exe = 'release\win-unpacked\MacOSDesktopDemo.exe'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
Add-Type -AssemblyName System.Drawing

$icon = [System.Drawing.Icon]::ExtractAssociatedIcon((Join-Path $root $Exe))
$bmp = $icon.ToBitmap()
Write-Output ("extracted={0}x{1}" -f $bmp.Width, $bmp.Height)

function Show($name, $px) {
  Write-Output ("{0,-16} B={1:X2} G={2:X2} R={3:X2} A={4:X2}" -f $name, $px.B, $px.G, $px.R, $px.A)
}

# Rule at 32px: bar y<5, band 23<=y<28, tiles y 24..26 at x=9/16/23 (+-2), window mark x 7..13 y 10..14.
Show 'menubar-16-2'  ($bmp.GetPixel(16, 2))            # expect F2 F5 F8
Show 'body-24-12'    ($bmp.GetPixel(24, 12))           # expect B=B2 G=6E R=1F
Show 'mark-10-12'    ($bmp.GetPixel(10, 12))          # expect white window mark
Show 'band-4-25'     ($bmp.GetPixel(4, 25))            # expect B=2A G=1E R=14
Show 'tile-16-25'    ($bmp.GetPixel(16, 25))           # expect white dock tile
Show 'corner-0-0'    ($bmp.GetPixel(0, 0))             # expect A=00 (rounded off)

$body = 0
$total = $bmp.Width * $bmp.Height
for ($y = 0; $y -lt $bmp.Height; $y++) {
  for ($x = 0; $x -lt $bmp.Width; $x++) {
    $p = $bmp.GetPixel($x, $y)
    if ($p.B -eq 0xB2 -and $p.G -eq 0x6E -and $p.R -eq 0x1F) { $body++ }
  }
}
Write-Output ("body-blue-fraction={0:P1}" -f ($body / $total))

$info = (Get-Item (Join-Path $root $Exe)).VersionInfo
Write-Output ("version={0} product={1} fileDescription={2}" -f $info.FileVersion, $info.ProductName, $info.FileDescription)
$bmp.Dispose(); $icon.Dispose()
