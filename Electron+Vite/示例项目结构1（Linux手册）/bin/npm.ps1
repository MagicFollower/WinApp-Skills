<#
  Forwards to npm using the absolute node.exe path, because this machine's shell PATH may be
  a snapshot taken before Node was installed.

  Usage:
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\npm.ps1 run typecheck
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File bin\npm.ps1 run dev -- --selftest
#>

param(
  [Parameter(ValueFromRemainingArguments = $true)]
  [string[]]$Arguments
)

$ErrorActionPreference = 'Stop'

$NodeDir = 'C:\Program Files\nodejs'
$NodeExe = Join-Path $NodeDir 'node.exe'
$NpmCli = Join-Path $NodeDir 'node_modules\npm\bin\npm-cli.js'

if (-not (Test-Path $NodeExe)) {
  $cmd = Get-Command node.exe -ErrorAction SilentlyContinue
  if ($cmd) { $NodeExe = $cmd.Source; $NodeDir = Split-Path -Parent $cmd.Source }
}
if (-not (Test-Path $NodeExe)) { Write-Output "!! node.exe not found"; exit 2 }
if (-not (Test-Path $NpmCli)) { Write-Output "!! npm-cli.js not found at $NpmCli"; exit 2 }

$env:Path = $NodeDir + [IO.Path]::PathSeparator + $env:Path
$root = Split-Path -Parent $PSScriptRoot

& $NodeExe $NpmCli --prefix $root @Arguments
exit $LASTEXITCODE
