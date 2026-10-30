using System;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using DeskOrder.Core;

namespace DeskOrder.App
{
    public class HotkeyService : IDisposable
    {
        private readonly DeskOrderApp _app;
        private HwndSource _source;
        private const int HotkeyDock = 0x4A01;
        private const int HotkeyMusic = 0x4A02;
        private const uint ModControl = 0x0002;
        private const uint ModAlt = 0x0001;
        private const int VkD = 0x44;
        private const int VkM = 0x4D;

        public HotkeyService(DeskOrderApp app)
        {
            _app = app;
            var p = new HwndSourceParameters("DeskOrderHotkeys");
            p.Width = 0;
            p.Height = 0;
            p.WindowStyle = 0;
            p.ExtendedWindowStyle = 0x80;
            _source = new HwndSource(p);
            _source.AddHook(WndProc);
        }

        public void Apply(AppSettings settings)
        {
            UnregisterAll();
            if (settings.General.HotkeyDock)
            {
                if (!RegisterHotKey(_source.Handle, HotkeyDock, ModControl | ModAlt, VkD))
                    Log.Warn("Hotkey Ctrl+Alt+D is already in use");
            }
            if (settings.General.HotkeyMusic)
            {
                if (!RegisterHotKey(_source.Handle, HotkeyMusic, ModControl | ModAlt, VkM))
                    Log.Warn("Hotkey Ctrl+Alt+M is already in use");
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0312)
            {
                int id = wParam.ToInt32();
                if (id == HotkeyDock) { _app.ToggleDock(); handled = true; }
                else if (id == HotkeyMusic) { _app.ToggleMusicWidget(); handled = true; }
            }
            return IntPtr.Zero;
        }

        private void UnregisterAll()
        {
            try
            {
                UnregisterHotKey(_source.Handle, HotkeyDock);
                UnregisterHotKey(_source.Handle, HotkeyMusic);
            }
            catch { }
        }

        public void Dispose()
        {
            UnregisterAll();
            try { _source.Dispose(); } catch { }
        }

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, int vk);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
