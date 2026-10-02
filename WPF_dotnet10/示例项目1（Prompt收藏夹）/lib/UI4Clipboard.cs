using System;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace StartUI4Controls
{
    /// <summary>
    /// 剪贴板读写工具，走 Win32 原生通道（等价于 Win32 编辑控件 Ctrl+C/Ctrl+V 的路径）。
    /// </summary>
    /// <remarks>
    /// <para>WPF 的 <see cref="System.Windows.Clipboard"/> 基于 OLE，写入前会执行
    /// <c>OleFlushClipboard</c>；剪贴板是全局单锁资源，当截图 / 剪贴板历史类程序正在监听
    /// <c>WM_CLIPBOARDUPDATE</c> 并持有剪贴板时，OLE 调用会在调用线程上重试到秒级甚至抛
    /// CLIPBRD_E_CANT_OPEN。因此库内所有控件的复制、剪切、粘贴都改由本类完成，且写入循环
    /// 固定在后台线程执行，UI 线程不会等锁。</para>
    /// </remarks>
    public static class UI4Clipboard
    {
        private const uint CF_UNICODETEXT = 13;
        private const uint GMEM_MOVEABLE = 0x0002;
        private const uint GMEM_ZEROINIT = 0x0040;

        private const int WriteAttempts = 30;
        private const int WriteRetryDelayMs = 100;
        private const int ReadAttempts = 20;
        private const int ReadRetryDelayMs = 100;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetClipboardData(uint uFormat);

        [DllImport("user32.dll")]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll")]
        private static extern bool IsClipboardFormatAvailable(uint format);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GlobalFree(IntPtr hMem);

        /// <summary>剪贴板中是否有可读的 Unicode 文本；该查询不打开剪贴板，不会与监听程序抢锁。</summary>
        public static bool ContainsText()
        {
            try
            {
                return IsClipboardFormatAvailable(CF_UNICODETEXT);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 在后台线程写入文本，最多重试约 3 秒，完成后回到调用方线程回调 true / false。
        /// 调用方应立即完成界面反馈（删除选区、显示提示），不要等待写入结果。
        /// </summary>
        public static void TrySetTextAsync(string text, Action<bool> onDone = null)
        {
            var value = text ?? string.Empty;
            Action<bool> callback = onDone;
            var ui = SynchronizationContext.Current;

            var thread = new Thread(delegate()
            {
                bool ok = false;
                for (int attempt = 0; attempt < WriteAttempts && !ok; attempt++)
                {
                    ok = TrySetOnce(value);
                    if (!ok) Thread.Sleep(WriteRetryDelayMs);
                }

                if (callback != null)
                {
                    if (ui != null) ui.Post(delegate { callback(ok); }, null);
                    else callback(ok);
                }
            });
            thread.IsBackground = true;
            thread.Start();
        }

        /// <summary>在后台线程读取剪贴板文本，最多重试约 2 秒，结果回到调用方线程回调（读不到时回调 null）。</summary>
        public static void TryGetTextAsync(Action<string> onDone)
        {
            if (onDone == null) return;
            Action<string> callback = onDone;
            var ui = SynchronizationContext.Current;

            var thread = new Thread(delegate()
            {
                string text = null;
                for (int attempt = 0; attempt < ReadAttempts && text == null; attempt++)
                {
                    text = TryGetOnce();
                    if (text == null) Thread.Sleep(ReadRetryDelayMs);
                }

                if (ui != null) ui.Post(delegate { callback(text); }, null);
                else callback(text);
            });
            thread.IsBackground = true;
            thread.Start();
        }

        private static bool TrySetOnce(string value)
        {
            if (!OpenClipboard(IntPtr.Zero)) return false;

            bool ok = false;
            IntPtr hMem = IntPtr.Zero;
            try
            {
                EmptyClipboard();

                var bytes = new byte[(value.Length + 1) * 2];
                Encoding.Unicode.GetBytes(value, 0, value.Length, bytes, 0);

                hMem = GlobalAlloc(GMEM_MOVEABLE | GMEM_ZEROINIT, (UIntPtr)bytes.Length);
                if (hMem != IntPtr.Zero)
                {
                    IntPtr p = GlobalLock(hMem);
                    if (p != IntPtr.Zero)
                    {
                        Marshal.Copy(bytes, 0, p, bytes.Length);
                        GlobalUnlock(hMem);
                        ok = SetClipboardData(CF_UNICODETEXT, hMem) != IntPtr.Zero;
                    }
                }
            }
            catch
            {
                return false;
            }
            finally
            {
                CloseClipboard();
                if (!ok && hMem != IntPtr.Zero) GlobalFree(hMem);
            }

            return ok;
        }

        private static string TryGetOnce()
        {
            if (!OpenClipboard(IntPtr.Zero)) return null;
            try
            {
                IntPtr hMem = GetClipboardData(CF_UNICODETEXT);
                if (hMem == IntPtr.Zero) return null;

                IntPtr p = GlobalLock(hMem);
                if (p == IntPtr.Zero) return null;
                try
                {
                    return Marshal.PtrToStringUni(p) ?? string.Empty;
                }
                finally
                {
                    GlobalUnlock(hMem);
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                CloseClipboard();
            }
        }
    }
}
