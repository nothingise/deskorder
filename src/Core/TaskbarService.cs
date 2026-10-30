using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using DeskOrder.Interop;

namespace DeskOrder.Core
{
    public class TaskbarService : IDisposable
    {
        private readonly AppSettings _settings;
        private Timer _timer;
        private Timer _previewTimer;
        private bool _applied;
        private bool _autoHideApplied;
        private int _autoHideOriginalState = -1;
        private readonly object _gate = new object();

        public bool IsApplied { get { return _applied; } }
        public bool IsAutoHideApplied { get { return _autoHideApplied; } }
        public string LastError { get; private set; }

        public TaskbarService(AppSettings settings)
        {
            _settings = settings;
        }

        public void StartIfEnabled()
        {
            if (_settings.Taskbar.Enabled) Apply();
        }

        public bool Apply()
        {
            lock (_gate)
            {
                try
                {
                    TaskbarSettings cfg = _settings.Taskbar;
                    int state = cfg.Mode == "acrylic" ? Win32.ACCENT_ENABLE_ACRYLICBLURBEHIND :
                                cfg.Mode == "blur" ? Win32.ACCENT_ENABLE_BLURBEHIND :
                                Win32.ACCENT_ENABLE_TRANSPARENTGRADIENT;
                    int alpha = cfg.Mode == "clear" ? 0 : (int)Math.Round(cfg.Opacity / 100.0 * 255.0);
                    int color = ParseTint(cfg.Tint, alpha);
                    int flags = cfg.Mode == "clear" ? 0 : 2;

                    List<IntPtr> bars = Accent.Taskbars(cfg.Secondary);
                    if (bars.Count == 0)
                    {
                        LastError = "未找到任务栏窗口";
                        Log.Warn("Taskbar apply: no taskbar windows found");
                        return false;
                    }
                    bool any = false;
                    foreach (IntPtr bar in bars)
                    {
                        bool ok = Accent.Apply(bar, state, flags, color);
                        foreach (IntPtr child in Accent.BridgeChildren(bar)) ok |= Accent.Apply(child, state, flags, color);
                        any |= ok;
                    }
                    Win32.DwmFlush();
                    _applied = any;
                    if (!any) LastError = "系统未接受任务栏透明设置，可尝试“沉浸模式（自动隐藏任务栏）”";
                    else LastError = "";
                    Log.Info("Taskbar apply mode=" + cfg.Mode + " bars=" + bars.Count + " ok=" + any);
                    StartReapplyTimer();
                    return any;
                }
                catch (Exception ex)
                {
                    LastError = ex.Message;
                    Log.Error("Taskbar apply failed", ex);
                    return false;
                }
            }
        }

        public void Disable()
        {
            lock (_gate)
            {
                try
                {
                    StopReapplyTimer();
                    foreach (IntPtr bar in Accent.Taskbars(true))
                    {
                        Accent.Disable(bar);
                        foreach (IntPtr child in Accent.BridgeChildren(bar)) Accent.Disable(child);
                        Win32.RedrawWindow(bar, IntPtr.Zero, IntPtr.Zero,
                            Win32.RDW_INVALIDATE | Win32.RDW_ALLCHILDREN | Win32.RDW_UPDATENOW | Win32.RDW_FRAME);
                    }
                    Win32.DwmFlush();
                    _applied = false;
                    Log.Info("Taskbar disabled");
                }
                catch (Exception ex)
                {
                    Log.Error("Taskbar disable failed", ex);
                }
            }
        }

        public void Preview(int seconds, Action after)
        {
            Apply();
            if (_previewTimer != null) _previewTimer.Dispose();
            _previewTimer = new Timer(delegate
            {
                Disable();
                if (after != null) after();
            }, null, Math.Max(1, seconds) * 1000, Timeout.Infinite);
        }

        private void StartReapplyTimer()
        {
            if (_timer == null)
            {
                _timer = new Timer(delegate
                {
                    if (_settings.Taskbar.Enabled) ReapplyIfNeeded();
                }, null, 15000, 15000);
            }
        }

        private void ReapplyIfNeeded()
        {
            try
            {
                TaskbarSettings cfg = _settings.Taskbar;
                if (!cfg.Enabled) return;
                int state = cfg.Mode == "acrylic" ? Win32.ACCENT_ENABLE_ACRYLICBLURBEHIND :
                            cfg.Mode == "blur" ? Win32.ACCENT_ENABLE_BLURBEHIND :
                            Win32.ACCENT_ENABLE_TRANSPARENTGRADIENT;
                int alpha = cfg.Mode == "clear" ? 0 : (int)Math.Round(cfg.Opacity / 100.0 * 255.0);
                int color = ParseTint(cfg.Tint, alpha);
                int flags = cfg.Mode == "clear" ? 0 : 2;
                foreach (IntPtr bar in Accent.Taskbars(cfg.Secondary))
                {
                    Accent.Apply(bar, state, flags, color);
                    foreach (IntPtr child in Accent.BridgeChildren(bar)) Accent.Apply(child, state, flags, color);
                }
            }
            catch { }
        }

        public bool SetAutoHide(bool enabled)
        {
            try
            {
                var abd = new Win32.APPBARDATA();
                abd.cbSize = Marshal.SizeOf(typeof(Win32.APPBARDATA));
                uint current = (uint)Win32.SHAppBarMessage(Win32.ABM_GETSTATE, ref abd).ToUInt64();
                if (_autoHideOriginalState < 0) _autoHideOriginalState = (int)current;
                int target = enabled ? ((int)current | Win32.ABS_AUTOHIDE) : ((int)current & ~Win32.ABS_AUTOHIDE);
                abd.lParam = new IntPtr(target);
                Win32.SHAppBarMessage(Win32.ABM_SETSTATE, ref abd);
                System.Threading.Thread.Sleep(120);
                uint now = (uint)Win32.SHAppBarMessage(Win32.ABM_GETSTATE, ref abd).ToUInt64();
                bool on = (now & Win32.ABS_AUTOHIDE) != 0;
                _autoHideApplied = enabled;
                if (on != enabled) LastError = "任务栏自动隐藏没有生效";
                else LastError = "";
                Log.Info("Taskbar auto-hide=" + enabled + " state=0x" + now.ToString("X"));
                return on == enabled;
            }
            catch (Exception ex)
            {
                LastError = ex.Message;
                Log.Error("Taskbar auto-hide failed", ex);
                return false;
            }
        }

        public void RestoreAutoHide()
        {
            try
            {
                if (_autoHideOriginalState < 0) return;
                var abd = new Win32.APPBARDATA();
                abd.cbSize = Marshal.SizeOf(typeof(Win32.APPBARDATA));
                abd.lParam = new IntPtr(_autoHideOriginalState);
                Win32.SHAppBarMessage(Win32.ABM_SETSTATE, ref abd);
                _autoHideApplied = false;
                _autoHideOriginalState = -1;
                Log.Info("Taskbar auto-hide restored");
            }
            catch (Exception ex) { Log.Error("RestoreAutoHide", ex); }
        }

        public void Repair()
        {
            try
            {
                Disable();
                Win32.DwmFlush();
                foreach (IntPtr bar in Accent.Taskbars(true))
                {
                    Win32.RedrawWindow(bar, IntPtr.Zero, IntPtr.Zero,
                        Win32.RDW_INVALIDATE | Win32.RDW_ALLCHILDREN | Win32.RDW_UPDATENOW | Win32.RDW_FRAME);
                    IntPtr r1;
                    Win32.SendMessageTimeout(bar, 0x031A, IntPtr.Zero, IntPtr.Zero, 0x0002, 800, out r1);
                }
                IntPtr r2;
                IntPtr text = Marshal.StringToHGlobalUni("ImmersiveColorSet");
                try { Win32.SendMessageTimeout(new IntPtr(0xFFFF), 0x001A, IntPtr.Zero, text, 0x0002, 800, out r2); }
                finally { Marshal.FreeHGlobal(text); }
                Log.Info("Taskbar repair requested");
            }
            catch (Exception ex) { Log.Error("Taskbar repair failed", ex); }
        }

        private void StopReapplyTimer()
        {
            if (_timer != null) { _timer.Dispose(); _timer = null; }
        }

        public static int ParseTint(string hex, int alpha)
        {
            int r = 16, g = 20, b = 24;
            if (!string.IsNullOrEmpty(hex) && hex.Length == 7 && hex[0] == '#')
            {
                try
                {
                    r = Convert.ToInt32(hex.Substring(1, 2), 16);
                    g = Convert.ToInt32(hex.Substring(3, 2), 16);
                    b = Convert.ToInt32(hex.Substring(5, 2), 16);
                }
                catch { }
            }
            return Accent.Abgr(r, g, b, alpha);
        }

        public void Dispose()
        {
            StopReapplyTimer();
            if (_previewTimer != null) _previewTimer.Dispose();
        }
    }
}
