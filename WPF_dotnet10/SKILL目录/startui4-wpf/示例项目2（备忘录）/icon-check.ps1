param(
    [string]$Exe = "publish_no_runtime\MemoTask_no_runtime.exe",
    [string]$Ico = "app\AppIcon.ico",
    [int]$Probe = 32
)
# 图标判据：把 exe 内嵌图标与 ico 文件在同一尺寸下整幅逐像素比，体积/字节数不是判据。
# 顺带读 ico 的帧表，确认多尺寸帧真的都在（ExtractAssociatedIcon 只会回 32x32）。
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Add-Type -AssemblyName System.Drawing | Out-Null

$exePath = (Resolve-Path -LiteralPath $Exe).Path
$icoPath = (Resolve-Path -LiteralPath $Ico).Path

$bytes = [System.IO.File]::ReadAllBytes($icoPath)
if ($bytes[0] -ne 0 -or $bytes[1] -ne 0) { throw "不是 ICO：$icoPath" }
$type = [int]$bytes[2] + [int]$bytes[3] * 256
$count = [int]$bytes[4] + [int]$bytes[5] * 256
"ico=" + (Split-Path -Leaf $icoPath) + "  bytes=" + $bytes.Length + "  type=" + $type + "  frames=" + $count
$cursor = 6 + 16 * $count
for ($i = 0; $i -lt $count; $i++) {
    $o = 6 + 16 * $i
    $w = [int]$bytes[$o]; if ($w -eq 0) { $w = 256 }
    $h = [int]$bytes[$o + 1]; if ($h -eq 0) { $h = 256 }
    $bpp = [int]$bytes[$o + 6] + [int]$bytes[$o + 7] * 256
    $len = [int]$bytes[$o + 8] + [int]$bytes[$o + 9] * 256 + [int]$bytes[$o + 10] * 65536 + [int]$bytes[$o + 11] * 16777216
    $off = [int]$bytes[$o + 12] + [int]$bytes[$o + 13] * 256 + [int]$bytes[$o + 14] * 65536 + [int]$bytes[$o + 15] * 16777216
    $kind = 'DIB'
    if ($bytes[$off] -eq 137 -and $bytes[$off + 1] -eq 80) { $kind = 'PNG' }
    "  frame " + $w + "x" + $h + "  " + $kind + "  bpp=" + $bpp + "  " + $len + " B  @ " + $off
    if ($off -ne $cursor) { throw ("帧表不连续：期望 offset " + $cursor + "，读到 " + $off) }
    $cursor += $len
}
if ($cursor -ne $bytes.Length) { throw ("末帧尾 " + $cursor + " != 文件长 " + $bytes.Length) }

$exeIcon = [System.Drawing.Icon]::ExtractAssociatedIcon($exePath)
$bmpA = $exeIcon.ToBitmap()
$icoIcon = New-Object System.Drawing.Icon($icoPath, (New-Object System.Drawing.Size($Probe, $Probe)))
$bmpB = $icoIcon.ToBitmap()
"exe-icon=" + $bmpA.Width + "x" + $bmpA.Height + "  ico@" + $Probe + "=" + $bmpB.Width + "x" + $bmpB.Height
if ($bmpA.Width -ne $bmpB.Width -or $bmpA.Height -ne $bmpB.Height) { throw "两幅尺寸不同，无法逐像素比" }

$diff = 0
$first = @()
for ($y = 0; $y -lt $bmpA.Height; $y++) {
    for ($x = 0; $x -lt $bmpA.Width; $x++) {
        $a = $bmpA.GetPixel($x, $y).ToArgb()
        $b = $bmpB.GetPixel($x, $y).ToArgb()
        if ($a -ne $b) {
            $diff++
            if ($first.Count -lt 5) {
                $first.Add(("  ({0},{1}) exe={2:X8} ico={3:X8}" -f $x, $y, $a, $b))
            }
        }
    }
}
foreach ($line in $first) { $line }
$total = $bmpA.Width * $bmpA.Height
"DIFF-PIXELS  " + $diff + " / " + $total
$cornerA = $bmpA.GetPixel(0, 0).A
$center = $bmpA.GetPixel([int]($bmpA.Width / 2), [int]($bmpA.Height / 2))
"exe corner(0,0).alpha=" + $cornerA + "  center=" + ('{0:X8}' -f $center.ToArgb())
$bmpA.Dispose(); $bmpB.Dispose(); $exeIcon.Dispose(); $icoIcon.Dispose()
if ($diff -ne 0) { throw ("内嵌图标与 ico 不一致：" + $diff + " 个像素") }
"PASS 整幅一致（" + $total + " 像素全等）"
