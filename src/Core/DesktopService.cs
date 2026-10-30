using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using DeskOrder.Interop;

namespace DeskOrder.Core
{
    public class DesktopItem
    {
        public string Path { get; set; }
        public string DisplayName { get; set; }
        public bool IsPublic { get; set; }
        public bool Selected { get; set; }

        public DesktopItem()
        {
            Path = "";
            DisplayName = "";
            Selected = true;
        }
    }

    public static class DesktopService
    {
        // Used by the self-tests and by --desktop-dir so they never touch the real desktop.
        public static string DesktopOverride { get; set; }

        // Set together with DesktopOverride when the public desktop must be ignored too
        // (screenshots for the repository must not show the real machine's shortcuts).
        public static string PublicDesktopOverride { get; set; }

        public static List<DesktopItem> EnumerateShortcuts()
        {
            var result = new List<DesktopItem>();
            string user = string.IsNullOrEmpty(DesktopOverride) ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) : DesktopOverride;
            string pub = string.IsNullOrEmpty(PublicDesktopOverride) ? Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory) : PublicDesktopOverride;
            AddFrom(result, user, false);
            AddFrom(result, pub, true);
            return result;
        }

        private static void AddFrom(List<DesktopItem> result, string dir, bool isPublic)
        {
            try
            {
                if (!Directory.Exists(dir)) return;
                foreach (string file in Directory.GetFiles(dir, "*.lnk"))
                    TryAdd(result, file, isPublic);
                foreach (string file in Directory.GetFiles(dir, "*.url"))
                    TryAdd(result, file, isPublic);
            }
            catch (Exception ex) { Log.Error("Enumerate desktop shortcuts", ex); }
        }

        private static void TryAdd(List<DesktopItem> result, string file, bool isPublic)
        {
            try
            {
                FileAttributes attr = File.GetAttributes(file);
                if ((attr & FileAttributes.Hidden) != 0 || (attr & FileAttributes.System) != 0) return;
                var item = new DesktopItem();
                item.Path = file;
                item.DisplayName = Path.GetFileNameWithoutExtension(file);
                item.IsPublic = isPublic;
                item.Selected = !isPublic;
                result.Add(item);
            }
            catch { }
        }

        public static bool IsOnUserDesktop(string path)
        {
            try
            {
                string source = string.IsNullOrEmpty(DesktopOverride) ? Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory) : DesktopOverride;
                string desktop = Path.GetFullPath(source).TrimEnd('\\');
                string dir = Path.GetFullPath(Path.GetDirectoryName(path)).TrimEnd('\\');
                return string.Equals(desktop, dir, StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }
    }

    public static class DesktopIcons
    {
        private const uint WM_COMMAND = 0x0111;
        private const int HideIconsValue = 1;

        public static bool IsHidden()
        {
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced"))
                {
                    if (key == null) return false;
                    object v = key.GetValue("HideIcons");
                    return v != null && Convert.ToInt32(v) != 0;
                }
            }
            catch { return false; }
        }

        public static bool SetVisible(bool visible)
        {
            try
            {
                bool hidden = IsHidden();
                bool wantHidden = !visible;
                if (hidden == wantHidden) return true;
                IntPtr defView = FindDefView();
                if (defView == IntPtr.Zero)
                {
                    Log.Warn("DefView not found; cannot toggle desktop icons");
                    return false;
                }
                IntPtr result;
                Win32.SendMessageTimeout(defView, WM_COMMAND, new IntPtr(0x7402), IntPtr.Zero, Win32.SMTO_ABORTIFHUNG, 1500, out result);
                System.Threading.Thread.Sleep(350);
                bool now = IsHidden();
                if (now == wantHidden) { Log.Info("Desktop icons " + (wantHidden ? "hidden" : "visible")); return true; }
                Log.Warn("Desktop icon toggle did not change state");
                return false;
            }
            catch (Exception ex)
            {
                Log.Error("SetVisible desktop icons", ex);
                return false;
            }
        }

        public static IntPtr FindDefView()
        {
            IntPtr progman = Win32.FindWindow("Progman", null);
            IntPtr view = IntPtr.Zero;
            if (progman != IntPtr.Zero)
                view = Win32.FindWindowEx(progman, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (view != IntPtr.Zero) return view;
            foreach (IntPtr h in Win32.FindTopLevelByClass("WorkerW"))
            {
                view = Win32.FindWindowEx(h, IntPtr.Zero, "SHELLDLL_DefView", null);
                if (view != IntPtr.Zero) return view;
            }
            return IntPtr.Zero;
        }
    }
}
