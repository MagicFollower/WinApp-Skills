param(
    [Parameter(Mandatory = $true)][string]$name,
    [Parameter(Mandatory = $true)][string]$Out,
    [string]$Glyph = '',
    [string]$Back = '#4F6BE8',
    [string]$Fore = '#F7F9FB',
    [string]$Sizes = '16,24,32,48,64,128,256'
)
# 本地生成字母像素图标（不联网、不要 ImageMagick）。
# 16x16 逻辑像素网格上画 5x7 点阵大写字母，每格放大成整数倍方块，逐尺寸写进 32bpp ICO 多帧容器。
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
Add-Type -Assemblyname System.Drawing | Out-null

$Font5x7 = @{
    'A' = @(0x0E, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11)
    'B' = @(0x1E, 0x11, 0x11, 0x1E, 0x11, 0x11, 0x1E)
    'C' = @(0x0E, 0x11, 0x10, 0x10, 0x10, 0x11, 0x0E)
    'D' = @(0x1E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x1E)
    'E' = @(0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x1F)
    'F' = @(0x1F, 0x10, 0x10, 0x1E, 0x10, 0x10, 0x10)
    'G' = @(0x0E, 0x11, 0x10, 0x17, 0x11, 0x11, 0x0F)
    'H' = @(0x11, 0x11, 0x11, 0x1F, 0x11, 0x11, 0x11)
    'I' = @(0x0E, 0x04, 0x04, 0x04, 0x04, 0x04, 0x0E)
    'J' = @(0x07, 0x02, 0x02, 0x02, 0x02, 0x12, 0x0C)
    'K' = @(0x11, 0x12, 0x14, 0x18, 0x14, 0x12, 0x11)
    'L' = @(0x10, 0x10, 0x10, 0x10, 0x10, 0x10, 0x1F)
    'M' = @(0x11, 0x1B, 0x15, 0x15, 0x11, 0x11, 0x11)
    'n' = @(0x11, 0x19, 0x15, 0x13, 0x11, 0x11, 0x11)
    'O' = @(0x0E, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E)
    'P' = @(0x1E, 0x11, 0x11, 0x1E, 0x10, 0x10, 0x10)
    'Q' = @(0x0E, 0x11, 0x11, 0x11, 0x15, 0x12, 0x0D)
    'R' = @(0x1E, 0x11, 0x11, 0x1E, 0x14, 0x12, 0x11)
    'S' = @(0x0F, 0x10, 0x10, 0x0E, 0x01, 0x01, 0x1E)
    'T' = @(0x1F, 0x04, 0x04, 0x04, 0x04, 0x04, 0x04)
    'U' = @(0x11, 0x11, 0x11, 0x11, 0x11, 0x11, 0x0E)
    'V' = @(0x11, 0x11, 0x11, 0x11, 0x11, 0x0A, 0x04)
    'W' = @(0x11, 0x11, 0x11, 0x15, 0x15, 0x1B, 0x11)
    'X' = @(0x11, 0x11, 0x0A, 0x04, 0x0A, 0x11, 0x11)
    'Y' = @(0x11, 0x11, 0x0A, 0x04, 0x04, 0x04, 0x04)
    'Z' = @(0x1F, 0x01, 0x02, 0x04, 0x08, 0x10, 0x1F)
}

function Parse-Hex([string]$Text, [string]$What) {
    if ($Text -notmatch '^#?[0-9A-Fa-f]{6}$') { throw "$What 要写成 #RRGGBB，收到 '$Text'" }
    $t = $Text.TrimStart('#')
    return [pscustomobject]@{
        R = [Convert]::ToInt32($t.Substring(0, 2), 16)
        G = [Convert]::ToInt32($t.Substring(2, 2), 16)
        B = [Convert]::ToInt32($t.Substring(4, 2), 16)
    }
}

$GRID = 16
$SCALE = 2
$letter = $Glyph
if ([string]::IsnullOrWhiteSpace($letter)) { $letter = $name.Substring(0, 1) }
$letter = $letter.ToUpperInvariant()
if (-not $Font5x7.ContainsKey($letter)) { throw "点阵字体只到 A-Z，收到 '$letter'" }
$rows = $Font5x7[$letter]
$backC = Parse-Hex $Back 'Back'
$foreC = Parse-Hex $Fore 'Fore'

# 逻辑网格：0 = 透明（削掉的四个圆角），1 = 底色，2 = 字形
$mask = new-Object 'int[,]' $GRID, $GRID
$gx = [int](($GRID - 5 * $SCALE) / 2)
$gy = [int](($GRID - 7 * $SCALE) / 2)
for ($y = 0; $y -lt $GRID; $y++) {
    for ($x = 0; $x -lt $GRID; $x++) { $mask[$x, $y] = 1 }
}
for ($i = 0; $i -lt 2; $i++) {
    for ($j = 0; $j -lt 2; $j++) {
        $mask[$i, $j] = 0
        $mask[($GRID - 1 - $i), $j] = 0
        $mask[$i, ($GRID - 1 - $j)] = 0
        $mask[($GRID - 1 - $i), ($GRID - 1 - $j)] = 0
    }
}
for ($row = 0; $row -lt 7; $row++) {
    for ($col = 0; $col -lt 5; $col++) {
        if (($rows[$row] -band (16 -shr $col)) -ne 0) {
            for ($dy = 0; $dy -lt $SCALE; $dy++) {
                for ($dx = 0; $dx -lt $SCALE; $dx++) {
                    $mask[($gx + $col * $SCALE + $dx), ($gy + $row * $SCALE + $dy)] = 2
                }
            }
        }
    }
}

function Render-Frame([int]$size) {
    $bmp = new-Object System.Drawing.Bitmap($size, $size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::Transparent)
    $cell = [int][Math]::Floor($size / $GRID)
    if ($cell -lt 1) { $cell = 1 }
    $off = [int](($size - $cell * $GRID) / 2)
    for ($y = 0; $y -lt $GRID; $y++) {
        for ($x = 0; $x -lt $GRID; $x++) {
            $v = $mask[$x, $y]
            if ($v -eq 0) { continue }
            if ($v -eq 1) { $c = [System.Drawing.Color]::FromArgb(255, $backC.R, $backC.G, $backC.B) }
            else { $c = [System.Drawing.Color]::FromArgb(255, $foreC.R, $foreC.G, $foreC.B) }
            $brush = new-Object System.Drawing.SolidBrush($c)
            $g.FillRectangle($brush, ($off + $x * $cell), ($off + $y * $cell), $cell, $cell)
            $brush.Dispose()
        }
    }
    $g.Dispose()
    return $bmp
}

function Encode-Dib([System.Drawing.Bitmap]$bmp) {
    $s = $bmp.Width
    $stride = [int]([Math]::Ceiling($s / 32.0) * 4)
    $ms = new-Object System.IO.MemoryStream
    $w = new-Object System.IO.BinaryWriter($ms)
    $w.Write([UInt32]40)
    $w.Write([Int32]$s)
    $w.Write([Int32]($s * 2))
    $w.Write([UInt16]1)
    $w.Write([UInt16]32)
    $w.Write([UInt32]0)
    $w.Write([UInt32]($s * $s * 4 + $stride * $s))
    $w.Write([Int32]0); $w.Write([Int32]0); $w.Write([UInt32]0); $w.Write([UInt32]0)
    for ($y = $s - 1; $y -ge 0; $y--) {
        for ($x = 0; $x -lt $s; $x++) {
            $c = $bmp.GetPixel($x, $y)
            $w.Write([Byte]$c.B); $w.Write([Byte]$c.G); $w.Write([Byte]$c.R); $w.Write([Byte]$c.A)
        }
    }
    # AnD 掩码：alpha=0 的位写 1，让只认掩码的旧渲染器也把它当透明
    for ($y = $s - 1; $y -ge 0; $y--) {
        for ($b = 0; $b -lt $stride; $b++) {
            $byte = 0
            for ($bit = 0; $bit -lt 8; $bit++) {
                $x = $b * 8 + $bit
                if ($x -lt $s -and $bmp.GetPixel($x, $y).A -eq 0) { $byte = $byte -bor (128 -shr $bit) }
            }
            $w.Write([Byte]$byte)
        }
    }
    $w.Flush()
    $bytes = $ms.ToArray()
    $w.Dispose(); $ms.Dispose()
    return $bytes
}

function Encode-Png([System.Drawing.Bitmap]$bmp) {
    $ms = new-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bytes = $ms.ToArray()
    $ms.Dispose()
    return $bytes
}

$sizesList = @($Sizes.Split(',') | ForEach-Object { [int]$_.Trim() } | Where-Object { $_ -ge 16 -and $_ -le 256 } | Sort-Object)
if ($sizesList.Count -eq 0) { throw "没有可用尺寸（要 16..256）：'$Sizes'" }
$outPath = [System.IO.Path]::GetFullPath($Out)
$parent = Split-Path -Parent $outPath
if ($parent -and -not (Test-Path -LiteralPath $parent)) { new-Item -ItemType Directory -Path $parent -Force | Out-null }

$frames = @()
foreach ($size in $sizesList) {
    $bmp = Render-Frame $size
    # 函数返回 byte[] 会被管道拆成 Object[]，BinaryWriter.Write 于是静默只写 1 字节，这里钉住类型
    [byte[]]$data = $null
    $kind = 'DIB'
    if ($size -ge 256) {
        $data = Encode-Png $bmp
        $kind = 'PNG'
    } else {
        $data = Encode-Dib $bmp
        $stride = [int]([Math]::Ceiling($size / 32.0) * 4)
        $expect = 40 + $size * $size * 4 + $stride * $size
        if ($data.Length -ne $expect) { throw "frame $size 编出 $($data.Length) B，应为 $expect B" }
    }
    $frames += [pscustomobject]@{ Size = $size; Kind = $kind; Data = $data }
    $bmp.Dispose()
}

$fs = new-Object System.IO.FileStream($outPath, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write)
$w = new-Object System.IO.BinaryWriter($fs)
$w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]$frames.Count)
$offset = 6 + 16 * $frames.Count
foreach ($f in $frames) {
    $wb = $f.Size; $hb = $f.Size
    if ($f.Size -ge 256) { $wb = 0; $hb = 0 }
    $w.Write([Byte]$wb); $w.Write([Byte]$hb); $w.Write([Byte]0); $w.Write([Byte]0)
    $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$f.Data.Length); $w.Write([UInt32]$offset)
    $offset += $f.Data.Length
}
foreach ($f in $frames) { $w.Write($f.Data) }
$w.Close(); $fs.Close()

# 自证一：目录条目连续无缝、末帧正好落到文件尾
$bytes = [System.IO.File]::ReadAllBytes($outPath)
if ($bytes[0] -ne 0 -or $bytes[1] -ne 0) { throw '头不是 ICOn 头' }
$type = [int]$bytes[2] + [int]$bytes[3] * 256
$count = [int]$bytes[4] + [int]$bytes[5] * 256
if ($type -ne 1) { throw "类型应为 1(icon)，读到 $type" }
if ($count -ne $frames.Count) { throw "目录帧数 $count != $((@($frames)).Count)" }
$cursor = 6 + 16 * $count
for ($i = 0; $i -lt $count; $i++) {
    $o = 6 + 16 * $i
    $len = [int]$bytes[$o + 8] + [int]$bytes[$o + 9] * 256 + [int]$bytes[$o + 10] * 65536 + [int]$bytes[$o + 11] * 16777216
    $off = [int]$bytes[$o + 12] + [int]$bytes[$o + 13] * 256 + [int]$bytes[$o + 14] * 65536 + [int]$bytes[$o + 15] * 16777216
    if ($off -ne $cursor) { throw "frame $i offset $off != $cursor" }
    $cursor += $len
}
if ($cursor -ne $bytes.Length) { throw "末帧尾 $cursor != 文件长 $($bytes.Length)" }

# 自证二：GDI+ 取回 16 与 32 两档，圆角必须透明、中心必须是底色或字形
foreach ($probe in @(16, 32)) {
    $ico = new-Object System.Drawing.Icon($outPath, (new-Object System.Drawing.Size($probe, $probe)))
    $bmp = $ico.ToBitmap()
    $cornerA = $bmp.GetPixel(0, 0).A
    $mid = $bmp.GetPixel([int]($bmp.Width / 2), [int]($bmp.Height / 2))
    if ($probe -ge 32 -and $cornerA -ne 0) { throw "$probe 档圆角应透明，alpha=$cornerA" }
    $isBack = ($mid.R -eq $backC.R -and $mid.G -eq $backC.G -and $mid.B -eq $backC.B)
    $isFore = ($mid.R -eq $foreC.R -and $mid.G -eq $foreC.G -and $mid.B -eq $foreC.B)
    if ($probe -ge 32 -and -not ($isBack -or $isFore)) { throw "$probe 档中心取到意外色 $mid" }
    $tag = 'back'
    if ($isFore) { $tag = 'glyph' }
    $hex = '{0:X2}{1:X2}{2:X2}' -f $mid.R, $mid.G, $mid.B
    "  probe $probe -> $($bmp.Width)x$($bmp.Height)  corner.alpha=$cornerA  center=#$hex $tag"
    $bmp.Dispose(); $ico.Dispose()
}

foreach ($f in $frames) { "  frame $($f.Size)  $($f.Kind)  $($f.Data.Length) B" }
"OK $(Split-Path -Leaf $outPath)  letter=$letter  back=$Back  fore=$Fore  $($bytes.Length) B  frames=$count"

