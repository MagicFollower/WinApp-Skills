<#
.SYNOPSIS
    确保 StartUI4Controls 源码种子完整：本地命中就零联网，有缺口才按 manifest 从 GitHub 回源并写回种子目录。
.DESCRIPTION
    种子 = StartUI4Controls 的 48 个文件（44 个 .cs + csproj + LICENSE.txt + README.md + 架构审计报告）。
    **2026-10-04 起种子是自持基线**（清单里的 upstream.baseline.selfHosted = true）：它比 fetchedFrom 那份多了排印三键
    （UI4.Font.Size.Base / Code / Family）与三处控件修复，那些改动只存在于本仓库的两个示例工程 lib/ 里。
    三挡语义（与其它"工程内种子"约定一致）：
      默认        本地优先。清单齐 → 直接返回，不发一个请求；缺文件/内容不符 → 回源补齐并写回 -SeedDir。
      -Offline    有缺口就逐名列出后失败（退 4），绝不碰网络。
      -Refresh    忽略本地命中，强制重新取源覆盖种子。自持基线状态下**默认拒绝**（退 2），
                  要真按 fetchedFrom 那份覆盖就再加 -AllowUpstreamReset；覆盖完必须重钉 manifest。
    取源通路按 A→B→C 依次尝试，全败才报错，并把每条通路的失败原因打出来：
      A  codeload tar.gz（curl.exe 优先，退回 Invoke-WebRequest）+ Win10 自带 tar.exe 解包
      B  同 A，但 URL 前加 -Mirror 指定的 GitHub 反代前缀（本机直连抖动时用）
      C  git clone --depth 1 --filter=blob:none --sparse + sparse-checkout 只取那个子目录
    校验：先按清单逐个比 git blob sha（把 CRLF 归一成 LF 再算，种子存 CRLF、仓库存 LF 时会假红），
          没 git 就退化成"路径齐 + 逐文件 LF 字节数 + 内容计数"。内容计数（44 个 .cs / 38 个令牌 / 237 条 DP 声明）只告警不拦截，
          因为上游改版后这三个数本就会变；清单与 sha 不过才是硬失败。
.PARAMETER SeedDir
    种子目录，默认 <Skill>/assets/seed/lib。
.PARAMETER Manifest
    清单文件，默认 <Skill>/assets/seed/manifest.json。
.PARAMETER Offline
    禁网：有缺口即失败，不尝试任何通路。
.PARAMETER Refresh
    强制重新取源，覆盖种子目录里的同名文件。
.PARAMETER AllowUpstreamReset
    与 -Refresh 一起给：承认"这会按 fetchedFrom 那份旧上游覆盖自持基线"并继续。单独给没有意义。
.PARAMETER Mirror
    GitHub 反代前缀，会拼在 tar.gz 的完整 URL 之前，例如 https://gh-proxy.com/ 。
.PARAMETER TmpDir
    下载与解包用的临时目录，默认 <SeedDir 的上级>\.fetch-tmp；跑完自己清掉。
.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File fetch-source.ps1
.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File fetch-source.ps1 -Offline
.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File fetch-source.ps1 -SeedDir D:\scratch\seed -Refresh -AllowUpstreamReset
#>
[CmdletBinding()]
param(
    [string]$SeedDir,
    [string]$Manifest,
    [switch]$Offline,
    [switch]$Refresh,
    [switch]$AllowUpstreamReset,
    [string]$Mirror,
    [string]$TmpDir
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false) } catch { }

function Fail([string]$Message, [int]$Code) {
    Write-Output ("FAIL  " + $Message)
    exit $Code
}

$skillRoot = Split-Path -Parent $PSScriptRoot
if ($Refresh -and $Offline) { Fail "-Refresh（强制取源）与 -Offline（禁网）不能同时给" 2 }
if ([string]::IsNullOrWhiteSpace($SeedDir)) { $SeedDir = Join-Path $skillRoot 'assets\seed\lib' }
if ([string]::IsNullOrWhiteSpace($Manifest)) { $Manifest = Join-Path $skillRoot 'assets\seed\manifest.json' }
$SeedDir = [System.IO.Path]::GetFullPath($SeedDir)
if (-not [System.IO.Path]::IsPathRooted($Manifest)) { $Manifest = Join-Path (Get-Location).Path $Manifest }
$Manifest = [System.IO.Path]::GetFullPath($Manifest)

if (-not (Test-Path -LiteralPath $Manifest)) {
    Fail ("清单缺失：" + $Manifest + " —— Skill 目录不完整（manifest.json 是回源的唯一依据）") 2
}
$doc = Get-Content -LiteralPath $Manifest -Raw -Encoding UTF8 | ConvertFrom-Json
$entries = @($doc.files)
if ($entries.Count -lt 1) { Fail "清单里没有文件条目" 2 }

# ---- 0.5 自持基线闸门 ------------------------------------------------------
# 种子比 fetchedFrom 那份（WinApp-Skills@5d96442 的 componentSourceCode，main 上目录已删）多了排印三键与
# 三处控件修复，而这些改动只在示例工程的 lib/ 里。无脑 -Refresh 会把它们静默打回去，所以默认拒绝。
$baseline = $doc.upstream.baseline
$selfHosted = ($null -ne $baseline) -and [bool]$baseline.selfHosted
if ($selfHosted -and $Refresh -and -not $AllowUpstreamReset) {
    Fail ("-Refresh 被挡：种子是自持基线（" + $baseline.since + " 起），比 fetchedFrom 那份多 " +
          $baseline.divergentFiles + " 个文件的改动 —— " + $baseline.divergentList + "。" +
          "按旧上游覆盖会把这条通路静默打回去（症状：所有 UI4* 控件掉到 WPF 裸默认 12 px，无编译错）。" +
          "确实要回上游那份就再加 -AllowUpstreamReset，之后必须重跑 make-manifest.ps1") 2
}
if ($selfHosted -and $Refresh) {
    Write-Output ("RESET  已授权按 fetchedFrom 覆盖自持基线（" + $baseline.divergentFiles +
                  " 个文件的有意改动会被打回）。跑完请重钉 manifest 并再跑一次本脚本确认 VERIFY ok")
}

# ---- 1. LF 归一与 blob sha -------------------------------------------------
# 种子存 CRLF、仓库 blob 存 LF：直接把磁盘字节喂给 git hash-object 会因行尾不同而假红，
# 所以先按字节把 CRLF 换成 LF（BOM 与其余字节原样保留），再算 sha。
function Get-LfBytes([string]$File) {
    $bytes = [System.IO.File]::ReadAllBytes($File)
    $out = New-Object System.Collections.Generic.List[byte]
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        if ($bytes[$i] -eq 0x0D -and ($i + 1) -lt $bytes.Length -and $bytes[$i + 1] -eq 0x0A) { continue }
        $out.Add($bytes[$i])
    }
    return ,$out.ToArray()
}

$script:gitOk = $false
if (Get-Command git.exe -ErrorAction SilentlyContinue) { $script:gitOk = $true }
$script:hashTmp = $null
if ($script:gitOk) {
    if ([string]::IsNullOrWhiteSpace($TmpDir)) {
        $TmpDir = Join-Path (Split-Path -Parent $SeedDir) '.fetch-tmp'
    }
    $script:hashTmp = Join-Path $TmpDir 'lf-probe.bin'
}

function Get-LfBlobSha([string]$File) {
    $lf = Get-LfBytes $File
    if ($script:gitOk) {
        if (-not (Test-Path -LiteralPath $TmpDir)) { New-Item -ItemType Directory -Path $TmpDir -Force | Out-Null }
        [System.IO.File]::WriteAllBytes($script:hashTmp, $lf)
        $sha = (& git.exe hash-object --no-filters -- $script:hashTmp)
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sha)) { return '' }
        return $sha.Trim()
    }
    return ''
}

# ---- 2. 本地优先：先按清单点货 --------------------------------------------
function Clear-TmpIfEmpty {
    if ([string]::IsNullOrWhiteSpace($TmpDir)) { return }
    if (-not (Test-Path -LiteralPath $TmpDir)) { return }
    if (@(Get-ChildItem -LiteralPath $TmpDir -Force).Count -eq 0) {
        Remove-Item -LiteralPath $TmpDir -Force
    }
}

function Get-Gaps([string]$Dir) {
    $gaps = @()
    foreach ($e in $entries) {
        $p = Join-Path $Dir ($e.path -replace '/', '\')
        if (-not (Test-Path -LiteralPath $p)) { $gaps += ($e.path + ' 缺'); continue }
        $lf = Get-LfBytes $p
        if ($script:gitOk) {
            $sha = Get-LfBlobSha $p
            if ($sha -ne $e.blobSha) { $gaps += ($e.path + ' sha 不符') }
        } elseif ($lf.Length -ne [int]$e.size) {
            $gaps += ($e.path + (' 字节不符（LF ' + $lf.Length + ' ≠ ' + $e.size + '）'))
        }
    }
    # 算 sha 要落一个探针文件；本地命中挡不联网，别让这坨临时件留在种子的同级目录里
    if ($script:hashTmp -and (Test-Path -LiteralPath $script:hashTmp)) { Remove-Item -LiteralPath $script:hashTmp -Force }
    return $gaps
}

$gapList = @()
if (-not $Refresh) { $gapList = @(Get-Gaps $SeedDir) }
if ($gapList.Count -eq 0 -and -not $Refresh) {
    Clear-TmpIfEmpty
    Write-Output ("LOCAL  " + $entries.Count + "/" + $entries.Count + "  种子完整，未联网：" + $SeedDir)
    exit 0
}

if ($Offline) {
    Clear-TmpIfEmpty
    $m = ("-Offline 且种子有 {0} 处缺口：" -f $gapList.Count) + (($gapList | Select-Object -First 12) -join '; ')
    if ($gapList.Count -gt 12) { $m += ("… 共 {0} 处，去掉 -Offline 让它回源补齐" -f $gapList.Count) }
    Fail $m 4
}

$why = if ($Refresh) { "-Refresh 强制重新取源" } else { ("种子有 {0} 处缺口，回源补齐" -f $gapList.Count) }
Write-Output ("FETCH  " + $why)
if ($selfHosted -and -not $Refresh) {
    Write-Output ("NOTE   种子为自持基线（偏离 fetchedFrom " + $baseline.divergentFiles + " 个文件）：回源取到的是基线前的上游那份，" +
                  "缺的文件若属那 " + $baseline.divergentFiles + " 个之列会在后面的 sha 校验里点名拦下。手工通路：从示例工程的 lib/ 整文件拷 —— " + $baseline.devSource)
}

if ([string]::IsNullOrWhiteSpace($TmpDir)) {
    $TmpDir = Join-Path (Split-Path -Parent $SeedDir) '.fetch-tmp'
}
if (Test-Path -LiteralPath $TmpDir) { Remove-Item -LiteralPath $TmpDir -Recurse -Force }
New-Item -ItemType Directory -Path $TmpDir -Force | Out-Null

# ---- 3. 通路 A / B：tar.gz + tar.exe --------------------------------------
$prefix = $doc.fetchedFrom.pathPrefix
$tarUrl = $doc.fetchedFrom.tarballByCommit
if ([string]::IsNullOrWhiteSpace($tarUrl)) { $tarUrl = $doc.fetchedFrom.tarball }

# 必须锁 Windows 自带的 tar.exe / curl.exe：脚本若被 Git Bash 一类的 PATH 拉起来，
# tar.exe 会解析成 GNU tar，它把 E:\path 当"远程主机 E:"解析并报 Cannot connect to E: resolve failed，
# 于是通路 A 永远白跑一次（实测就是这样退到通路 C 才成功的）。
function Resolve-WindowsBin([string]$Name) {
    $sys = Join-Path $env:SystemRoot ('System32\' + $Name)
    if (Test-Path -LiteralPath $sys) { return $sys }
    $cmd = Get-Command $Name -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    return ''
}
$tarBin = Resolve-WindowsBin 'tar.exe'
$curlBin = Resolve-WindowsBin 'curl.exe'

function Invoke-Download([string]$Url, [string]$OutFile, [string]$Tag, [int]$MinBytes = 1000) {
    # 原生命令的 stderr 不能用 2>&1 接管道：$ErrorActionPreference='Stop' 下 PS 5.1 会把它当终止错误抛出来
    $ErrorActionPreference = 'Continue'
    if (-not [string]::IsNullOrWhiteSpace($curlBin)) {
        & $curlBin -sS --fail --retry 2 --max-time 300 -o $OutFile $Url | Out-Null
        $rc = $LASTEXITCODE
        $ErrorActionPreference = 'Stop'
        if ($rc -eq 0 -and (Test-Path -LiteralPath $OutFile) -and (Get-Item -LiteralPath $OutFile).Length -gt $MinBytes) {
            return ("curl.exe " + $Tag)
        }
        return ''
    }
    try {
        [Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $Url -OutFile $OutFile -UseBasicParsing -TimeoutSec 300
        if ((Get-Item -LiteralPath $OutFile).Length -gt $MinBytes) { return ("Invoke-WebRequest " + $Tag) }
    } catch { $ErrorActionPreference = 'Stop'; return '' }
    $ErrorActionPreference = 'Stop'
    return ''
}

function Expand-Into([string]$Archive, [string]$Dest) {
    # 只解那一个子目录：整包解会把仓库里另一个技能的中日文文件名交给 bsdtar，它会报
    # "Invalid empty pathname" 并以非 0 退出（实测过），A 通路就永远白跑。
    # 顺带只落 48 个文件而不是整包 673 个。
    $ErrorActionPreference = 'Continue'
    $listing = & $tarBin -tzf $Archive | Where-Object { $_ -like ('*/' + $prefix + '/*') }
    $rc = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($rc -ne 0 -or @($listing).Count -lt 1) { return $false }
    $top = (@($listing)[0] -split '/')[0]
    $member = $top + '/' + $prefix
    $depth = ($prefix.Split('/').Count) + 1
    if (-not (Test-Path -LiteralPath $Dest)) { New-Item -ItemType Directory -Path $Dest -Force | Out-Null }
    $ErrorActionPreference = 'Continue'
    & $tarBin -xzf $Archive -C $Dest "--strip-components=$depth" $member | Out-Null
    $rc = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($rc -ne 0) { return $false }
    $got = @(Get-ChildItem -LiteralPath $Dest -Recurse -File)
    if ($got.Count -lt 40) { return $false }
    return $Dest
}

function Copy-SourceInto([string]$SrcRoot, [string]$Dst) {
    # 只按清单点名拷：bsdtar 会把非 ASCII 文件名写成乱码名（实测 架构审计报告-*.md 落地成
    # 鏋舵瀯瀹¤鎶ュ憡-*.md），整目录递归拷会把这堆垃圾名一起写进种子。缺的那些改走 raw 单文件补。
    $copied = 0
    foreach ($e in $entries) {
        $from = Join-Path $SrcRoot ($e.path -replace '/', '\')
        if (-not (Test-Path -LiteralPath $from)) { continue }
        $to = Join-Path $Dst ($e.path -replace '/', '\')
        $parent = Split-Path -Parent $to
        if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
        Copy-Item -LiteralPath $from -Destination $to -Force
        $copied++
    }
    return $copied
}

function Invoke-GapFill([string]$Dst) {
    # 通路 D：逐文件走 raw.githubusercontent（URL 编码后中文文件名不会被解包器改写）
    if ([string]::IsNullOrWhiteSpace($doc.fetchedFrom.rawByPath)) { return 0 }
    $filled = 0
    foreach ($e in (Get-Gaps $Dst)) {
        $rel = ($e -split ' ')[0]
        $url = $doc.fetchedFrom.rawByPath -f ((($rel -split '/') | ForEach-Object { [Uri]::EscapeDataString($_) }) -join '/')
        if (-not [string]::IsNullOrWhiteSpace($Mirror)) { $url = $Mirror.TrimEnd('/') + '/' + $url }
        $to = Join-Path $Dst ($rel -replace '/', '\')
        $parent = Split-Path -Parent $to
        if (-not (Test-Path -LiteralPath $parent)) { New-Item -ItemType Directory -Path $parent -Force | Out-Null }
        $tmpTo = Join-Path $TmpDir ('fill-' + $filled + '.bin')
        # MinBytes 给 -1：raw 取回的小文件（AssemblyInfo.cs 才 136 字节）不能被体积门槛误判成失败，
        # 内容对不对由后面的 blob sha 校验说话
        if ([string]::IsNullOrWhiteSpace((Invoke-Download $url $tmpTo 'D raw' (-1)))) { continue }
        Move-Item -LiteralPath $tmpTo -Destination $to -Force
        $filled++
    }
    return $filled
}

$srcRoot = ''
$attempts = @()
$via = ''
if ($tarBin) {
    $tgz = Join-Path $TmpDir 'repo.tar.gz'
    $urls = @($tarUrl)
    if (-not [string]::IsNullOrWhiteSpace($Mirror)) { $urls += ($Mirror.TrimEnd('/') + '/' + $tarUrl) }
    for ($u = 0; $u -lt $urls.Count; $u++) {
        $tag = if ($u -eq 0) { 'A 直连' } else { 'B 镜像' }
        if (Test-Path -LiteralPath $tgz) { Remove-Item -LiteralPath $tgz -Force }
        $how = Invoke-Download $urls[$u] $tgz $tag
        if ([string]::IsNullOrWhiteSpace($how)) { $attempts += ("{0} 下载失败：{1}" -f $tag, $urls[$u]); continue }
        $ex = Expand-Into $tgz (Join-Path $TmpDir 'src')
        if ($ex -isnot [string]) { $attempts += ("{0} 下载后解包/定位子目录失败：{1}" -f $tag, $urls[$u]); continue }
        $bytes = (Get-Item -LiteralPath $tgz).Length
        Write-Output ("DOWNLOAD  {0}  {1:N0} B  → {2}" -f $how, $bytes, $ex)
        $srcRoot = $ex; $via = $tag; break
    }
} else {
    $attempts += 'A/B 跳过：系统里没有 tar.exe（Win10 1803+ 自带）'
}

# ---- 4. 通路 C：git sparse clone ------------------------------------------
if ([string]::IsNullOrWhiteSpace($srcRoot) -and $script:gitOk) {
    $clone = Join-Path $TmpDir 'clone'
    $errLog = Join-Path $TmpDir 'clone.err.txt'
    $ErrorActionPreference = 'Continue'
    & git.exe clone --quiet --depth 1 --filter=blob:none --sparse -- $doc.fetchedFrom.gitClone $clone 2> $errLog | Out-Null
    $rc1 = $LASTEXITCODE
    & git.exe -C $clone sparse-checkout set -- $prefix 2> $errLog | Out-Null
    $rc2 = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    $err = ''
    if (Test-Path -LiteralPath $errLog) { $err = ((Get-Content -LiteralPath $errLog -Encoding UTF8 -ErrorAction SilentlyContinue) -join ' / ') }
    $cand = Join-Path $clone (($prefix -replace '/', '\') + '\')
    if ($rc1 -ne 0) {
        $attempts += ('C git clone 失败（' + $rc1 + '）：' + $err)
    } elseif ($rc2 -ne 0) {
        $attempts += ('C sparse-checkout 失败（' + $rc2 + '）：' + $err)
    } elseif ((Test-Path -LiteralPath $cand) -and @(Get-ChildItem -LiteralPath $cand -Recurse -File).Count -gt 0) {
        Write-Output ("DOWNLOAD  C git sparse clone  → " + $cand)
        $srcRoot = $cand; $via = 'C git'
    } else {
        $attempts += 'C clone 成功但子目录里没有文件'
    }
} elseif ([string]::IsNullOrWhiteSpace($srcRoot)) {
    $attempts += 'C 跳过：没有 git.exe'
}

if ([string]::IsNullOrWhiteSpace($srcRoot)) {
    $m = "三条通路全败（A tar.gz 直连 / B 镜像 / C git sparse）。原因：" + ($attempts -join ' ｜ ') +
         "。下一步：换个网络再跑一次；或给 -Mirror 填一个可达的 GitHub 反代前缀；或从上游仓库手工把 " + $prefix + " 整份放进 " + $SeedDir
    Fail $m 3
}

# ---- 5. 写回种子 + 校验 ---------------------------------------------------
if (-not (Test-Path -LiteralPath $SeedDir)) { New-Item -ItemType Directory -Path $SeedDir -Force | Out-Null }
$copied = Copy-SourceInto $srcRoot $SeedDir
$filled = Invoke-GapFill $SeedDir
Write-Output ("SEED    清单拷入 " + $copied + "/" + $entries.Count + "，raw 单文件补 " + $filled)

$after = @(Get-Gaps $SeedDir)
if ($after.Count -gt 0) {
    $m = ("取回后校验不过，{0} 处：" -f $after.Count) + (($after | Select-Object -First 12) -join '; ') +
         "（通路 " + $via + "；种子目录已写入的部分保留，修好网络后重跑本脚本即可）"
    Fail $m 5
}

# 内容计数只作告警：上游改版时这三个数本来会动
$csCount = @($entries | Where-Object { $_.path.EndsWith('.cs') }).Count
$all = ''
foreach ($e in $entries) {
    if (-not $e.path.EndsWith('.cs')) { continue }
    $all += ((Get-Content -LiteralPath (Join-Path $SeedDir ($e.path -replace '/', '\')) -Raw -Encoding UTF8) + "`n")
}
$dpCount = ([regex]::Matches($all, 'public static readonly DependencyProperty')).Count
$tokenBody = [regex]::Match($all, 'public enum UI4ThemeToken\s*\{(?<b>[^}]*)\}').Groups['b'].Value
$tokenCount = ([regex]::Matches($tokenBody, '(?m)^\s*([A-Z][A-Za-z0-9]*)\s*(,|$)')).Count
$warn = @()
if ($csCount -ne [int]$doc.expected.csFiles) { $warn += ("{0} 个 .cs，清单预期 {1}" -f $csCount, $doc.expected.csFiles) }
if ($dpCount -ne [int]$doc.expected.dpDeclarations) { $warn += ("{0} 条 DP 声明，清单预期 {1}" -f $dpCount, $doc.expected.dpDeclarations) }
if ($tokenCount -ne [int]$doc.expected.themeTokens) { $warn += ("{0} 个颜色令牌，清单预期 {1}" -f $tokenCount, $doc.expected.themeTokens) }

Write-Output ("VERIFY  ok " + $entries.Count + "/" + $entries.Count + "  通路 " + $via +
              "  .cs=" + $csCount + " 令牌=" + $tokenCount + " DP声明=" + $dpCount)
if ($warn.Count -gt 0) {
    Write-Output ("WARN  计数与清单预期不符（上游可能改版，不拦截）：" + ($warn -join '；') +
                  "。确认后重新生成 assets/seed/manifest.json 再同步 expected。")
}
Write-Output ("OK    种子已齐：" + $SeedDir)
Write-Output ("      用法与组件清单看 lib 里的 " + $doc.fetchedFrom.usageDoc + "（属性名/默认值/枚举/事件以它为准）")
# 校验也要用临时文件算 sha，所以清场放在最后一步
if (Test-Path -LiteralPath $TmpDir) { Remove-Item -LiteralPath $TmpDir -Recurse -Force -ErrorAction SilentlyContinue }
