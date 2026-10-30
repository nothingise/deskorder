using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace DeskOrder.Interop
{
    public static class Accent
    {
        public static bool Apply(IntPtr hWnd, int state, int flags, int gradientColor)
        {
            if (hWnd == IntPtr.Zero) return false;
            var policy = new Win32.ACCENT_POLICY();
            policy.AccentState = state;
            policy.AccentFlags = flags;
            policy.GradientColor = gradientColor;
            policy.AnimationId = 0;

            int size = Marshal.SizeOf(typeof(Win32.ACCENT_POLICY));
            IntPtr ptr = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(policy, ptr, false);
                var data = new Win32.WINDOWCOMPOSITIONATTRIBDATA();
                data.Attribute = Win32.WCA_ACCENT_POLICY;
                data.Data = ptr;
                data.SizeOfData = size;
                return Win32.SetWindowCompositionAttribute(hWnd, ref data);
            }
            catch { return false; }
            finally { Marshal.FreeHGlobal(ptr); }
        }

        public static bool Disable(IntPtr hWnd)
        {
            return Apply(hWnd, Win32.ACCENT_DISABLED, 0, 0);
        }

        public static List<IntPtr> Taskbars(bool includeSecondary)
        {
            var result = new List<IntPtr>();
            foreach (IntPtr h in Win32.TopLevelWindows())
            {
                string cls = Win32.ClassNameOf(h);
                if (cls == "Shell_TrayWnd") result.Add(h);
                else if (includeSecondary && cls == "Shell_SecondaryTrayWnd") result.Add(h);
            }
            return result;
        }

        public static List<IntPtr> BridgeChildren(IntPtr taskbar)
        {
            var result = new List<IntPtr>();
            WalkBridgeChildren(taskbar, result, 0);
            return result;
        }

        private static void WalkBridgeChildren(IntPtr parent, List<IntPtr> result, int depth)
        {
            if (depth > 4) return;
            foreach (IntPtr h in Win32.Children(parent))
            {
                string cls = Win32.ClassNameOf(h);
                if (cls == "Windows.UI.Composition.DesktopWindowContentBridge" ||
                    cls.IndexOf("Composition", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    cls.IndexOf("Xaml", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    result.Add(h);
                }
                WalkBridgeChildren(h, result, depth + 1);
            }
        }

        public static int Abgr(int r, int g, int b, int alpha)
        {
            if (alpha < 0) alpha = 0;
            if (alpha > 255) alpha = 255;
            return ((alpha & 0xFF) << 24) | ((b & 0xFF) << 16) | ((g & 0xFF) << 8) | (r & 0xFF);
        }
    }
}
