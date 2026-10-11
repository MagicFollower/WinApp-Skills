$ErrorActionPreference = 'SilentlyContinue'
$wm = Get-ItemProperty -LiteralPath 'HKCU:\Control Panel\Desktop\WindowMetrics'
$dp = Get-ItemProperty -LiteralPath 'HKCU:\Control Panel\Desktop'
$ve = Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VisualEffects'
$dwm = Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\DWM'

Write-Output ('MinAnimate=' + $wm.MinAnimate)
Write-Output ('DragFullWindows=' + $dp.DragFullWindows)
Write-Output ('UserPreferencesMask=' + (($dp.UserPreferencesMask | ForEach-Object { $_.ToString() }) -join ' '))
Write-Output ('VisualEffects_AnimateWindows=' + $ve.AnimateWindows)
Write-Output ('DWM_EnableAeroPeek=' + $dwm.EnableAeroPeek)
Write-Output ('DWM_AllowComposition=' + $dwm.AllowComposition)

# 任务栏是否在(不在的话 shell 也不会往任务栏缩)
$tb = Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'
Write-Output ('Taskbar_SmallTaskbars=' + $tb.SmallTaskbars)

# 当前前台窗口的 exstyle:看它是不是 layered(WS_EX_LAYERED=0x80000) / toolwindow(0x80) / transparent(0x20)
Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class W {
  [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll", CharSet=CharSet.Auto")] public static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int n);
  [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr h, int i);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
}
'@
$h = [W]::GetForegroundWindow()
$sb = New-Object System.Text.StringBuilder 200
[void][W]::GetWindowText($h, $sb, 200)
$ex = [W]::GetWindowLong($h, -20)
$pid2 = 0
[void][W]::GetWindowThreadProcessId($h, [ref]$pid2)
Write-Output ('Foreground hwnd=' + $h + ' title=' + $sb.ToString() + ' pid=' + $pid2)
Write-Output ('Foreground exstyle=0x' + ('{0:X8}' -f ($ex -band 0xFFFFFFFF)))
Write-Output ('  WS_EX_LAYERED(0x80000)=' + (($ex -band 0x80000) -ne 0))
Write-Output ('  WS_EX_TRANSPARENT(0x20)=' + (($ex -band 0x20) -ne 0))
Write-Output ('  WS_EX_TOOLWINDOW(0x80)=' + (($ex -band 0x80) -ne 0))
Write-Output ('  WS_EX_APPWINDOW(0x40000)=' + (($ex -band 0x40000) -ne 0))
Write-Output ('  WS_EX_NOREDIRECTIONBITMAP(0x200000)=' + (($ex -band 0x200000) -ne 0))
