<#
.SYNOPSIS
    生成一个 .NET 10 WPF 工程骨架：app/ + lib/（StartUI4Controls 源码整份自包含）+ 两档发布脚本。
.DESCRIPTION
    模板在本 Skill 目录内；组件源码种子默认也在（assets/seed/lib，48 个文件）。
    种子完整 → 全程不联网；种子缺失/不完整 → 自动调 fetch-source.ps1 按 manifest 从 GitHub 回源补齐再往下走，
    取回的内容写回种子目录，下次就是本地命中。-Offline 则禁网，有缺口直接失败。
    占位符 __APPNAME__ 替换成 -Name 传入的应用名。产出的工程可直接 dotnet build；app/AppIcon.ico 由本脚本生成占位图标（不满意再覆盖）。
.PARAMETER Name
    应用名，同时用作 RootNamespace / AssemblyName / 数据目录字面量。字母开头，仅字母与数字。
.PARAMETER Path
    工程根目录，默认 .\<Name>。
.PARAMETER Force
    目标目录已有 C# 源码时仍然继续（默认拒绝，避免覆盖别人的工程）。
.PARAMETER Offline
    禁网：种子有缺口就报缺口退出，不尝试回源。离线机器/CI 上用它把"没网就别动"钉成契约。
.PARAMETER Mirror
    GitHub 反代前缀，透传给 fetch-source.ps1（直连抖动时用，例如 https://gh-proxy.com/）。
.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File scaffold.ps1 -Name MyApp -Path D:\work\MyApp
.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File scaffold.ps1 -Name MyApp -Offline
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Name,
    [string]$Path,
    [switch]$Force,
    [switch]$Offline,
    [string]$Mirror
)

$ErrorActionPreference = 'Stop'
$Token = '__APPNAME__'
# 控制台默认码页会把中文诊断打成乱码，调用方（含 agent）读 stdout 就丢了信息
try { [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false) } catch { }

function Fail([string]$Message, [int]$Code) {
    Write-Output ("FAIL  " + $Message)
    exit $Code
}

function Set-Bytes([string]$File, [byte[]]$Bytes) {
    [System.IO.File]::WriteAllBytes($File, $Bytes)
}

function Copy-Tree([string]$Src, [string]$Dst) {
    Get-ChildItem -LiteralPath $Src -Recurse -File | ForEach-Object {
        $rel = $_.FullName.Substring($Src.Length).TrimStart('\')
        $target = Join-Path $Dst $rel
        $parent = Split-Path -Parent $target
        if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
        Copy-Item -LiteralPath $_.FullName -Destination $target -Force
    }
}

# 手写单幅 32bpp ICO：Header(6) + Entry(16) + BITMAPINFOHEADER(40) + XOR(size*size*4) + AND(每行按 4 字节对齐)
function New-PlaceholderIcon([string]$OutPath, [int]$Size) {
    $rowBytes = ((($Size + 31) -shr 5) * 4)
    $xorLen = $Size * $Size * 4
    $andLen = $rowBytes * $Size
    $imgLen = 40 + $xorLen + $andLen

    $ms = New-Object System.IO.MemoryStream
    $w = New-Object System.IO.BinaryWriter($ms)
    $w.Write([UInt16]0); $w.Write([UInt16]1); $w.Write([UInt16]1)
    $w.Write([Byte]($Size -band 0xFF)); $w.Write([Byte]($Size -band 0xFF))
    $w.Write([Byte]0); $w.Write([Byte]0); $w.Write([UInt16]1); $w.Write([UInt16]32)
    $w.Write([UInt32]$imgLen); $w.Write([UInt32]22)
    $w.Write([UInt32]40); $w.Write([Int32]$Size); $w.Write([Int32]($Size * 2))
    $w.Write([UInt16]1); $w.Write([UInt16]32); $w.Write([UInt32]0)
    $w.Write([UInt32]($xorLen + $andLen)); $w.Write([Int32]0); $w.Write([Int32]0)
    $w.Write([UInt32]0); $w.Write([UInt32]0)

    $cx = $Size / 2.0
    $radius = $Size * 0.34
    for ($y = 0; $y -lt $Size; $y++) {
        for ($x = 0; $x -lt $Size; $x++) {
            $dx = $x + 0.5 - $cx
            $dy = $y + 0.5 - $cx
            $dist = [Math]::Sqrt(($dx * $dx) + ($dy * $dy))
            if ($dist -le $radius) {
                $w.Write([Byte]0xE8); $w.Write([Byte]0x6B); $w.Write([Byte]0x4F); $w.Write([Byte]0xFF)
            } else {
                $w.Write([Byte]0xFB); $w.Write([Byte]0xF9); $w.Write([Byte]0xF7); $w.Write([Byte]0xFF)
            }
        }
    }
    for ($i = 0; $i -lt $andLen; $i++) { $w.Write([Byte]0) }

    $w.Flush()
    Set-Bytes $OutPath $ms.ToArray()
    $w.Dispose(); $ms.Dispose()
}

# ---- 0. 入参与 Skill 目录 --------------------------------------------------
if ($Name -notmatch '^[A-Za-z][A-Za-z0-9]*$') {
    Fail "应用名要字母开头、仅字母数字（要当命名空间与 exe 名用）：收到 '$Name'" 2
}
if ([string]::IsNullOrWhiteSpace($Path)) { $Path = Join-Path (Get-Location).Path $Name }
$Path = [System.IO.Path]::GetFullPath($Path)

$skillRoot = Split-Path -Parent $PSScriptRoot
$seedDir = Join-Path $skillRoot 'assets\seed\lib'
$seedManifest = Join-Path $skillRoot 'assets\seed\manifest.json'
$fetchScript = Join-Path $PSScriptRoot 'fetch-source.ps1'
$tplApp = Join-Path $skillRoot 'assets\templates\app'
$tplRoot = Join-Path $skillRoot 'assets\templates\root'
foreach ($d in @($tplApp, $tplRoot)) {
    if (-not (Test-Path -LiteralPath $d)) { Fail "Skill 目录不完整，缺少 $d" 3 }
}
if (-not (Test-Path -LiteralPath $fetchScript)) { Fail "Skill 目录不完整，缺少 $fetchScript" 3 }

# ---- 1. 种子：本地优先，有缺口先回源（缺文件就停，别产出半拉工程）----------
$required = @('StartUI4Controls.csproj', 'LICENSE.txt', 'AssemblyInfo.cs', 'README.md',
              '架构审计报告-3.0.0主题机制评审.md',
              'UI4Theme.cs', 'UI4ThemeToken.cs', 'UI4ThemeDefinition.cs', 'UI4ThemePacks.cs',
              'UI4Button.cs', 'UI4NavigationView.cs',
              (Join-Path 'Internal' 'ThemeSync.cs'), (Join-Path 'Internal' 'ScrollBarResources.cs'))

function Get-SeedGaps([string]$Dir) {
    $g = @()
    foreach ($r in $required) {
        if (-not (Test-Path -LiteralPath (Join-Path $Dir $r))) { $g += ('缺 ' + $r) }
    }
    $files = @(Get-ChildItem -LiteralPath $Dir -Recurse -File -ErrorAction SilentlyContinue)
    $expectTotal = 48; $expectCs = 44
    if (Test-Path -LiteralPath $seedManifest) {
        $m = Get-Content -LiteralPath $seedManifest -Raw -Encoding UTF8 | ConvertFrom-Json
        $expectTotal = [int]$m.expected.totalFiles
        $expectCs = [int]$m.expected.csFiles
    }
    $csCount = @($files | Where-Object { $_.Extension -eq '.cs' }).Count
    if ($csCount -lt $expectCs) { $g += ('.cs 只有 ' + $csCount + ' 个，预期 ' + $expectCs) }
    if ($files.Count -lt $expectTotal) {
        $g += ('文件只有 ' + $files.Count + ' 个，预期 ' + $expectTotal + '（44 .cs + csproj + LICENSE.txt + README.md + 架构审计报告）')
    }
    return $g
}

$gaps = @(Get-SeedGaps $seedDir)
if ($gaps.Count -gt 0) {
    if ($Offline) {
        Fail ('-Offline 且种子不完整：' + ($gaps -join '；') + '。去掉 -Offline 让它从 GitHub 回源补齐') 4
    }
    Write-Output ('SEED   种子不完整（' + ($gaps -join '；') + '），先回源')
    & $fetchScript -SeedDir $seedDir -Mirror $Mirror
    $rc = $LASTEXITCODE
    $gaps = @(Get-SeedGaps $seedDir)
    if ($gaps.Count -gt 0) {
        $m = ('回源后种子仍不完整（fetch-source 退 ' + $rc + '）：' + ($gaps -join '；')) +
             '。手工通路：把上游 WPF_dotnet10/componentSourceCode/StartUI4Controls 整份放进 ' + $seedDir
        Fail $m 4
    }
    Write-Output ('SEED   已补齐并写回，下次零联网：' + $seedDir)
}

# ---- 2. 目标目录 ----------------------------------------------------------
if (Test-Path -LiteralPath $Path) {
    $clash = @(Get-ChildItem -LiteralPath $Path -Recurse -File |
               Where-Object { $_.Extension -eq '.cs' -or $_.Extension -eq '.csproj' })
    if ($clash.Count -gt 0 -and -not $Force) {
        $m = ("目标目录已有 C# 源码（{0} 个文件），不覆盖。要先看内容：" -f $clash.Count) +
             "Get-ChildItem -Recurse -File '$Path'；确认要合并再加 -Force"
        Fail $m 5
    }
} else {
    New-Item -ItemType Directory -Path $Path -Force | Out-Null
}

# ---- 3. 落 lib 种子 + app 模板 -------------------------------------------
$appDir = Join-Path $Path 'app'
$libDir = Join-Path $Path 'lib'
New-Item -ItemType Directory -Path $appDir -Force | Out-Null
New-Item -ItemType Directory -Path $libDir -Force | Out-Null

# Copy-Item 把目录拷进"已存在的同名目标"会套出一层 lib\lib，所以逐文件按相对路径拷
Copy-Tree $seedDir $libDir
foreach ($sub in @('Models', 'ViewModels', 'Views', 'Services', 'Helpers', 'Converters')) {
    New-Item -ItemType Directory -Path (Join-Path $appDir $sub) -Force | Out-Null
}
Copy-Tree $tplApp $appDir
Copy-Tree $tplRoot $Path

# ---- 4. 占位符替换（String.Replace 是字面替换，不用 -replace 走正则）------
$utf8Bom = New-Object System.Text.UTF8Encoding($true)
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$ascii = New-Object System.Text.ASCIIEncoding
$textExt = @('.csproj', '.xaml', '.cs', '.manifest', '.txt', '.cmd', '.md')
$touched = 0
Get-ChildItem -LiteralPath $Path -Recurse -File | Where-Object {
    ($textExt -contains $_.Extension.ToLower()) -and ($_.FullName -notmatch '\\lib\\')
} | ForEach-Object {
    $bytes = [System.IO.File]::ReadAllBytes($_.FullName)
    $skip = 0
    if ($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF) { $skip = 3 }
    $body = $utf8Bom.GetString($bytes, $skip, $bytes.Length - $skip)
    if ($body.Contains($Token)) {
        $out = $body.Replace($Token, $Name)
        if ($_.Extension.ToLower() -eq '.cmd') { Set-Bytes $_.FullName $ascii.GetBytes($out) }
        elseif ($_.Extension.ToLower() -eq '.md') { Set-Bytes $_.FullName $utf8NoBom.GetBytes($out) }
        else { Set-Bytes $_.FullName $utf8Bom.GetBytes($out) }
        $script:touched++
    }
}

$tokenCsproj = Join-Path $appDir ($Token + '.csproj')
if (Test-Path -LiteralPath $tokenCsproj) {
    Move-Item -LiteralPath $tokenCsproj -Destination (Join-Path $appDir ($Name + '.csproj')) -Force
}

# ---- 5. 占位图标（ApplicationIcon 与 Resource 两处都指它，缺了构建就红）---
New-PlaceholderIcon (Join-Path $appDir 'AppIcon.ico') 32

# ---- 6. 产物自检 + 交付信息 ----------------------------------------------
$appCsproj = Join-Path $appDir ($Name + '.csproj')
foreach ($p in @($appCsproj, (Join-Path $appDir 'App.xaml'), (Join-Path $appDir 'App.xaml.cs'),
                 (Join-Path $appDir 'MainWindow.xaml'), (Join-Path $appDir 'Helpers\Theme.cs'),
                 (Join-Path $appDir 'Services\SelfTest.cs'), (Join-Path $libDir 'StartUI4Controls.csproj'),
                 (Join-Path $libDir 'LICENSE.txt'), (Join-Path $Path 'publish.cmd'),
                 (Join-Path $Path 'publish_no_runtime.cmd'), (Join-Path $appDir 'AppIcon.ico'),
                 (Join-Path $Path 'doc\运行与构建（T0Level）.md'))) {
    if (-not (Test-Path -LiteralPath $p)) { Fail "产物缺 $p" 6 }
}
$leftover = @(Get-ChildItem -LiteralPath $Path -Recurse -File |
              Where-Object { $textExt -contains $_.Extension.ToLower() } |
              Where-Object { $_.FullName -notmatch '\\lib\\' } |
              Where-Object { Select-String -LiteralPath $_.FullName -Pattern $Token -SimpleMatch -Quiet })
if ($leftover.Count -gt 0) {
    $m = ("占位符没替换干净：{0}" -f (($leftover | ForEach-Object { $_.Name }) -join ', '))
    Fail $m 7
}
$libCount = @(Get-ChildItem -LiteralPath $libDir -Recurse -File).Count
$icoSize = (Get-Item -LiteralPath (Join-Path $appDir 'AppIcon.ico')).Length
$docPath = Join-Path $Path 'doc\运行与构建（T0Level）.md'
$todoCount = @(Select-String -LiteralPath $docPath -Pattern '【模板】' -SimpleMatch -ErrorAction SilentlyContinue).Count

Write-Output "OK   工程已生成：$Path"
Write-Output ("     lib/   {0} 个文件（StartUI4Controls v3.0.0 源码自包含，含组件手册 README.md）" -f $libCount)
Write-Output "     属性名/默认值/枚举/事件查 lib/README.md；主题实测数据查 lib/架构审计报告-3.0.0主题机制评审.md"
Write-Output ("     app/   占位符替换 {0} 个文件；RootNamespace/AssemblyName = {1}；AppIcon.ico {2} 字节" -f $touched, $Name, $icoSize)
Write-Output "     下一步：dotnet build `"$appCsproj`""
Write-Output "     跑起来验收主题接线：窗口上「暗 / 跟随系统 / 灰纸套装」都要改观感，"
Write-Output "     右下状态行生效键应从 light 变成 dark / paper-grey。只有局部在变 = 那处写了字面色。"
Write-Output ("     还差 " + $todoCount + " 处【模板】：doc\运行与构建（T0Level）.md —— 构建与验收通过后逐条跑命令、" +
              "把真实输出贴进去，跑不了的行显式写「未验证 + 原因」，八节缺一不可")
