param(
    [Parameter(Mandatory = $true)][string]$Exe,
    [string]$Name = "MemoTask*",
    [int]$TimeoutMs = 30000
)
# 起一个 WPF 进程、轮询到主窗口出现、回读标题，然后只关自己起的那个 pid。
# 点鼠标/敲键盘的自动化都别用：本机搜狗输入法候选窗会吃掉输入，误点还会落到用户桌面。
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$resolved = (Resolve-Path -LiteralPath $Exe).Path
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$p = Start-Process -FilePath $resolved -PassThru
while ($sw.ElapsedMilliseconds -lt $TimeoutMs) {
    Start-Sleep -Milliseconds 50
    $p.Refresh()
    if ($p.MainWindowHandle -ne [IntPtr]::Zero) { break }
}
$sw.Stop()
"pid=" + $p.Id
"title=" + $p.MainWindowTitle
"first_window_ms=" + $sw.ElapsedMilliseconds
Stop-Process -Id $p.Id -Force
Start-Sleep -Milliseconds 800
"residual=" + @(Get-Process -Name $Name -ErrorAction SilentlyContinue).Count
