using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskOrder.App;
using DeskOrder.Core;
using DeskOrder.Interop;
using DeskOrder.UI;

namespace DeskOrder.Windows
{
    public class MusicWidget : Window
    {
        private readonly DeskOrderApp _app;
        private readonly Border _card;
        private readonly Border _cover;
        private readonly TextBlock _title;
        private readonly TextBlock _artist;
        private readonly TextBlock _time;
        private readonly ThinSlider _progress;
        private readonly ThinSlider _volume;
        private readonly Border _playButton;
        private readonly StackPanel _playContent;
        private readonly SpectrumControl _spectrum;
        private readonly DispatcherTimer _timer;
        private bool _updatingProgress;

        public MusicWidget(DeskOrderApp app)
        {
            _app = app;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            Topmost = true;
            SizeToContent = SizeToContent.Manual;
            Width = app.Settings.Music.WidgetWidth;
            Height = app.Settings.Music.WidgetHeight;
            SnapsToDevicePixels = true;

            _card = new Border();
            _card.Margin = new Thickness(14);
            _card.CornerRadius = new CornerRadius(24);
            _card.BorderThickness = new Thickness(1);
            _card.BorderBrush = new SolidColorBrush(Color.FromArgb(48, 255, 255, 255));
            _card.Background = new LinearGradientBrush(Color.FromArgb(247, 23, 30, 44), Color.FromArgb(247, 9, 13, 21), new Point(0, 0), new Point(0, 1));
            _card.Effect = new DropShadowEffect { BlurRadius = 32, ShadowDepth = 0, Opacity = 0.55, Color = Colors.Black };
            Content = _card;

            var root = new Grid();
            root.Margin = new Thickness(18, 14, 18, 16);
            _card.Child = root;
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(48) });

            // Header
            var header = new Grid();
            var headLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            var accentDot = new System.Windows.Shapes.Ellipse { Width = 6, Height = 6, Fill = Theme.Accent2Brush, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
            headLeft.Children.Add(accentDot);
            headLeft.Children.Add(UiKit.Text("音乐律动", 12, Theme.MutedBrush, FontWeights.SemiBold));
            header.Children.Add(headLeft);
            var headRight = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            var playlistBtn = UiKit.IconButton("\uE8FD", 28, 13, delegate { _app.ShowMain(2); });
            var closeBtn = new Border { Width = 28, Height = 28, CornerRadius = new CornerRadius(14), Background = Brushes.Transparent, Cursor = Cursors.Hand };
            var closeCross = UiKit.PathIcon("M 6,6 L 16,16 M 16,6 L 6,16", null, Theme.MutedBrush, 1.4);
            closeCross.HorizontalAlignment = HorizontalAlignment.Center;
            closeCross.VerticalAlignment = VerticalAlignment.Center;
            closeBtn.Child = closeCross;
            closeBtn.MouseEnter += delegate { closeBtn.Background = Theme.Surface2Brush; closeCross.Stroke = Theme.TextBrush; };
            closeBtn.MouseLeave += delegate { closeBtn.Background = Brushes.Transparent; closeCross.Stroke = Theme.MutedBrush; };
            closeBtn.MouseLeftButtonUp += delegate { HideWidget(); };
            headRight.Children.Add(playlistBtn);
            headRight.Children.Add(closeBtn);
            header.Children.Add(headRight);
            Grid.SetRow(header, 0);
            root.Children.Add(header);
            headLeft.Cursor = Cursors.SizeAll;
            headLeft.MouseLeftButtonDown += delegate (object s, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch { } } };

            // Body
            var body = new Grid();
            body.Margin = new Thickness(0, 10, 0, 0);
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(104) });
            body.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(body, 1);
            root.Children.Add(body);

            _cover = new Border();
            _cover.Width = 104;
            _cover.Height = 104;
            _cover.CornerRadius = new CornerRadius(18);
            _cover.Background = new LinearGradientBrush(Color.FromRgb(45, 62, 112), Color.FromRgb(22, 98, 91), new Point(0, 0), new Point(1, 1));
            _cover.Margin = new Thickness(0, 2, 14, 0);
            _cover.VerticalAlignment = VerticalAlignment.Top;
            var coverFallback = UiKit.Text("♪", 40, Theme.TextBrush, FontWeights.Normal);
            coverFallback.FontFamily = new FontFamily("Segoe UI Symbol");
            coverFallback.HorizontalAlignment = HorizontalAlignment.Center;
            coverFallback.VerticalAlignment = VerticalAlignment.Center;
            _cover.Child = coverFallback;
            body.Children.Add(_cover);

            var info = new StackPanel();
            info.Margin = new Thickness(0, 0, 0, 0);
            Grid.SetColumn(info, 1);
            body.Children.Add(info);

            _title = UiKit.Text("未在播放", 16, Theme.TextBrush, FontWeights.SemiBold);
            _title.TextTrimming = TextTrimming.CharacterEllipsis;
            _title.TextWrapping = TextWrapping.NoWrap;
            info.Children.Add(_title);
            _artist = UiKit.Text("点击选择音乐，或播放任意应用让 Dock 跟着律动", 11.5, Theme.DimBrush, null);
            _artist.Margin = new Thickness(0, 5, 0, 0);
            _artist.TextTrimming = TextTrimming.CharacterEllipsis;
            _artist.TextWrapping = TextWrapping.NoWrap;
            info.Children.Add(_artist);

            var progressRow = new Grid();
            progressRow.Margin = new Thickness(0, 12, 0, 0);
            progressRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            progressRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _progress = new ThinSlider(0);
            _progress.VerticalAlignment = VerticalAlignment.Center;
            _progress.ValueChanged += delegate
            {
                if (_updatingProgress) return;
                if (_app.Player.Current != null) _app.Player.Seek(_progress.Value * Math.Max(1, _app.Player.Duration));
            };
            progressRow.Children.Add(_progress);
            _time = UiKit.Text("0:00 / 0:00", 10.5, Theme.DimBrush, null);
            _time.Margin = new Thickness(10, 0, 0, 0);
            _time.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_time, 1);
            progressRow.Children.Add(_time);
            info.Children.Add(progressRow);

            var controls = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var prev = MediaButton(Geometry.Parse("M 2,4 L 4,4 L 4,20 L 2,20 Z M 18,4 L 18,20 L 6,12 Z"), 30, delegate { _app.Player.Previous(); });
            _playContent = new StackPanel { Orientation = Orientation.Horizontal };
            _playButton = MediaButton(null, 38, delegate { _app.Player.Toggle(); });
            RefreshPlayGlyph();
            var next = MediaButton(Geometry.Parse("M 16,4 L 18,4 L 18,20 L 16,20 Z M 2,4 L 2,20 L 14,12 Z"), 30, delegate { _app.Player.Next(true); });
            controls.Children.Add(prev);
            controls.Children.Add(_playButton);
            controls.Children.Add(next);

            var vol = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(16, 0, 0, 0) };
            var volIcon = UiKit.Text("音量", 10.5, Theme.DimBrush, null);
            volIcon.VerticalAlignment = VerticalAlignment.Center;
            vol.Children.Add(volIcon);
            _volume = new ThinSlider(_app.Settings.Music.Volume);
            _volume.Width = 70;
            _volume.Margin = new Thickness(8, 0, 0, 0);
            _volume.VerticalAlignment = VerticalAlignment.Center;
            _volume.ValueChanged += delegate { _app.Player.SetVolume(_volume.Value); };
            vol.Children.Add(_volume);
            controls.Children.Add(vol);
            info.Children.Add(controls);

            // Spectrum
            _spectrum = new SpectrumControl();
            _spectrum.Audio = _app.Audio;
            _spectrum.Player = _app.Player;
            _spectrum.VisualStyle = _app.Settings.Music.Style;
            _spectrum.Intensity = _app.Settings.Music.Intensity;
            _spectrum.PlayerOnly = _app.Settings.Music.Source == "app";
            _spectrum.Margin = new Thickness(0, 8, 0, 0);
            _spectrum.Height = 38;
            Grid.SetRow(_spectrum, 2);
            root.Children.Add(_spectrum);

            _app.Player.TrackChanged += delegate { RefreshTrack(); };
            _app.Player.StateChanged += delegate { RefreshTrack(); };
            _app.Player.Changed += delegate { RefreshTrack(); };
            _timer = new DispatcherTimer(DispatcherPriority.Background);
            _timer.Interval = TimeSpan.FromMilliseconds(280);
            _timer.Tick += delegate { Tick(); };
            _timer.Start();
            RefreshTrack();

            SourceInitialized += delegate
            {
                try
                {
                    IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                    Win32.EnableWindowChrome(hwnd, true, false);
                    int corner = 2;
                    Win32.DwmSetWindowAttribute(hwnd, 33, ref corner, 4);
                }
                catch { }
            };
            LocationChanged += delegate
            {
                if (!IsVisible) return;
                _app.Settings.Music.WidgetLeft = Left;
                _app.Settings.Music.WidgetTop = Top;
            };
            MouseEnter += delegate { _app.Settings.Music.WidgetVisible = true; };
        }

        private Border MediaButton(Geometry geometry, double size, Action click)
        {
            var b = new Border();
            b.Width = size;
            b.Height = size;
            b.CornerRadius = new CornerRadius(size / 2);
            b.Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
            b.Margin = new Thickness(0, 0, 8, 0);
            b.Cursor = Cursors.Hand;
            b.VerticalAlignment = VerticalAlignment.Center;
            if (geometry != null)
            {
                var p = new System.Windows.Shapes.Path();
                p.Data = geometry;
                p.Fill = Theme.TextBrush;
                p.Stroke = null;
                p.Stretch = Stretch.None;
                p.Width = 22;
                p.Height = 22;
                p.HorizontalAlignment = HorizontalAlignment.Center;
                p.VerticalAlignment = VerticalAlignment.Center;
                b.Child = p;
            }
            b.MouseEnter += delegate { b.Background = new SolidColorBrush(Color.FromArgb(42, 255, 255, 255)); };
            b.MouseLeave += delegate { b.Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)); };
            b.MouseLeftButtonUp += delegate { click(); };
            return b;
        }

        private void RefreshPlayGlyph()
        {
            _playButton.Child = null;
            var glyph = UiKit.PlayGlyph(_app.Player.IsPlaying, 17, Theme.TextOnAccent);
            glyph.HorizontalAlignment = HorizontalAlignment.Center;
            glyph.VerticalAlignment = VerticalAlignment.Center;
            var host = new Grid();
            host.Children.Add(glyph);
            _playButton.Child = host;
            _playButton.Background = Theme.AccentGradient;
        }

        private void RefreshTrack()
        {
            TrackInfo t = _app.Player.Current;
            _title.Text = t == null ? "未在播放" : t.DisplayTitle;
            _artist.Text = t == null ? "点击选择音乐，或播放任意应用让 Dock 跟着律动" : (string.IsNullOrEmpty(t.Artist) ? "未知艺术家" : t.Artist);
            BitmapSource cover = t == null ? null : t.Cover;
            if (cover != null)
            {
                var brush = new ImageBrush(cover);
                brush.Stretch = Stretch.UniformToFill;
                _cover.Background = brush;
                _cover.Child = null;
            }
            else
            {
                _cover.Background = new LinearGradientBrush(Color.FromRgb(45, 62, 112), Color.FromRgb(22, 98, 91), new Point(0, 0), new Point(1, 1));
                if (_cover.Child == null)
                {
                    var fallback = UiKit.Text("♪", 40, Theme.TextBrush, FontWeights.Normal);
                    fallback.FontFamily = new FontFamily("Segoe UI Symbol");
                    fallback.HorizontalAlignment = HorizontalAlignment.Center;
                    fallback.VerticalAlignment = VerticalAlignment.Center;
                    _cover.Child = fallback;
                }
            }
            RefreshPlayGlyph();
            _updatingProgress = true;
            _progress.Value = 0;
            _updatingProgress = false;
        }

        private void Tick()
        {
            if (!IsVisible) return;
            TrackInfo t = _app.Player.Current;
            if (t == null) return;
            double dur = Math.Max(0.001, _app.Player.Duration);
            double pos = Math.Max(0, Math.Min(dur, _app.Player.Position));
            _updatingProgress = true;
            _progress.Value = pos / dur;
            _updatingProgress = false;
            _time.Text = Format(pos) + " / " + Format(dur);
            _playButton.ToolTip = _app.Player.IsPlaying ? "暂停" : "播放";
        }

        private static string Format(double seconds)
        {
            if (double.IsNaN(seconds) || seconds < 0) seconds = 0;
            int total = (int)seconds;
            return (total / 60) + ":" + (total % 60).ToString("00");
        }

        public void ApplySettings()
        {
            if (_spectrum != null)
            {
                _spectrum.VisualStyle = _app.Settings.Music.Style;
                _spectrum.Intensity = _app.Settings.Music.Intensity;
                _spectrum.PlayerOnly = _app.Settings.Music.Source == "app";
            }
        }

        public void ShowNearDock()
        {
            bool firstShow = !IsVisible;
            if (firstShow)
            {
                Show();
                Topmost = true;
            }
            if (firstShow) PlaceWidgetPhysical();
            RefreshTrack();
            _app.Settings.Music.WidgetVisible = true;
            _app.Settings.Music.WidgetWidth = (int)Width;
            _app.Settings.Music.WidgetHeight = (int)Height;
        }

        private void PlaceWidgetPhysical()
        {
            try
            {
                UpdateLayout();
                IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                Win32.RECT wr;
                if (!Win32.GetWindowRect(hwnd, out wr)) return;
                int w = wr.Width;
                int h = wr.Height;
                IntPtr mon = Win32.MonitorFromWindow(hwnd, Win32.MONITOR_DEFAULTTONEAREST);
                var info = new Win32.MONITORINFO();
                info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Win32.MONITORINFO));
                Win32.GetMonitorInfo(mon, ref info);
                int x = info.rcWork.Left + (info.rcWork.Width - w) / 2;
                int y = info.rcWork.Bottom - h - 84;
                if (_app.DockWindow != null && _app.DockWindow.IsDockVisible)
                {
                    IntPtr dockHwnd = new System.Windows.Interop.WindowInteropHelper(_app.DockWindow).Handle;
                    Win32.RECT dr;
                    if (Win32.GetWindowRect(dockHwnd, out dr))
                    {
                        x = dr.Left + (dr.Width - w) / 2;
                        y = dr.Top - h - 10;
                    }
                }
                if (x < info.rcWork.Left + 10) x = info.rcWork.Left + 10;
                if (x + w > info.rcWork.Right - 10) x = info.rcWork.Right - w - 10;
                if (y < info.rcWork.Top + 10) y = info.rcWork.Top + 10;
                if (y + h > info.rcWork.Bottom - 10) y = info.rcWork.Bottom - h - 10;
                Win32.SetWindowPos(hwnd, Win32.HWND_TOPMOST, x, y, w, h, Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW);
            }
            catch (Exception ex) { Log.Warn("PlaceWidgetPhysical: " + ex.Message); }
        }

        public void HideWidget()
        {
            Hide();
            _app.Settings.Music.WidgetVisible = false;
            SettingsStore.Save(_app.Settings);
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_app.Exiting) return;
            e.Cancel = true;
            HideWidget();
        }
    }
}
