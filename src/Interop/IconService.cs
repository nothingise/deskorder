using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;

namespace DeskOrder.Interop
{
    public static class IconService
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern int SHCreateItemFromParsingName(string path, IntPtr pbc, ref Guid riid, out IntPtr ppv);

        [DllImport("gdi32.dll")]
        private static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeSize { public int cx; public int cy; public NativeSize(int x, int y) { cx = x; cy = y; } }

        [ComImport, Guid("bcc18b79-ba16-442f-80c4-8a59c30c463b"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IShellItemImageFactory
        {
            [PreserveSig] int GetImage(NativeSize size, int flags, out IntPtr phbm);
        }

        private const int SIIGBF_RESIZETOFIT = 0x00;
        private const int SIIGBF_BIGGERSIZEOK = 0x01;
        private const int SIIGBF_ICONONLY = 0x04;

        private static readonly object Gate = new object();
        private static readonly Dictionary<string, BitmapSource> Cache = new Dictionary<string, BitmapSource>();

        public static BitmapSource GetIcon(string path, int size)
        {
            return Get(path, size, true);
        }

        public static BitmapSource GetThumbnail(string path, int size)
        {
            BitmapSource img = Get(path, size, false);
            if (img == null) img = Get(path, size, true);
            return img;
        }

        private static BitmapSource Get(string path, int size, bool iconOnly)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string key = path.ToLowerInvariant() + "|" + size + "|" + (iconOnly ? "i" : "t");
            lock (Gate)
            {
                BitmapSource cached;
                if (Cache.TryGetValue(key, out cached)) return cached;
            }

            BitmapSource result = LoadShellImage(path, size, iconOnly);
            if (result != null)
            {
                result.Freeze();
                lock (Gate) Cache[key] = result;
            }
            return result;
        }

        private static BitmapSource LoadShellImage(string path, int size, bool iconOnly)
        {
            IntPtr ppv = IntPtr.Zero;
            try
            {
                Guid iid = typeof(IShellItemImageFactory).GUID;
                int hr = SHCreateItemFromParsingName(path, IntPtr.Zero, ref iid, out ppv);
                if (hr != 0 || ppv == IntPtr.Zero) return null;
                var factory = (IShellItemImageFactory)Marshal.GetObjectForIUnknown(ppv);
                IntPtr hbmp;
                int flags = SIIGBF_BIGGERSIZEOK | (iconOnly ? SIIGBF_ICONONLY : SIIGBF_RESIZETOFIT);
                hr = factory.GetImage(new NativeSize(size, size), flags, out hbmp);
                if (hr != 0 || hbmp == IntPtr.Zero) return null;
                try
                {
                    BitmapSource src = Imaging.CreateBitmapSourceFromHBitmap(hbmp, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
                    return src;
                }
                finally { DeleteObject(hbmp); }
            }
            catch
            {
                return null;
            }
            finally
            {
                if (ppv != IntPtr.Zero) Marshal.Release(ppv);
            }
        }

        public static BitmapSource LoadImageFile(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                var bmp = new BitmapImage();
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.UriSource = new Uri(path, UriKind.Absolute);
                bmp.EndInit();
                bmp.Freeze();
                return bmp;
            }
            catch { return null; }
        }

        public static void ClearCache()
        {
            lock (Gate) Cache.Clear();
        }
    }
}
