using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DeskOrder.Interop
{
    public class ShortcutInfo
    {
        public string TargetPath { get; set; }
        public string Arguments { get; set; }
        public string WorkingDirectory { get; set; }
        public string IconPath { get; set; }
        public int IconIndex { get; set; }
    }

    public static class ShortcutResolver
    {
        [ComImport, Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLinkCoClass { }

        [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder file, int maxPath, IntPtr data, uint flags);
            void GetIDList(out IntPtr pidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder name, int maxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string name);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder dir, int maxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string dir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder args, int maxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string args);
            void GetHotkey(out ushort hotkey);
            void SetHotkey(ushort hotkey);
            void GetShowCmd(out int showCmd);
            void SetShowCmd(int showCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder iconPath, int maxPath, out int iconIndex);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string iconPath, int iconIndex);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pathRel, uint reserved);
            void Resolve(IntPtr hwnd, uint flags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
        }

        [ComImport, Guid("0000010b-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPersistFile
        {
            void GetClassID(out Guid pClassID);
            [PreserveSig] int IsDirty();
            void Load([MarshalAs(UnmanagedType.LPWStr)] string fileName, uint mode);
            void Save([MarshalAs(UnmanagedType.LPWStr)] string fileName, bool remember);
            void SaveCompleted([MarshalAs(UnmanagedType.LPWStr)] string fileName);
            void GetCurFile([MarshalAs(UnmanagedType.LPWStr)] out string fileName);
        }

        public static ShortcutInfo Resolve(string shortcutPath)
        {
            if (string.IsNullOrEmpty(shortcutPath) || !File.Exists(shortcutPath)) return null;
            if (!shortcutPath.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) return null;
            object obj = null;
            try
            {
                obj = new ShellLinkCoClass();
                var link = (IShellLinkW)obj;
                var file = (IPersistFile)obj;
                file.Load(shortcutPath, 0x00000000);
                var sb = new StringBuilder(1024);
                link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0);
                var info = new ShortcutInfo();
                info.TargetPath = sb.ToString();
                sb = new StringBuilder(1024);
                link.GetArguments(sb, sb.Capacity);
                info.Arguments = sb.ToString();
                sb = new StringBuilder(1024);
                link.GetWorkingDirectory(sb, sb.Capacity);
                info.WorkingDirectory = sb.ToString();
                sb = new StringBuilder(1024);
                int iconIndex;
                link.GetIconLocation(sb, sb.Capacity, out iconIndex);
                info.IconPath = sb.ToString();
                info.IconIndex = iconIndex;
                if (string.IsNullOrEmpty(info.TargetPath))
                {
                    // Some shortcuts point at a folder or an app alias; leave it to the shell.
                    info.TargetPath = "";
                }
                return info;
            }
            catch { return null; }
            finally
            {
                if (obj != null) Marshal.ReleaseComObject(obj);
            }
        }

        public static string ResolveTargetPath(string path)
        {
            if (!path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) return path;
            ShortcutInfo info = Resolve(path);
            return info != null && !string.IsNullOrEmpty(info.TargetPath) ? info.TargetPath : path;
        }

        /// <summary>
        /// Writes a .lnk. Used for the Startup-folder autostart fallback, which works when
        /// the registry Run key is unavailable.
        /// </summary>
        public static bool Create(string shortcutPath, string targetPath, string arguments, string workingDirectory)
        {
            if (string.IsNullOrEmpty(shortcutPath) || string.IsNullOrEmpty(targetPath)) return false;
            object obj = null;
            try
            {
                string dir = Path.GetDirectoryName(shortcutPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
                obj = new ShellLinkCoClass();
                var link = (IShellLinkW)obj;
                var file = (IPersistFile)obj;
                link.SetPath(targetPath);
                if (!string.IsNullOrEmpty(arguments)) link.SetArguments(arguments);
                if (!string.IsNullOrEmpty(workingDirectory)) link.SetWorkingDirectory(workingDirectory);
                link.SetIconLocation(targetPath, 0);
                link.SetDescription("DeskOrder");
                file.Save(shortcutPath, true);
                return File.Exists(shortcutPath);
            }
            catch { return false; }
            finally
            {
                if (obj != null) Marshal.ReleaseComObject(obj);
            }
        }
    }
}
