<#
  Multi-size .ico without external tools (ASCII-only for PowerShell 5.1).

  Layers 16..128 are classic 32bpp BMP entries, 256 is a PNG entry (Vista+ convention).
  Both go through ONE pixel rule (Get-LayerPixel) so verify-icon.ps1 can sample the built
  exe and compare against the same numbers.

  Motif: a macOS-ish desktop - light menu bar on top, blue desktop body, dark dock strip
  with three white tiles near the bottom, rounded corners (outside = transparent).
#>

param(
  [string]$Out = 'electron\icon.ico'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$sizes = @(16, 24, 32, 48, 64, 128)
$pngSize = 256

# Colors are BGRA.
$body = @(0xB2, 0x6E, 0x1F, 0xFF)   # desktop blue
$bar = @(0xF2, 0xF5, 0xF8, 0xFF)    # menu bar, near white
$band = @(0x2A, 0x1E, 0x14, 0xFF)   # dock strip, near black
$tile = @(0xFF, 0xFF, 0xFF, 0xFF)   # dock tiles + logo dot
$empty = @(0x00, 0x00, 0x00, 0x00)  # outside the rounded square

function Get-LayerPixel {
  param([int]$X, [int]$Y, [int]$Size)

  $r = [math]::Max(2, [math]::Round($Size * 0.22))
  $cx = if ($X -lt $r) { $r } elseif ($X -ge ($Size - $r)) { $Size - $r } else { -1 }
  $cy = if ($Y -lt $r) { $r } elseif ($Y -ge ($Size - $r)) { $Size - $r } else { -1 }
  if ($cx -ge 0 -and $cy -ge 0) {
    $dx = $X - $cx
    $dy = $Y - $cy
    if (($dx * $dx + $dy * $dy) -gt ($r * $r)) { return $empty }
  }

  if ($Y -lt [math]::Round($Size * 0.16)) { return $bar }

  $bandTop = [math]::Round($Size * 0.72)
  $bandBottom = [math]::Round($Size * 0.88)
  if ($Y -ge $bandTop -and $Y -lt $bandBottom) {
    $tileTop = [math]::Round($Size * 0.75)
    $tileBottom = [math]::Round($Size * 0.85)
    if ($Y -ge $tileTop -and $Y -lt $tileBottom) {
      foreach ($center in @(0.28, 0.5, 0.72)) {
        $half = $Size * 0.065
        if ([math]::Abs($X - ($Size * $center)) -le $half) { return $tile }
      }
    }
    return $band
  }

  # small white "window" mark in the upper left of the desktop body
  if ($X -ge [math]::Round($Size * 0.22) -and $X -lt [math]::Round($Size * 0.44) -and
      $Y -ge [math]::Round($Size * 0.30) -and $Y -lt [math]::Round($Size * 0.46)) { return $tile }

  return $body
}

function Get-BmpEntryBytes {
  param([int]$Size)
  $ms = New-Object System.IO.MemoryStream
  $bw = New-Object System.IO.BinaryWriter $ms
  $maskRowBytes = [math]::Ceiling($Size / 32) * 4
  $maskBytes = $maskRowBytes * $Size
  $pixelBytes = $Size * $Size * 4

  $bw.Write([int32]40)            # BITMAPINFOHEADER size
  $bw.Write([int32]$Size)         # width
  $bw.Write([int32]($Size * 2))   # height = XOR image + AND mask
  $bw.Write([int16]1)             # planes
  $bw.Write([int16]32)            # bit count
  $bw.Write([int32]0)             # BI_RGB
  $bw.Write([int32]$pixelBytes)
  $bw.Write([int32]0)
  $bw.Write([int32]0)
  $bw.Write([int32]0)
  $bw.Write([int32]0)

  for ($y = $Size - 1; $y -ge 0; $y--) {   # bottom-up DIB
    for ($x = 0; $x -lt $Size; $x++) {
      $p = Get-LayerPixel -X $x -Y $y -Size $Size
      $bw.Write([byte]$p[0])
      $bw.Write([byte]$p[1])
      $bw.Write([byte]$p[2])
      $bw.Write([byte]$p[3])
    }
  }
  $bw.Write((New-Object byte[] $maskBytes))
  $bw.Flush()
  return , $ms.ToArray()
}

function Get-PngEntryBytes {
  param([int]$Size)
  # Same rule as the BMP layers, filled through LockBits (SetPixel would crawl).
  $buf = New-Object byte[] ($Size * $Size * 4)
  $i = 0
  for ($y = 0; $y -lt $Size; $y++) {
    for ($x = 0; $x -lt $Size; $x++) {
      $p = Get-LayerPixel -X $x -Y $y -Size $Size
      $buf[$i] = $p[0]; $buf[$i + 1] = $p[1]; $buf[$i + 2] = $p[2]; $buf[$i + 3] = $p[3]
      $i += 4
    }
  }

  $bmp = New-Object System.Drawing.Bitmap ($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $rect = New-Object System.Drawing.Rectangle (0, 0, $Size, $Size)
  $lockMode = [System.Drawing.Imaging.ImageLockMode]::WriteOnly
  $data = $bmp.LockBits($rect, $lockMode, $bmp.PixelFormat)
  [System.Runtime.InteropServices.Marshal]::Copy($buf, 0, $data.Scan0, $buf.Length)
  $bmp.UnlockBits($data)

  $ms = New-Object System.IO.MemoryStream
  $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  return , $ms.ToArray()
}

$layers = @()
foreach ($size in $sizes) {
  $layers += [pscustomobject]@{ Size = $size; Kind = 'BMP'; Bytes = (Get-BmpEntryBytes -Size $size) }
}
$layers += [pscustomobject]@{ Size = $pngSize; Kind = 'PNG'; Bytes = (Get-PngEntryBytes -Size $pngSize) }

$outPath = Join-Path $root $Out
$dir = Split-Path -Parent $outPath
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }

$fs = [System.IO.File]::Create($outPath)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$layers.Count)

$offset = 6 + (16 * $layers.Count)
foreach ($l in $layers) {
  $dim = if ($l.Size -ge 256) { 0 } else { $l.Size }
  $bw.Write([byte]$dim)
  $bw.Write([byte]$dim)
  $bw.Write([byte]0)
  $bw.Write([byte]0)
  $bw.Write([uint16]1)
  $bw.Write([uint16]32)
  $bw.Write([int32]$l.Bytes.Length)
  $bw.Write([int32]$offset)
  $offset += $l.Bytes.Length
}
foreach ($l in $layers) { $bw.Write($l.Bytes) }
$bw.Flush()
$bw.Close()
$fs.Close()

$bytes = [System.IO.File]::ReadAllBytes($outPath)
$count = [BitConverter]::ToUInt16($bytes, 4)
Write-Output ("ico={0} bytes={1} reserved={2} type={3} count={4}" -f $Out, $bytes.Length, [BitConverter]::ToUInt16($bytes, 0), [BitConverter]::ToUInt16($bytes, 2), $count)
for ($i = 0; $i -lt $count; $i++) {
  $base = 6 + (16 * $i)
  $w = $bytes[$base]; if ($w -eq 0) { $w = 256 }
  $h = $bytes[$base + 1]; if ($h -eq 0) { $h = 256 }
  $sizeRes = [BitConverter]::ToUInt32($bytes, $base + 8)
  $offRes = [BitConverter]::ToUInt32($bytes, $base + 12)
  $kind = if ($bytes[$offRes] -eq 0x89) { 'PNG' } else { 'BMP' }
  Write-Output ("  layer {0}x{1} {2} bytes={3}" -f $w, $h, $kind, $sizeRes)
}

$icon = New-Object System.Drawing.Icon $outPath
Write-Output ("icon-loaded size={0}x{1}" -f $icon.Width, $icon.Height)
$icon.Dispose()
