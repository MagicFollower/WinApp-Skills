param(
  [string]$Exe = 'release\win-unpacked\LinuxCmdManual.exe'
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root
Add-Type -AssemblyName System.Drawing

# Sample the icon Windows actually carries inside the built exe.
$icon = [System.Drawing.Icon]::ExtractAssociatedIcon((Join-Path $root $Exe))
$bmp = $icon.ToBitmap()
Write-Output ("extracted={0}x{1}" -f $bmp.Width, $bmp.Height)

function Show($name, $px) {
  Write-Output ("{0,-16} B={1:X2} G={2:X2} R={3:X2} A={4:X2}" -f $name, $px.B, $px.G, $px.R, $px.A)
}

# Matches the rule in make-icon.ps1: stripes where (x+y)%16<3, inner square, outer fill.
Show 'corner-0-0'  ($bmp.GetPixel(0, 0))                 # expect white stripe
Show 'inner-15-15' ($bmp.GetPixel(15, 15))               # expect B=32 G=B8 R=E8
Show 'outer-2-5'   ($bmp.GetPixel(2, 5))                 # expect B=6E G=3B R=1F

$blue = 0
for ($y = 0; $y -lt $bmp.Width; $y++) {
  for ($x = 0; $x -lt $bmp.Height; $x++) {
    $p = $bmp.GetPixel($x, $y)
    if ($p.B -eq 0x6E -and $p.G -eq 0x3B -and $p.R -eq 0x1F) { $blue++ }
  }
}
$total = $bmp.Width * $bmp.Height
Write-Output ("outer-blue-fraction={0:P1}" -f ($blue / $total))
Write-Output ("version={0} product={1} fileDescription={2}" -f `
  (Get-Item (Join-Path $root $Exe)).VersionInfo.FileVersion, `
  (Get-Item (Join-Path $root $Exe)).VersionInfo.ProductName, `
  (Get-Item (Join-Path $root $Exe)).VersionInfo.FileDescription)
$bmp.Dispose(); $icon.Dispose()
