<#
  Intranet distribution: copy the freshly built artifacts into a versioned folder on a
  share, write SHA256 sidecars, and prune old versions. Pass -Share \\server\apps for a
  real share; the verification run used a local directory as the stand-in.
#>

param(
  [Parameter(Mandatory = $true)]
  [string]$Share,
  [string]$ProductName = 'MacOSDesktopDemo',
  [int]$KeepVersions = 2,
  [switch]$DryRun
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

# -Encoding UTF8 is required: Windows PowerShell 5.1 defaults to ANSI and mangles a
# package.json containing non-ASCII, which then breaks ConvertFrom-Json.
$version = (Get-Content 'package.json' -Raw -Encoding UTF8 | ConvertFrom-Json).version
$src = Join-Path $root 'release'
if (-not (Test-Path $src)) { Write-Output '!! no release directory, run electron-win-build.ps1 first'; exit 1 }

$targets = @(
  (Join-Path $src "$($ProductName)_portable.exe"),
  (Join-Path $src "$($ProductName)_setup.exe")
)

$dst = Join-Path (Join-Path $Share $ProductName) $version
Write-Output ("share-target={0}" -f $dst)

foreach ($f in $targets) {
  if (-not (Test-Path $f)) { Write-Output ("!! missing artifact {0}" -f $f); exit 1 }
  $item = Get-Item $f
  $built = $item.VersionInfo.FileVersion
  if ($built -ne $version) {
    Write-Output ("!! artifact {0} was built as {1} but package.json says {2} - rebuild before publishing" -f $item.Name, $built, $version)
    exit 1
  }
  $hash = (Get-FileHash -Path $f -Algorithm SHA256).Hash
  Write-Output ("  {0,8:N1} MB  v{1}  sha256={2}  {3}" -f ($item.Length / 1MB), $built, $hash.Substring(0, 16), $item.Name)
  if ($DryRun) { continue }
  if (-not (Test-Path $dst)) { New-Item -ItemType Directory -Path $dst -Force | Out-Null }
  Copy-Item -Path $f -Destination $dst -Force
  Set-Content -Path (Join-Path $dst "$($item.Name).sha256") -Value $hash -Encoding ASCII
}

if ($DryRun) { Write-Output 'dry-run, nothing written'; exit 0 }

$all = @(Get-ChildItem -Path (Join-Path $Share $ProductName) -Directory | Sort-Object Name)
if ($all.Count -gt $KeepVersions) {
  $stale = $all[0..($all.Count - $KeepVersions - 1)]
  foreach ($s in $stale) { Write-Output ("  pruning {0}" -f $s.Name) }
  $stale | Remove-Item -Recurse -Force
}
Write-Output ("retained-versions={0}" -f (@(Get-ChildItem -Path (Join-Path $Share $ProductName) -Directory).Count))
