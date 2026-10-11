<#
  Pixel-letter monogram .ico, generated locally with System.Drawing only.
  No network, no ImageMagick, no Python. ASCII-only for PowerShell 5.1.

  A 5x7 dot matrix sits on an 11-cell logical grid; each cell is floor(Size/11)
  whole pixels, so every layer is hard-edged with no resampling and no
  anti-aliasing - that is the point of the pixel look.
#>

param(
  [string]$Letter = 'E',
  [string]$BgHex = '#2F7A55',
  [string]$FgHex = '',
  [string]$OutFile = 'electron\icon.ico'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$root = Split-Path -Parent $PSScriptRoot

# One row per 5 bits, 7 rows, MSB is the leftmost pixel.
$Font = @{
  "0" = '01110100011001110101110011000101110'; "1" = '00100011000010000100001000010001110'
  "2" = '01110100010000100010001000100011111'; "3" = '11111000100010000010000011000101110'
  "4" = '00010001100101010010111110001000010'; "5" = '11111100001111000001000011000101110'
  "6" = '00110010001000011110100011000101110'; "7" = '11111000010001000100010000100001000'
  "8" = '01110100011000101110100011000101110'; "9" = '01110100011000101111000010001001100'
  "A" = '01110100011000111111100011000110001'; "B" = '11110100011000111110100011000111110'
  "C" = '01110100011000010000100001000101110'; "D" = '11110100011000110001100011000111110'
  "E" = '11111100001000011110100001000011111'; "F" = '11111100001000011110100001000010000'
  "G" = '01110100011000010111100011000101111'; "H" = '10001100011000111111100011000110001'
  "I" = '11111001000010000100001000010011111'; "J" = '00111000100001000010000101001001100'
  "K" = '10001100101010011000101001001010001'; "L" = '10000100001000010000100001000011111'
  "M" = '10001110111010110101100011000110001'; "N" = '10001110011010110011100011000110001'
  "O" = '01110100011000110001100011000101110'; "P" = '11110100011000111110100001000010000'
  "Q" = '01110100011000110001101011001001101'; "R" = '11110100011000111110101001001010001'
  "S" = '01111100001000001110000010000111110'; "T" = '11111001000010000100001000010000100'
  "U" = '10001100011000110001100011000101110'; "V" = '10001100011000110001100010101000100'
  "W" = '10001100011000110101101011101110001'; "X" = '10001100010101000100010101000110001'
  "Y" = '10001100010101000100001000010000100'; "Z" = '11111000010001000100010001000011111'
}

$key = $Letter.ToUpperInvariant()
if (-not $Font.ContainsKey($key)) {
  Write-Host "!! unsupported -Letter '$Letter' - only A-Z and 0-9 have a 5x7 matrix" -ForegroundColor Red
  exit 1
}
$dots = $Font[$key]

function Parse-Hex([string]$Hex) {
  $h = $Hex.TrimStart('#')
  if ($h.Length -ne 6) { Write-Host "!! bad color '$Hex'" -ForegroundColor Red; exit 1 }
  return , @(
    [Convert]::ToInt32($h.Substring(0, 2), 16),
    [Convert]::ToInt32($h.Substring(2, 2), 16),
    [Convert]::ToInt32($h.Substring(4, 2), 16)
  )
}

$bgRgb = Parse-Hex $BgHex

# Foreground follows background relative luminance unless given explicitly,
# the same rule the theme token --pf-accent-fg uses.
if ([string]::IsNullOrWhiteSpace($FgHex)) {
  $lum = 0.2126 * $bgRgb[0] + 0.7152 * $bgRgb[1] + 0.0722 * $bgRgb[2]
  if ($lum -lt 140) { $fgRgb = @(0xFF, 0xFF, 0xFF) } else { $fgRgb = @(0x11, 0x11, 0x11) }
  $fgNote = 'auto'
} else {
  $fgRgb = Parse-Hex $FgHex
  $fgNote = 'given'
}
$fgText = ($fgRgb | ForEach-Object { $_.ToString('X2') }) -join ''

# THE pixel rule. Both the BMP and the PNG layers consume this single BGRA
# buffer top-down, so there is exactly one place that knows what the glyph is.
function Get-IconBuffer([int]$Size) {
  $cell = [math]::Floor($Size / 11)
  if ($cell -lt 1) { $cell = 1 }
  $gw = 5 * $cell
  $gh = 7 * $cell
  $ox = [math]::Floor(($Size - $gw) / 2)
  $oy = [math]::Floor(($Size - $gh) / 2)

  $buf = New-Object byte[] ($Size * $Size * 4)
  for ($p = 0; $p -lt ($Size * $Size); $p++) {
    $o = $p * 4
    $buf[$o] = [byte]$bgRgb[2]
    $buf[$o + 1] = [byte]$bgRgb[1]
    $buf[$o + 2] = [byte]$bgRgb[0]
    $buf[$o + 3] = 0xFF
  }

  for ($gy = 0; $gy -lt 7; $gy++) {
    for ($gx = 0; $gx -lt 5; $gx++) {
      if ($dots[$gy * 5 + $gx] -ne '1') { continue }
      for ($py = 0; $py -lt $cell; $py++) {
        $y = $oy + ($gy * $cell) + $py
        if ($y -ge $Size) { continue }
        for ($px = 0; $px -lt $cell; $px++) {
          $x = $ox + ($gx * $cell) + $px
          if ($x -ge $Size) { continue }
          $o = ($y * $Size + $x) * 4
          $buf[$o] = [byte]$fgRgb[2]
          $buf[$o + 1] = [byte]$fgRgb[1]
          $buf[$o + 2] = [byte]$fgRgb[0]
          $buf[$o + 3] = 0xFF
        }
      }
    }
  }
  return , $buf
}

function Get-BmpEntry([int]$Size, $Buf) {
  $ms = New-Object System.IO.MemoryStream
  $bw = New-Object System.IO.BinaryWriter $ms
  $maskRowBytes = [math]::Ceiling($Size / 32) * 4
  $maskBytes = $maskRowBytes * $Size

  $bw.Write([int32]40)
  $bw.Write([int32]$Size)
  $bw.Write([int32]($Size * 2))
  $bw.Write([int16]1)
  $bw.Write([int16]32)
  $bw.Write([int32]0)
  $bw.Write([int32]($Size * $Size * 4))
  $bw.Write([int32]0)
  $bw.Write([int32]0)
  $bw.Write([int32]0)
  $bw.Write([int32]0)

  for ($y = $Size - 1; $y -ge 0; $y--) {   # bottom-up DIB
    $rowStart = $y * $Size * 4
    $row = New-Object byte[] ($Size * 4)
    [Array]::Copy($Buf, $rowStart, $row, 0, $Size * 4)
    $bw.Write($row)
  }
  $bw.Write((New-Object byte[] $maskBytes))
  $bw.Flush()
  return , $ms.ToArray()
}

function Get-PngEntry([int]$Size, $Buf) {
  $bmp = New-Object System.Drawing.Bitmap ($Size, $Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $rect = New-Object System.Drawing.Rectangle (0, 0, $Size, $Size)
  $data = $bmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, $bmp.PixelFormat)
  [System.Runtime.InteropServices.Marshal]::Copy($Buf, 0, $data.Scan0, $Buf.Length)
  $bmp.UnlockBits($data)
  $ms = New-Object System.IO.MemoryStream
  $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
  $bmp.Dispose()
  return , $ms.ToArray()
}

$sizes = @(16, 24, 32, 48, 64, 128)
$pngSize = 256

$layers = @()
foreach ($size in ($sizes + $pngSize)) {
  $buf = Get-IconBuffer $size
  $cell = [math]::Floor($size / 11); if ($cell -lt 1) { $cell = 1 }
  if ($size -eq $pngSize) {
    $layers += [pscustomobject]@{ Size = $size; Kind = 'PNG'; Bytes = (Get-PngEntry $size $buf) }
  } else {
    $layers += [pscustomobject]@{ Size = $size; Kind = 'BMP'; Bytes = (Get-BmpEntry $size $buf) }
  }
  Write-Host ("  layer {0,3}px  cell={1,2}px  glyph={2}x{3}" -f $size, $cell, ($cell * 5), ($cell * 7))
}

$outPath = Join-Path $root $OutFile
$outDir = Split-Path -Parent $outPath
if (-not (Test-Path $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }

$fs = [System.IO.File]::Create($outPath)
$bw = New-Object System.IO.BinaryWriter $fs
$bw.Write([uint16]0); $bw.Write([uint16]1); $bw.Write([uint16]$layers.Count)
$offset = 6 + (16 * $layers.Count)
foreach ($l in $layers) {
  $dim = if ($l.Size -ge 256) { 0 } else { $l.Size }
  $bw.Write([byte]$dim); $bw.Write([byte]$dim); $bw.Write([byte]0); $bw.Write([byte]0)
  $bw.Write([uint16]1); $bw.Write([uint16]32)
  $bw.Write([int32]$l.Bytes.Length); $bw.Write([int32]$offset)
  $offset += $l.Bytes.Length
}
foreach ($l in $layers) { $bw.Write($l.Bytes) }
$bw.Flush(); $bw.Close(); $fs.Close()

$bytes = [System.IO.File]::ReadAllBytes($outPath)
$count = [BitConverter]::ToUInt16($bytes, 4)
Write-Host ("letter={0} bg={1} fg=#{2}({3}) ico={4} bytes={5} count={6}" -f `
    $key, $BgHex, $fgText, $fgNote, $OutFile, $bytes.Length, $count)
for ($i = 0; $i -lt $count; $i++) {
  $base = 6 + (16 * $i)
  $w = $bytes[$base]; if ($w -eq 0) { $w = 256 }
  $sizeRes = [BitConverter]::ToUInt32($bytes, $base + 8)
  $offRes = [BitConverter]::ToUInt32($bytes, $base + 12)
  $kind = if ($bytes[$offRes] -eq 0x89) { 'PNG' } else { 'BMP' }
  Write-Host ("  entry {0} {1} bytes={2}" -f $w, $kind, $sizeRes)
}

$icon = New-Object System.Drawing.Icon $outPath
Write-Host ("icon-loaded size={0}x{1}" -f $icon.Width, $icon.Height)
$icon.Dispose()
