using System;
using System.Runtime.InteropServices;

namespace MemoTask.Helpers
{
    /// <summary>
    /// 恢复窗口位置前判断它还在不在某块屏上。WPF 没有多屏枚举 API，拉 WinForms 不值当，
    /// 直接问 user32：MonitorFromPoint(MONITOR_DEFAULTTONULL) 不在任何显示器上就返回 NULL。
    /// </summary>
    internal static class Monitors
    {
        private const uint MONITOR_DEFAULTTONULL = 0;
        private const uint MONITOR_DEFAULTTONEAREST = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint flags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO info);

        /// <summary>
        /// 矩形（物理像素）是否与某个真实显示器有交集。窗口左上角落在屏外但还露出一角的情况也算在屏内。
        /// </summary>
        public static bool IsVisibleOnSomeMonitor(double left, double top, double width, double height)
        {
            if (width <= 0 || height <= 0) return false;
            if (double.IsNaN(left) || double.IsNaN(top)) return false;

            // 探三个点：左上、右下、中心，任一命中即可；再要求与那块屏的矩形真的相交。
            POINT[] probes =
            {
                Point(left + 8, top + 8),
                Point(left + width / 2, top + height / 2),
                Point(left + width - 8, top + height - 8),
            };
            foreach (POINT p in probes)
            {
                IntPtr h = MonitorFromPoint(p, MONITOR_DEFAULTTONULL);
                if (h == IntPtr.Zero) continue;
                MONITORINFO info = new MONITORINFO();
                info.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
                if (!GetMonitorInfo(h, ref info)) continue;
                if (Intersects(info.rcMonitor, left, top, width, height)) return true;
            }
            return false;
        }

        /// <summary>把离屏的坐标拉回最近显示器的可用区，供「记住的位置已失效」时兜底。</summary>
        public static void ClampToNearestMonitor(ref double left, ref double top, double width, double height)
        {
            POINT p = Point(left + width / 2, top + height / 2);
            IntPtr h = MonitorFromPoint(p, MONITOR_DEFAULTTONEAREST);
            if (h == IntPtr.Zero) return;
            MONITORINFO info = new MONITORINFO();
            info.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            if (!GetMonitorInfo(h, ref info)) return;

            double workLeft = info.rcWork.Left;
            double workTop = info.rcWork.Top;
            double workRight = info.rcWork.Right;
            double workBottom = info.rcWork.Bottom;

            if (left + width > workRight) left = workRight - width;
            if (top + height > workBottom) top = workBottom - height;
            if (left < workLeft) left = workLeft;
            if (top < workTop) top = workTop;
        }

        private static POINT Point(double x, double y)
        {
            POINT p;
            p.X = (int)Math.Round(x);
            p.Y = (int)Math.Round(y);
            return p;
        }

        private static bool Intersects(RECT r, double left, double top, double width, double height)
        {
            return left < r.Right && left + width > r.Left && top < r.Bottom && top + height > r.Top;
        }
    }
}
