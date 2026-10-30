using System;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using DeskOrder.App;
using DeskOrder.Core;
using DeskOrder.Interop;
using DeskOrder.UI;

namespace DeskOrder.Windows
{
    /// <summary>
    /// A bare, transparent spectrum strip that lives on the desktop.
    /// No card, no border, no background and no mouse capture.
    /// </summary>
    public class VisualizerWindow : Window
    {
        private readonly DeskOrderApp _app;
        private readonly SpectrumControl _spectrum;
        private System.Windows.Threading.DispatcherTimer _topTimer;
        private bool _hiddenForFullscreen;
        private int _lastDockTop = int.MinValue;
        private int _lastDockCenterX = int.MinValue;
        private int _lastDockHeight = 0;

        public VisualizerWindow(DeskOrderApp app)
        {
            _app = app;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            Width = 560;
            Height = 76;
            SnapsToDevicePixels = true;

            _spectrum = new SpectrumControl();
            _spectrum.Audio = app.Audio;
            _spectrum.Player = app.Player;
            _spectrum.VisualStyle = app.Settings.Music.Style;
            _spectrum.Intensity = app.Settings.Music.Intensity;
            _spectrum.PlayerOnly = app.Settings.Music.Source == "app";
            _spectrum.HorizontalAlignment = HorizontalAlignment.Stretch;
            _spectrum.VerticalAlignment = VerticalAlignment.Stretch;
            var root = new System.Windows.Controls.Grid();
            root.Background = Brushes.Transparent;
            root.Children.Add(_spectrum);
            Content = root;

            Loaded += delegate { Place(); };
            SourceInitialized += OnSourceInitialized;
        }

        public bool IsOverlayVisible { get { return IsVisible; } }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            try
            {
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                Win32.EnableWindowChrome(hwnd, true, true);
                long ex = Win32.GetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE).ToInt64();
                ex |= Win32.WS_EX_TRANSPARENT;
                Win32.SetWindowLongPtr(hwnd, Win32.GWL_EXSTYLE, new IntPtr(ex));
            }
            catch { }
        }

        public void ShowOverlay()
        {
            _hiddenForFullscreen = false;
            if (!IsVisible) Show();
            Topmost = true;
            Place();
            StartTopTimer();
        }

        public void HideOverlay()
        {
            if (_topTimer != null) _topTimer.Stop();
            if (IsVisible) Hide();
        }

        public void ApplySettings()
        {
            if (_spectrum == null) return;
            _spectrum.VisualStyle = _app.Settings.Music.Style;
            _spectrum.Intensity = _app.Settings.Music.Intensity;
            _spectrum.PlayerOnly = _app.Settings.Music.Source == "app";
            if (IsVisible) Place();
        }

        private void Place()
        {
            try
            {
                if (!IsVisible) return;
                UpdateLayout();
                IntPtr hwnd = new WindowInteropHelper(this).Handle;
                Win32.RECT wr;
                if (!Win32.GetWindowRect(hwnd, out wr)) return;
                int w = wr.Width;
                int h = wr.Height;
                IntPtr mon = Win32.MonitorFromWindow(hwnd, Win32.MONITOR_DEFAULTTONEAREST);
                var info = new Win32.MONITORINFO();
                info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Win32.MONITORINFO));
                Win32.GetMonitorInfo(mon, ref info);

                int x = info.rcWork.Left + (info.rcWork.Width - w) / 2;
                int gap = _app.Settings.Music.VisualizerOffset;
                if (gap < 0) gap = 0;
                // Fallback mirrors where the Dock normally parks: 8 px above the work area.
                int fallbackDockH = _lastDockHeight > 0 ? _lastDockHeight : 110;
                int y = info.rcWork.Bottom - fallbackDockH - 8 - h - gap;
                bool followed = false;
                if (_app.DockWindow != null && _app.DockWindow.IsDockVisible)
                {
                    IntPtr dock = new WindowInteropHelper(_app.DockWindow).Handle;
                    Win32.RECT dr;
                    if (Win32.GetWindowRect(dock, out dr))
                    {
                        // Ignore the Dock while it is parked off-screen (fullscreen
                        // suppression): following it would drag the strip away too.
                        bool onScreen = dr.Bottom > info.rcMonitor.Top + 20 && dr.Top < info.rcMonitor.Bottom - 20;
                        if (onScreen)
                        {
                            _lastDockTop = dr.Top;
                            _lastDockCenterX = dr.Left + dr.Width / 2;
                            _lastDockHeight = dr.Height;
                            y = dr.Top - h - gap;
                            x = dr.Left + (dr.Width - w) / 2;
                            followed = true;
                        }
                    }
                }
                if (!followed && _lastDockTop != int.MinValue)
                {
                    // Keep the last on-screen position while the Dock is hidden/parked.
                    y = _lastDockTop - h - gap;
                    x = _lastDockCenterX - w / 2;
                }
                if (x < info.rcWork.Left + 8) x = info.rcWork.Left + 8;
                if (x + w > info.rcWork.Right - 8) x = info.rcWork.Right - w - 8;
                if (y < info.rcWork.Top + 8) y = info.rcWork.Top + 8;
                if (y + h > info.rcWork.Bottom - 8) y = info.rcWork.Bottom - h - 8;
                Win32.SetWindowPos(hwnd, Win32.HWND_TOPMOST, x, y, w, h, Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW);
            }
            catch (Exception ex) { Log.Warn("Visualizer place: " + ex.Message); }
        }

        private void StartTopTimer()
        {
            if (_topTimer == null)
            {
                _topTimer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Background);
                _topTimer.Interval = TimeSpan.FromMilliseconds(500);
                _topTimer.Tick += delegate { OnTopTick(); };
            }
            _topTimer.Start();
        }

        private void OnTopTick()
        {
            try
            {
                bool fullscreen = _app.Settings.Music.HideVisualizerOnFullscreen && FullscreenDetector.IsActive();
                if (fullscreen)
                {
                    if (IsVisible)
                    {
                        _hiddenForFullscreen = true;
                        Hide();
                    }
                    return;
                }
                if (_hiddenForFullscreen)
                {
                    _hiddenForFullscreen = false;
                    if (_app.Settings.Music.ShowVisualizer) ShowOverlay();
                    return;
                }
                // Re-place every tick so the gap follows the Dock while it moves.
                Place();
            }
            catch { }
        }

        public void CloseOverlay()
        {
            try { if (_topTimer != null) _topTimer.Stop(); Close(); } catch { }
        }
    }
}
