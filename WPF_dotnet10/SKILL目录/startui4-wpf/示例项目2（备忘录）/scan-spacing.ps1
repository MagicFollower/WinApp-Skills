param(
    [string]$Root = "."
)
# Margin/Padding 必须在 4 的倍数刻度上（0 允许，由父级供间距时常用 0）。
# 输出为空 = 过。这条能抓到真东西：模板初版报的就是 10 / 14 / 18。
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$files = Get-ChildItem -LiteralPath (Join-Path $Root "app") -Recurse -Filter *.xaml |
    Select-Object -ExpandProperty FullName
"scanned_files=" + $files.Count
foreach ($f in $files) { "  " + (Split-Path -Leaf $f) }
$bad = Select-String -Path $files -Pattern '(Margin|Padding|InnerPadding)="([0-9,\s]*)"' -AllMatches |
    ForEach-Object { $_.Matches } | ForEach-Object { $_.Groups[2].Value -split '[,\s]+' } |
    Where-Object { $_ -match '^\d+$' } | ForEach-Object { [int]$_ } |
    Where-Object { $_ -ne 0 -and ($_ % 4) -ne 0 } | Sort-Object -Unique
"off_scale_values=[" + ($bad -join ' ') + "]"
"off_scale_count=" + @($bad).Count
