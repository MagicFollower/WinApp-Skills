<#
  Crops one window's rectangle out of the screen, plus a 2x crop of its menu
  band so native-menu labels are legible.

  Takes a process id rather than a window title: the title is Chinese, and
  Windows PowerShell 5.1 decodes a BOM-less script as ANSI, which corrupts
  non-ASCII literals and breaks quote pairing. Keeping this file ASCII-only
  and passing a pid sidesteps that entirely.
#>

param(
  [Parameter(Mandatory = $true)][int]$Pid2,
  [string]$OutFile = 'window.png'
)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

Add-Type @"
using System;
using System.Runtime.InteropServices;
public class Win32Cap {
  [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out RECT r);
  [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
}
"@

$proc = Get-Process -Id $Pid2 -ErrorAction SilentlyContinue
if (-not $proc) { Write-Host "pid=$Pid2 not running"; exit 1 }
$h = $proc.MainWindowHandle
if ($h -eq [IntPtr]::Zero) { Write-Host "pid=$Pid2 has no main window"; exit 1 }

$r = New-Object Win32Cap+RECT
if (-not [Win32Cap]::GetWindowRect($h, [ref]$r)) { Write-Host 'GetWindowRect failed'; exit 1 }
$w = $r.Right - $r.Left
$hh = $r.Bottom - $r.Top
if ($w -le 0 -or $hh -le 0) { Write-Host "degenerate rect ${w}x${hh}"; exit 1 }

$root = Split-Path -Parent $PSScriptRoot
$out = Join-Path $root $OutFile

$full = New-Object System.Drawing.Bitmap $w, $hh
$g = [System.Drawing.Graphics]::FromImage($full)
$g.CopyFromScreen($r.Left, $r.Top, 0, 0, (New-Object System.Drawing.Size ($w, $hh)))
$full.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $full.Dispose()
Write-Host ("window rect={0},{1} {2}x{3}  file={4} bytes={5}" -f `
    $r.Left, $r.Top, $w, $hh, $OutFile, (Get-Item -LiteralPath $out).Length)
