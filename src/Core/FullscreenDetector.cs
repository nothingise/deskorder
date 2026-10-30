using System;
using DeskOrder.Interop;

namespace DeskOrder.Core
{
    /// <summary>
    /// Shared "is a fullscreen app in the foreground" check, used by both the
    /// Dock and the desktop visualizer so they hide together in games/videos.
    /// </summary>
    public static class FullscreenDetector
    {
        public static bool IsActive()
        {
            try
            {
                int state;
                if (Win32.SHQueryUserNotificationState(out state) == 0 && (state == 3 || state == 4)) return true;
                IntPtr fg = Win32.GetForegroundWindow();
                if (fg == IntPtr.Zero) return false;
                string cls = Win32.ClassNameOf(fg);
                if (cls == "Progman" || cls == "WorkerW" || cls == "Shell_TrayWnd") return false;
                var r = new Win32.RECT();
                if (!Win32.GetWindowRect(fg, out r)) return false;
                IntPtr mon = Win32.MonitorFromWindow(fg, Win32.MONITOR_DEFAULTTONEAREST);
                var info = new Win32.MONITORINFO();
                info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Win32.MONITORINFO));
                Win32.GetMonitorInfo(mon, ref info);
                return r.Left <= info.rcMonitor.Left + 1 && r.Top <= info.rcMonitor.Top + 1 &&
                       r.Right >= info.rcMonitor.Right - 1 && r.Bottom >= info.rcMonitor.Bottom - 1;
            }
            catch { return false; }
        }
    }
}
