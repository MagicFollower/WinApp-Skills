<#
  Generates a real multi-size .ico without external tools (ASCII-only for PowerShell 5.1).
  Layers 16..128 are classic 32bpp BMP entries written straight from a pixel rule;
  256 is a PNG entry (Vista+ convention) drawn with GDI+ fills.
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
$outer = @(0x6E, 0x3B, 0x1F, 0xFF)
$inner = @(0x32, 0xB8, 0xE8, 0xFF)
$stripe = @(0xFF, 0xFF, 0xFF, 0xFF)

function Get-LayerPixel {
  param([int]$X, [int]$Y, [int]$Size)
  $q = [math]::Floor($Size / 4)
  $isInner = ($X -ge $q) -and ($X -lt ($Size - $q)) -and ($Y -ge $q) -and ($Y -lt ($Size - $q))
  if ((($X + $Y) % 16) -lt 3) { return $stripe }
  if ($isInner) { return $inner }
  return $outer
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
  # Same pixel rule as the BMP layers, filled through LockBits (SetPixel would crawl).
  $buf = New-Object byte[] ($Size * $Size * 4)
  $i = 0
  $q = [math]::Floor($Size / 4)
  for ($y = 0; $y -lt $Size; $y++) {
    for ($x = 0; $x -lt $Size; $x++) {
      if ((($x + $y) % 16) -lt 3) {
        $b = 0xFF; $g2 = 0xFF; $r = 0xFF
      } elseif ($x -ge $q -and $x -lt ($Size - $q) -and $y -ge $q -and $y -lt ($Size - $q)) {
        $b = $inner[0]; $g2 = $inner[1]; $r = $inner[2]
      } else {
        $b = $outer[0]; $g2 = $outer[1]; $r = $outer[2]
      }
      $buf[$i] = $b; $buf[$i + 1] = $g2; $buf[$i + 2] = $r; $buf[$i + 3] = 0xFF
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
