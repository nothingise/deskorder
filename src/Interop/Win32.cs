using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DeskOrder.Interop
{
    public static class Win32
    {
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left; public int Top; public int Right; public int Bottom; public int Width { get { return Right - Left; } } public int Height { get { return Bottom - Top; } } }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT { public int X; public int Y; public POINT(int x, int y) { X = x; Y = y; } }

        [StructLayout(LayoutKind.Sequential)]
        public struct SIZE { public int cx; public int cy; public SIZE(int x, int y) { cx = x; cy = y; } }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MARGINS { public int Left; public int Right; public int Top; public int Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        public struct APPBARDATA
        {
            public int cbSize;
            public IntPtr hWnd;
            public uint uCallbackMessage;
            public uint uEdge;
            public RECT rc;
            public IntPtr lParam;
        }

        [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc proc, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr parent, EnumWindowsProc proc, IntPtr lParam);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder text, int count);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindow(string className, string windowName);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr FindWindowEx(IntPtr parent, IntPtr after, string className, string windowName);
        [DllImport("user32.dll")] public static extern bool IsWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);
        [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] public static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
        [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hWnd, uint cmd);
        [DllImport("user32.dll")] public static extern bool GetCursorPos(out POINT point);
        [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
        [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr hWnd, int cmd);
        [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] public static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);
        [DllImport("user32.dll")] public static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint flags, uint timeout, out IntPtr result);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")] public static extern bool SetWindowCompositionAttribute(IntPtr hWnd, ref WINDOWCOMPOSITIONATTRIBDATA data);
        [DllImport("user32.dll")] public static extern bool RedrawWindow(IntPtr hWnd, IntPtr rect, IntPtr rgn, uint flags);
        [DllImport("user32.dll")] public static extern bool MonitorFromPoint(POINT pt, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);
        [DllImport("user32.dll")] public static extern IntPtr MonitorFromPointEx(POINT pt, uint flags);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern bool GetMonitorInfo(IntPtr monitor, ref MONITORINFO info);
        [DllImport("user32.dll")] public static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr clip, EnumWindowsProc proc, IntPtr data);
        [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hWnd);
        [DllImport("shcore.dll")] public static extern int GetDpiForMonitor(IntPtr monitor, int dpiType, out uint dpiX, out uint dpiY);
        [DllImport("dwmapi.dll")] public static extern int DwmSetWindowAttribute(IntPtr hWnd, int attr, ref int value, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hWnd, int attr, out int value, int size);
        [DllImport("dwmapi.dll")] public static extern int DwmFlush();
        [DllImport("shell32.dll")] public static extern int SHQueryUserNotificationState(out int state);
        [DllImport("shell32.dll")] public static extern UIntPtr SHAppBarMessage(uint message, ref APPBARDATA data);

        public const int GWL_STYLE = -16;
        public const int GWL_EXSTYLE = -20;
        public const long WS_EX_TOOLWINDOW = 0x00000080;
        public const long WS_EX_TRANSPARENT = 0x00000020;
        public const long WS_EX_NOACTIVATE = 0x08000000;
        public const uint GW_CHILD = 5;
        public const uint GW_HWNDNEXT = 2;
        public const int SW_HIDE = 0;
        public const int SW_SHOWNORMAL = 1;
        public const int SW_MINIMIZE = 6;
        public const int SW_RESTORE = 9;
        public const int SW_SHOWNOACTIVATE = 4;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOZORDER = 0x0004;
        public const uint SWP_NOACTIVATE = 0x0010;
        public const uint SWP_SHOWWINDOW = 0x0040;
        public const uint SWP_FRAMECHANGED = 0x0020;
        public static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        public const uint ABM_GETSTATE = 4;
        public const uint ABM_SETSTATE = 10;
        public const int ABS_AUTOHIDE = 1;
        public const int ABS_ALWAYSONTOP = 2;
        public const uint RDW_INVALIDATE = 0x0001;
        public const uint RDW_ALLCHILDREN = 0x0080;
        public const uint RDW_UPDATENOW = 0x0100;
        public const uint RDW_FRAME = 0x0400;
        public const uint MONITOR_DEFAULTTONEAREST = 2;
        public const uint MONITOR_DEFAULTTOPRIMARY = 1;
        public const int MDT_EFFECTIVE_DPI = 0;
        public const uint SMTO_ABORTIFHUNG = 0x0002;

        [StructLayout(LayoutKind.Sequential)]
        public struct ACCENT_POLICY
        {
            public int AccentState;
            public int AccentFlags;
            public int GradientColor;
            public int AnimationId;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct WINDOWCOMPOSITIONATTRIBDATA
        {
            public int Attribute;
            public IntPtr Data;
            public int SizeOfData;
        }

        public const int WCA_ACCENT_POLICY = 19;
        public const int ACCENT_DISABLED = 0;
        public const int ACCENT_ENABLE_GRADIENT = 1;
        public const int ACCENT_ENABLE_TRANSPARENTGRADIENT = 2;
        public const int ACCENT_ENABLE_BLURBEHIND = 3;
        public const int ACCENT_ENABLE_ACRYLICBLURBEHIND = 4;

        public static string ClassNameOf(IntPtr hWnd)
        {
            var sb = new StringBuilder(256);
            GetClassName(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public static string TextOf(IntPtr hWnd)
        {
            var sb = new StringBuilder(512);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        public static List<IntPtr> TopLevelWindows()
        {
            var list = new List<IntPtr>();
            EnumWindows(delegate (IntPtr h, IntPtr l) { list.Add(h); return true; }, IntPtr.Zero);
            return list;
        }

        public static List<IntPtr> Children(IntPtr parent)
        {
            var list = new List<IntPtr>();
            EnumChildWindows(parent, delegate (IntPtr h, IntPtr l) { list.Add(h); return true; }, IntPtr.Zero);
            return list;
        }

        public static List<IntPtr> FindTopLevelByClass(string className)
        {
            var list = new List<IntPtr>();
            foreach (IntPtr h in TopLevelWindows())
            {
                if (ClassNameOf(h) == className) list.Add(h);
            }
            return list;
        }

        public static bool TryGetRect(IntPtr hWnd, out RECT rect)
        {
            return GetWindowRect(hWnd, out rect);
        }

        public static int ScalePercent(IntPtr hWnd)
        {
            try
            {
                uint dpi = GetDpiForWindow(hWnd);
                if (dpi == 0) return 100;
                return (int)Math.Round(dpi / 96.0 * 100.0);
            }
            catch { return 100; }
        }

        public static MONITORINFO MonitorInfoOf(IntPtr hWnd)
        {
            IntPtr mon = MonitorFromWindow(hWnd, MONITOR_DEFAULTTONEAREST);
            var info = new MONITORINFO();
            info.cbSize = Marshal.SizeOf(typeof(MONITORINFO));
            GetMonitorInfo(mon, ref info);
            return info;
        }

        public static void EnableWindowChrome(IntPtr hWnd, bool toolWindow, bool noActivate)
        {
            try
            {
                long ex = GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64();
                if (noActivate) ex |= WS_EX_NOACTIVATE;
                else ex &= ~WS_EX_NOACTIVATE;
                if (toolWindow) ex |= WS_EX_TOOLWINDOW;
                else ex &= ~WS_EX_TOOLWINDOW;
                SetWindowLongPtr(hWnd, GWL_EXSTYLE, new IntPtr(ex));
            }
            catch { }
        }
    }
}
