$ErrorActionPreference = 'Stop'
Add-Type @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
public static class E {
  delegate bool EnumProc(IntPtr h, IntPtr l);
  [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc cb, IntPtr l);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetWindowTextW(IntPtr h, StringBuilder s, int n);
  [DllImport("user32.dll")] static extern int GetWindowLongW(IntPtr h, int i);
  [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
  [DllImport("user32.dll")] static extern bool IsWindowVisible(IntPtr h);
  [DllImport("user32.dll")] static extern bool IsIconic(IntPtr h);
  [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h, out RECT r);
  public struct RECT { public int L, T, R, B; }
  public static List<string> Run(string prefix) {
    var res = new List<string>();
    EnumWindows((h, l) => {
      var sb = new StringBuilder(256);
      GetWindowTextW(h, sb, 256);
      string t = sb.ToString();
      if (t.StartsWith(prefix)) {
        uint pid = 0;
        GetWindowThreadProcessId(h, out pid);
        long st = GetWindowLongW(h, -16);
        long ex = GetWindowLongW(h, -20);
        RECT r;
        GetWindowRect(h, out r);
        res.Add(t + "|hwnd=0x" + ((long)h).ToString("X") + "|pid=" + pid
          + "|visible=" + IsWindowVisible(h) + "|iconic=" + IsIconic(h)
          + "|rect=" + r.L + "," + r.T + "," + (r.R - r.L) + "x" + (r.B - r.T)
          + "|style=0x" + ((ulong)(st & 0xFFFFFFFF)).ToString("X8")
          + "|exstyle=0x" + ((ulong)(ex & 0xFFFFFFFF)).ToString("X8"));
      }
      return true;
    }, IntPtr.Zero);
    return res;
  }
}
'@

$rows = [E]::Run('ChatAPP')
if ($rows.Count -eq 0) { Write-Output 'NO-WINDOWS-FOUND'; exit 0 }

# PowerShell 变量名大小写不敏感:$EX 会被下面的 $ex(那个 UInt64)覆盖,所以表用 $EXB/$STB
$EXB = @{ LAYERED=0x80000; TRANSPARENT=0x20; TOOLWINDOW=0x80; APPWINDOW=0x40000; NOREDIR=0x200000; COMMDLG=0x20000000; WINDOWEDGE=0x100; CLIENTEDGE=0x200; DXWM=0x1000000 }
$STB = @{ CAPTION=0x00C00000; SYSMENU=0x80000; THICKFRAME=0x40000; MINIMIZEBOX=0x20000; MAXIMIZEBOX=0x10000; POPUP=0x80000000; CHILD=0x40000000; MINIMIZED=0x20000000 }

foreach ($r in $rows) {
  $stv = [Convert]::ToUInt64(([regex]::Match($r, '\|style=0x([0-9A-F]{8})')).Groups[1].Value, 16)
  $exv = [Convert]::ToUInt64(([regex]::Match($r, '\|exstyle=0x([0-9A-F]{8})')).Groups[1].Value, 16)
  Write-Output $r
  $exNames = @($EXB.GetEnumerator() | Where-Object { ($exv -band $_.Value) -ne 0 } | ForEach-Object { $_.Key })
  $stNames = @($STB.GetEnumerator() | Where-Object { ($stv -band $_.Value) -ne 0 } | ForEach-Object { $_.Key })
  Write-Output ('   exstyle bits: ' + ($exNames -join ' '))
  Write-Output ('   style   bits: ' + ($stNames -join ' '))
}
