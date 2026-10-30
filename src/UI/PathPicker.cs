using System;
using System.Windows;
using System.Windows.Interop;

namespace DeskOrder.UI
{
    /// <summary>Win32 file/folder pickers, shared by the Dock page and the dock menu.</summary>
    public static class PathPicker
    {
        public const string ProgramFilter = "程序与快捷方式|*.exe;*.lnk;*.url;*.bat;*.cmd;*.ps1|所有文件|*.*";

        public static string[] PickFiles(string title) { return PickFiles(title, null); }

        /// <summary>Owner is the window that opened us: a topmost dock window would
        /// otherwise cover its own modal dialog, which looks like a dead button.</summary>
        public static string[] PickFiles(string title, Window owner)
        {
            var dialog = new System.Windows.Forms.OpenFileDialog();
            dialog.Title = string.IsNullOrEmpty(title) ? "添加到 Dock" : title;
            dialog.Multiselect = true;
            dialog.Filter = ProgramFilter;
            if (Show(dialog, owner) != System.Windows.Forms.DialogResult.OK) return new string[0];
            return dialog.FileNames;
        }

        public static string PickFolder(string title) { return PickFolder(title, null); }

        public static string PickFolder(string title, Window owner)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = string.IsNullOrEmpty(title) ? "选择要固定到 Dock 的文件夹" : title;
            dialog.ShowNewFolderButton = false;
            if (Show(dialog, owner) != System.Windows.Forms.DialogResult.OK) return "";
            return dialog.SelectedPath;
        }

        private static System.Windows.Forms.DialogResult Show(System.Windows.Forms.CommonDialog dialog, Window owner)
        {
            System.Windows.Forms.IWin32Window handle = Owner(owner);
            return handle == null ? dialog.ShowDialog() : dialog.ShowDialog(handle);
        }

        private static System.Windows.Forms.IWin32Window Owner(Window window)
        {
            if (window == null) return null;
            try
            {
                IntPtr handle = new WindowInteropHelper(window).Handle;
                if (handle == IntPtr.Zero) return null;
                return new OwnerHandle(handle);
            }
            catch { return null; }
        }

        private sealed class OwnerHandle : System.Windows.Forms.IWin32Window
        {
            private readonly IntPtr _handle;
            public OwnerHandle(IntPtr handle) { _handle = handle; }
            public IntPtr Handle { get { return _handle; } }
        }
    }
}
