<#
.SYNOPSIS
    按目录里实际存在的文件重新生成 assets/seed/manifest.json（钉 path + blobSha + size + 内容计数基线）。
.DESCRIPTION
    fetch-source.ps1 的校验完全以 manifest.json 为准，所以"上游改版"这件事必须先重钉清单再取源，否则新/删的文件会被当成缺口一直报。
    blobSha 用 LF 归一后的 git blob sha，与 GitHub contents API 报的 sha 同口径（磁盘存 CRLF 还是 LF 都不影响）。没有 git 时退化成只钉 size，
    此时 fetch-source.ps1 的校验也跟着退化成"路径齐 + LF 字节数"。
    fetchedFrom 段（仓库、分支、commit、各通路 URL）默认从旧 manifest 原样继承；上游换了目录用 -Repo/-Branch/-PathPrefix/-Commit 覆盖。
.PARAMETER SourceDir
    要清点的那份源码目录，默认 <Skill>/assets/seed/lib。
.PARAMETER OutFile
    写出的清单，默认 <Skill>/assets/seed/manifest.json。
.PARAMETER Repo
    覆盖 fetchedFrom.repo，例如 MagicFollower/WinApp-Skills。
.PARAMETER Branch
    覆盖 fetchedFrom.branch。
.PARAMETER Commit
    覆盖 fetchedFrom.commit（40 位 sha；tarball 与 raw 两条 URL 都由它拼出稳定版本）。
.PARAMETER PathPrefix
    覆盖 fetchedFrom.pathPrefix（仓库内那个组件目录的相对路径）。
.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File make-manifest.ps1
.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File make-manifest.ps1 -Commit 5d96442af3ca16917f95b42b4f29019b0dc6252c
#>
[CmdletBinding()]
param(
    [string]$SourceDir,
    [string]$OutFile,
    [string]$Repo,
    [string]$Branch,
    [string]$Commit,
    [string]$PathPrefix
)

$ErrorActionPreference = 'Stop'
try { [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false) } catch { }

function Fail([string]$Message, [int]$Code) {
    Write-Output ("FAIL  " + $Message)
    exit $Code
}

$skillRoot = Split-Path -Parent $PSScriptRoot
if ([string]::IsNullOrWhiteSpace($SourceDir)) { $SourceDir = Join-Path $skillRoot 'assets\seed\lib' }
if ([string]::IsNullOrWhiteSpace($OutFile)) { $OutFile = Join-Path $skillRoot 'assets\seed\manifest.json' }
$SourceDir = [System.IO.Path]::GetFullPath($SourceDir)
if (-not (Test-Path -LiteralPath $SourceDir)) { Fail "目录不存在：$SourceDir" 2 }

function Get-LfBytes([string]$File) {
    $bytes = [System.IO.File]::ReadAllBytes($File)
    $out = New-Object System.Collections.Generic.List[byte]
    for ($i = 0; $i -lt $bytes.Length; $i++) {
        if ($bytes[$i] -eq 0x0D -and ($i + 1) -lt $bytes.Length -and $bytes[$i + 1] -eq 0x0A) { continue }
        $out.Add($bytes[$i])
    }
    return ,$out.ToArray()
}

$gitOk = $false
if (Get-Command git.exe -ErrorAction SilentlyContinue) { $gitOk = $true }
# 探针文件放种子同级，不用 %TEMP%：与 fetch-source.ps1 的 .fetch-tmp 同一套约定，算完就删
$probeDir = Join-Path (Split-Path -Parent $SourceDir) '.manifest-tmp'
if (-not (Test-Path -LiteralPath $probeDir)) { New-Item -ItemType Directory -Path $probeDir -Force | Out-Null }
$probe = Join-Path $probeDir 'lf.bin'

$entries = @(Get-ChildItem -LiteralPath $SourceDir -Recurse -File | ForEach-Object {
    $rel = $_.FullName.Substring($SourceDir.Length).TrimStart('\', '/') -replace '\\', '/'
    $lf = Get-LfBytes $_.FullName
    $sha = ''
    if ($gitOk) {
        [System.IO.File]::WriteAllBytes($probe, $lf)
        $ErrorActionPreference = 'Continue'
        $sha = (& git.exe hash-object --no-filters -- $probe)
        $ErrorActionPreference = 'Stop'
        if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sha)) { $sha = '' } else { $sha = $sha.Trim() }
    }
    [pscustomobject]@{ path = $rel; blobSha = $sha; size = $lf.Length }
} | Sort-Object path)
Remove-Item -LiteralPath $probeDir -Recurse -Force -ErrorAction SilentlyContinue

if ($entries.Count -lt 1) { Fail "目录里没有文件：$SourceDir" 2 }
$csCount = @($entries | Where-Object { $_.path.EndsWith('.cs') }).Count
if (-not $gitOk) { Write-Output "WARN  没有 git.exe，blobSha 钉不了；fetch-source.ps1 的校验会退化成按 LF 字节数比" }
if ($gitOk -and @($entries | Where-Object { [string]::IsNullOrWhiteSpace($_.blobSha) }).Count -gt 0) {
    Fail '有文件算不出 blob sha' 2
}

function CountOf([string]$Pattern, [string]$Text) { return ([regex]::Matches($Text, $Pattern)).Count }
$allText = ''
foreach ($e in ($entries | Where-Object { $_.path.EndsWith('.cs') })) {
    $allText += ((Get-Content -LiteralPath (Join-Path $SourceDir ($e.path -replace '/', '\')) -Raw -Encoding UTF8) + "`n")
}
$dpDeclared = CountOf 'public static readonly DependencyProperty' $allText
$tokenBody = [regex]::Match($allText, 'public enum UI4ThemeToken\s*\{(?<b>[^}]*)\}').Groups['b'].Value
$tokenMembers = ([regex]::Matches($tokenBody, '(?m)^\s*([A-Z][A-Za-z0-9]*)\s*(,|$)')).Count

# fetchedFrom 继承旧清单，只覆盖点名要改的字段
$old = $null
if (Test-Path -LiteralPath $OutFile) {
    $old = Get-Content -LiteralPath $OutFile -Raw -Encoding UTF8 | ConvertFrom-Json
}
$ff = [ordered]@{}
foreach ($k in @('repo', 'branch', 'commit', 'pathPrefix', 'tree', 'tarball', 'tarballByCommit', 'rawByPath', 'gitClone', 'usageDoc')) {
    $ff[$k] = if ($old) { $old.fetchedFrom.$k } else { '' }
}
$slug = $Repo; if ([string]::IsNullOrWhiteSpace($slug)) { $slug = $ff.repo }
$br = $Branch; if ([string]::IsNullOrWhiteSpace($br)) { $br = $ff.branch }
$cm = $Commit; if ([string]::IsNullOrWhiteSpace($cm)) { $cm = $ff.commit }
$pp = $PathPrefix; if ([string]::IsNullOrWhiteSpace($pp)) { $pp = $ff.pathPrefix }
if ([string]::IsNullOrWhiteSpace($slug) -or [string]::IsNullOrWhiteSpace($br) -or [string]::IsNullOrWhiteSpace($pp)) {
    Fail 'fetchedFrom 的 repo/branch/pathPrefix 不全，旧清单里也没有，必须用参数给齐' 2
}
$ff.repo = $slug; $ff.branch = $br; $ff.commit = $cm; $ff.pathPrefix = $pp
$ff.tree = "https://github.com/$slug/tree/$br/$pp"
$ff.tarball = "https://codeload.github.com/$slug/tar.gz/refs/heads/$br"
$ff.tarballByCommit = "https://codeload.github.com/$slug/tar.gz/$cm"
$ff.rawByPath = "https://raw.githubusercontent.com/$slug/$cm/$pp/{0}"
$ff.gitClone = "https://github.com/$slug.git"
if ([string]::IsNullOrWhiteSpace($ff.usageDoc)) { $ff.usageDoc = 'README.md' }

$doc = [ordered]@{
    schema      = if ($old) { $old.schema } else { 1 }
    fetchedFrom = $ff
    mirrorNote  = if ($old) { $old.mirrorNote } else { '-Mirror takes a GitHub reverse-proxy prefix; it is prepended to the full tarball URL. Probe reachability with a GET on the real download URL, never a HEAD.' }
    upstream    = if ($old) { $old.upstream } else { $null }
    generated   = [ordered]@{
        atUtc   = ([DateTime]::UtcNow).ToString('yyyy-MM-ddTHH:mm:ssZ')
        by      = 'make-manifest.ps1'
        note    = 'blobSha 是 LF 归一后的 git blob sha，与 GitHub contents API 的 sha 同口径；size 同样是 LF 后的字节数。'
    }
    expected    = [ordered]@{
        totalFiles     = $entries.Count
        csFiles        = $csCount
        themeTokens    = $tokenMembers
        dpDeclarations = $dpDeclared
        dpNote         = 'public static readonly DependencyProperty 声明数，含别名（如 UI4CheckBox.BoxCornerRadiusProperty = CornerRadiusProperty）。文本扫 DependencyProperty.Register( 会漏，因为本库把声明与注册调用写在相邻两行。'
    }
    files       = @($entries | ForEach-Object { [ordered]@{ path = $_.path; blobSha = $_.blobSha; size = $_.size } })
}

$json = $doc | ConvertTo-Json -Depth 6
[System.IO.File]::WriteAllText($OutFile, $json, (New-Object System.Text.UTF8Encoding($false)))
Write-Output ("OK    清单已重钉：" + $OutFile)
Write-Output ("      " + $entries.Count + " 个文件（.cs " + $csCount + "），令牌 " + $tokenMembers + "，DP 声明 " + $dpDeclared)
Write-Output ("      源目录 " + $SourceDir)
Write-Output "      下一步：fetch-source.ps1 -Refresh 走一遍，确认 VERIFY ok 与通路可用"
