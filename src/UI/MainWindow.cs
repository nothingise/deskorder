using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shell;
using System.Windows.Threading;
using DeskOrder.App;
using DeskOrder.Core;
using DeskOrder.Interop;
using DeskOrder.Windows;

namespace DeskOrder.UI
{
    public partial class MainWindow : Window
    {
        private readonly DeskOrderApp _app;
        private readonly Grid _root;
        private readonly Grid _pageHost;
        private readonly Grid _toastLayer;
        private readonly StackPanel _navItems;
        private readonly TextBlock _crumb;
        private readonly TextBlock _status;
        private readonly TextBlock _maxGlyph;
        private PageView _currentPage;
        private int _pageIndex = 0;
        private readonly Border[] _navBorders = new Border[5];
        private readonly TextBlock[] _navTexts = new TextBlock[5];
        private readonly Canvas[] _navIcons = new Canvas[5];

        public MainWindow(DeskOrderApp app)
        {
            _app = app;
            Title = "桌序 DeskOrder";
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            Background = Theme.BgBrush;
            MinWidth = 1060;
            MinHeight = 700;
            Width = Math.Max(1060, app.Settings.Window.Width);
            Height = Math.Max(700, app.Settings.Window.Height);
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            if (app.Settings.Window.Left >= 0 && app.Settings.Window.Top >= 0)
            {
                WindowStartupLocation = WindowStartupLocation.Manual;
                Left = app.Settings.Window.Left;
                Top = app.Settings.Window.Top;
            }
            if (app.Settings.Window.Maximized) WindowState = WindowState.Maximized;
            UseLayoutRounding = true;
            SnapsToDevicePixels = true;
            TextOptions.SetTextFormattingMode(this, TextFormattingMode.Display);

            var chrome = new WindowChrome();
            chrome.CaptionHeight = 0;
            chrome.ResizeBorderThickness = new Thickness(6);
            chrome.GlassFrameThickness = new Thickness(0);
            chrome.CornerRadius = new CornerRadius(0);
            chrome.UseAeroCaptionButtons = false;
            WindowChrome.SetWindowChrome(this, chrome);

            _root = new Grid();
            _root.ClipToBounds = true;
            Content = _root;

            var bg = new Grid();
            bg.Background = new LinearGradientBrush(Color.FromRgb(10, 13, 20), Color.FromRgb(7, 9, 15), new Point(0, 0), new Point(1, 1));
            var glow1 = new System.Windows.Shapes.Ellipse();
            glow1.Width = 760;
            glow1.Height = 760;
            glow1.Fill = new RadialGradientBrush(Color.FromArgb(55, 62, 92, 180), Color.FromArgb(0, 62, 92, 180));
            glow1.HorizontalAlignment = HorizontalAlignment.Left;
            glow1.VerticalAlignment = VerticalAlignment.Top;
            glow1.Margin = new Thickness(-260, -380, 0, 0);
            glow1.IsHitTestVisible = false;
            bg.Children.Add(glow1);
            var glow2 = new System.Windows.Shapes.Ellipse();
            glow2.Width = 640;
            glow2.Height = 640;
            glow2.Fill = new RadialGradientBrush(Color.FromArgb(38, 30, 150, 135), Color.FromArgb(0, 30, 150, 135));
            glow2.HorizontalAlignment = HorizontalAlignment.Right;
            glow2.VerticalAlignment = VerticalAlignment.Bottom;
            glow2.Margin = new Thickness(0, 0, -200, -300);
            glow2.IsHitTestVisible = false;
            bg.Children.Add(glow2);
            _root.Children.Add(bg);

            // Layout: sidebar + main
            var layout = new Grid();
            layout.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(226) });
            layout.ColumnDefinitions.Add(new ColumnDefinition());
            _root.Children.Add(layout);

            var sidebar = new Border();
            sidebar.Background = new SolidColorBrush(Color.FromArgb(210, 12, 16, 25));
            sidebar.BorderBrush = Theme.StrokeSoft;
            sidebar.BorderThickness = new Thickness(0, 0, 1, 0);
            Grid.SetColumn(sidebar, 0);
            layout.Children.Add(sidebar);

            var sideGrid = new Grid();
            sideGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(64) });
            sideGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            sideGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(96) });
            sidebar.Child = sideGrid;

            var brand = BuildBrand();
            Grid.SetRow(brand, 0);
            sideGrid.Children.Add(brand);

            _navItems = new StackPanel();
            _navItems.Margin = new Thickness(14, 12, 14, 0);
            Grid.SetRow(_navItems, 1);
            sideGrid.Children.Add(_navItems);
            string[] names = new string[] { "概览", "Dock 整理", "音乐", "任务栏", "设置" };
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                var item = new Border();
                item.Height = 46;
                item.CornerRadius = new CornerRadius(12);
                item.Margin = new Thickness(0, 3, 0, 3);
                item.Cursor = Cursors.Hand;
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
                row.ColumnDefinitions.Add(new ColumnDefinition());
                Canvas icon = NavIcon(i);
                icon.HorizontalAlignment = HorizontalAlignment.Center;
                icon.VerticalAlignment = VerticalAlignment.Center;
                row.Children.Add(icon);
                var text = UiKit.Text(names[i], 13.5, Theme.MutedBrush, FontWeights.Medium);
                text.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(text, 1);
                row.Children.Add(text);
                item.Child = row;
                item.MouseEnter += delegate { if (_pageIndex != index) item.Background = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255)); };
                item.MouseLeave += delegate { if (_pageIndex != index) item.Background = Brushes.Transparent; };
                item.MouseLeftButtonUp += delegate { Navigate(index); };
                _navBorders[i] = item;
                _navTexts[i] = text;
                _navIcons[i] = icon;
                _navItems.Children.Add(item);
            }

            var bottom = new StackPanel();
            bottom.Margin = new Thickness(20, 0, 20, 12);
            bottom.VerticalAlignment = VerticalAlignment.Bottom;
            var line = new Border();
            line.Height = 1;
            line.Background = Theme.StrokeSoft;
            line.Margin = new Thickness(0, 0, 0, 10);
            bottom.Children.Add(line);
            var live = new StackPanel { Orientation = Orientation.Horizontal };
            var dot = new System.Windows.Shapes.Ellipse { Width = 6, Height = 6, Fill = Theme.SuccessBrush, Margin = new Thickness(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center };
            live.Children.Add(dot);
            live.Children.Add(UiKit.Text("仅在本机运行 · 离线", 10.5, Theme.DimBrush, null));
            bottom.Children.Add(live);
            var ver = UiKit.Text("桌序 DeskOrder  1.0", 10.5, Theme.DimBrush, null);
            ver.Margin = new Thickness(0, 7, 0, 0);
            bottom.Children.Add(ver);
            _status = UiKit.Text("Dock 已就绪", 10.5, Theme.DimBrush, null);
            _status.TextTrimming = TextTrimming.CharacterEllipsis;
            _status.Margin = new Thickness(0, 5, 0, 0);
            bottom.Children.Add(_status);
            Grid.SetRow(bottom, 2);
            sideGrid.Children.Add(bottom);

            // main area
            var main = new Grid();
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(54) });
            main.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(main, 1);
            layout.Children.Add(main);

            var top = new Grid();
            top.Background = Brushes.Transparent;
            var drag = new Border();
            drag.Background = Brushes.Transparent;
            drag.Cursor = Cursors.Arrow;
            drag.MouseLeftButtonDown += delegate (object s, MouseButtonEventArgs e)
            {
                if (e.ClickCount == 2) ToggleMaximize();
                else if (e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch { } }
            };
            top.Children.Add(drag);
            _crumb = UiKit.Text("概览", 11.5, Theme.DimBrush, FontWeights.Medium);
            _crumb.HorizontalAlignment = HorizontalAlignment.Left;
            _crumb.VerticalAlignment = VerticalAlignment.Center;
            _crumb.Margin = new Thickness(28, 0, 0, 0);
            _crumb.IsHitTestVisible = false;
            top.Children.Add(_crumb);

            var caption = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Top };
            caption.Margin = new Thickness(0, 8, 10, 0);
            var min = CaptionButton(false, delegate { WindowState = WindowState.Minimized; });
            var max = CaptionButton(true, delegate { ToggleMaximize(); });
            _maxGlyph = (TextBlock)((Grid)max.Child).Children[0];
            var close = CaptionButton(false, delegate { Close(); });
            close.MouseEnter += delegate { close.Background = Theme.DangerBrush; };
            close.MouseLeave += delegate { close.Background = Brushes.Transparent; };
            // Use a cross for close, minus/max glyph for others
            var cross = new Grid();
            cross.Children.Add(UiKit.PathIcon("M 5,5 L 14,14 M 14,5 L 5,14", null, Theme.TextBrush, 1.4));
            cross.IsHitTestVisible = false;
            close.Child = cross;
            caption.Children.Add(min);
            caption.Children.Add(max);
            caption.Children.Add(close);
            top.Children.Add(caption);
            Grid.SetRow(top, 0);
            main.Children.Add(top);

            _pageHost = new Grid();
            _pageHost.Margin = new Thickness(0, 0, 0, 0);
            Grid.SetRow(_pageHost, 1);
            main.Children.Add(_pageHost);

            _toastLayer = new Grid();
            _toastLayer.IsHitTestVisible = false;
            _toastLayer.VerticalAlignment = VerticalAlignment.Bottom;
            Grid.SetRowSpan(_toastLayer, 2);
            Grid.SetColumnSpan(_toastLayer, 2);
            _root.Children.Add(_toastLayer);

            SourceInitialized += OnSourceInitialized;
            StateChanged += delegate
            {
                UpdateMaxGlyph();
                if (WindowState == WindowState.Minimized) _app.ResumeDockAfterMain();
                else if (IsVisible && _app.DockWindow != null) _app.DockWindow.SuspendForMain(true);
            };
            LocationChanged += delegate { SaveBounds(); };
            SizeChanged += delegate { SaveBounds(); };
            Closing += OnClosing;
            IsVisibleChanged += delegate
            {
                if (!IsVisible && _app != null && !_app.Exiting) _app.ResumeDockAfterMain();
            };
            Navigate(0);
        }

        private FrameworkElement BuildBrand()
        {
            var grid = new Grid();
            grid.Margin = new Thickness(18, 0, 16, 0);
            grid.MouseLeftButtonDown += delegate (object s, MouseButtonEventArgs e)
            {
                if (e.ClickCount == 2) ToggleMaximize();
                else if (e.ButtonState == MouseButtonState.Pressed) { try { DragMove(); } catch { } }
            };
            var logo = new Border();
            logo.Width = 30;
            logo.Height = 30;
            logo.CornerRadius = new CornerRadius(10);
            logo.Background = Theme.AccentGradient;
            var bars = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            for (int i = 0; i < 3; i++)
            {
                var b = new Border { Width = 2.5, Height = 6 + i * 5, CornerRadius = new CornerRadius(1.5), Background = Theme.TextOnAccent, Margin = new Thickness(1.2, 0, 1.2, 0), VerticalAlignment = VerticalAlignment.Center };
                bars.Children.Add(b);
            }
            logo.Child = bars;
            logo.HorizontalAlignment = HorizontalAlignment.Left;
            grid.Children.Add(logo);
            var name = UiKit.Text("桌序", 17, Theme.TextBrush, FontWeights.SemiBold);
            name.Margin = new Thickness(44, 0, 0, 0);
            name.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(name);
            var tag = UiKit.Text("DESKORDER", 8.5, Theme.DimBrush, FontWeights.SemiBold);
            tag.Margin = new Thickness(44, 22, 0, 0);
            tag.VerticalAlignment = VerticalAlignment.Center;
            grid.Children.Add(tag);
            return grid;
        }

        private Border CaptionButton(bool maximize, RoutedEventHandler click)
        {
            var b = new Border();
            b.Width = 42;
            b.Height = 32;
            b.CornerRadius = new CornerRadius(8);
            b.Margin = new Thickness(2, 0, 2, 0);
            b.Cursor = Cursors.Hand;
            b.Background = Brushes.Transparent;
            b.MouseEnter += delegate { b.Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255)); };
            b.MouseLeave += delegate { b.Background = Brushes.Transparent; };
            b.MouseLeftButtonUp += delegate (object s, MouseButtonEventArgs e) { if (click != null) click(s, e); };
            var glyph = new TextBlock();
            glyph.FontFamily = Theme.IconFont;
            glyph.FontSize = 10;
            glyph.Foreground = Theme.TextBrush;
            glyph.Text = maximize ? "\uE922" : "\uE921";
            glyph.HorizontalAlignment = HorizontalAlignment.Center;
            glyph.VerticalAlignment = VerticalAlignment.Center;
            var g = new Grid();
            g.Children.Add(glyph);
            b.Child = g;
            return b;
        }

        private static Canvas NavIcon(int index)
        {
            var canvas = new Canvas();
            canvas.Width = 20;
            canvas.Height = 20;
            Brush brush = new SolidColorBrush(Color.FromArgb(170, 220, 228, 245));
            brush.Freeze();
            switch (index)
            {
                case 0:
                    for (int i = 0; i < 4; i++)
                    {
                        var r = new System.Windows.Shapes.Rectangle { Width = 6.5, Height = 6.5, RadiusX = 2, RadiusY = 2, Fill = brush };
                        Canvas.SetLeft(r, (i % 2) * 10.5 + 1);
                        Canvas.SetTop(r, (i / 2) * 10.5 + 1);
                        canvas.Children.Add(r);
                    }
                    break;
                case 1:
                    var pill = new Border { Width = 18, Height = 8, CornerRadius = new CornerRadius(4), BorderBrush = brush, BorderThickness = new Thickness(1.4), Background = Brushes.Transparent };
                    Canvas.SetLeft(pill, 1); Canvas.SetTop(pill, 6);
                    canvas.Children.Add(pill);
                    for (int i = 0; i < 3; i++)
                    {
                        var d = new System.Windows.Shapes.Ellipse { Width = 2.2, Height = 2.2, Fill = brush };
                        Canvas.SetLeft(d, 5 + i * 4.5); Canvas.SetTop(d, 9);
                        canvas.Children.Add(d);
                    }
                    break;
                case 2:
                    for (int i = 0; i < 3; i++)
                    {
                        var bar = new Border { Width = 3, Height = 7 + i * 4, CornerRadius = new CornerRadius(1.5), Background = brush };
                        Canvas.SetLeft(bar, 4 + i * 5.5);
                        Canvas.SetTop(bar, 16 - bar.Height);
                        canvas.Children.Add(bar);
                    }
                    break;
                case 3:
                    var tl = new Border { Width = 18, Height = 9, CornerRadius = new CornerRadius(4.5), BorderBrush = brush, BorderThickness = new Thickness(1.4) };
                    Canvas.SetLeft(tl, 1); Canvas.SetTop(tl, 6);
                    canvas.Children.Add(tl);
                    var dot = new System.Windows.Shapes.Ellipse { Width = 3, Height = 3, Fill = brush };
                    Canvas.SetLeft(dot, 4); Canvas.SetTop(dot, 9);
                    canvas.Children.Add(dot);
                    break;
                default:
                    for (int i = 0; i < 3; i++)
                    {
                        var line = new Border { Width = 18, Height = 1.6, CornerRadius = new CornerRadius(1), Background = brush };
                        Canvas.SetLeft(line, 1); Canvas.SetTop(line, 4 + i * 6);
                        canvas.Children.Add(line);
                        var knob = new System.Windows.Shapes.Ellipse { Width = 4.5, Height = 4.5, Fill = brush };
                        Canvas.SetLeft(knob, 3 + i * 5.5); Canvas.SetTop(knob, 2.5 + i * 6);
                        canvas.Children.Add(knob);
                    }
                    break;
            }
            return canvas;
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            try
            {
                IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                int corner = 2;
                Win32.DwmSetWindowAttribute(hwnd, 33, ref corner, 4);
                int dark = 1;
                Win32.DwmSetWindowAttribute(hwnd, 20, ref dark, 4);
            }
            catch { }
        }

        private void ToggleMaximize()
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        }

        private void UpdateMaxGlyph()
        {
            if (_maxGlyph == null) return;
            _maxGlyph.Text = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
        }

        public void Navigate(int index)
        {
            if (index < 0 || index > 4) index = 0;
            if (_currentPage != null)
            {
                _currentPage.Dispose();
                _currentPage = null;
            }
            PageView page;
            switch (index)
            {
                case 0: page = new OverviewPage(_app, this); break;
                case 1: page = new DockPage(_app, this); break;
                case 2: page = new MusicPage(_app, this); break;
                case 3: page = new TaskbarPage(_app, this); break;
                default: page = new SettingsPage(_app, this); break;
            }
            _pageIndex = index;
            _currentPage = page;
            _pageHost.Children.Clear();
            _pageHost.Children.Add(page);
            _crumb.Text = page.Title;
            for (int i = 0; i < _navBorders.Length; i++)
            {
                bool active = i == index;
                _navBorders[i].Background = active ? Theme.Solid(Color.FromArgb(30, 124, 155, 255)) : Brushes.Transparent;
                _navTexts[i].Foreground = active ? Theme.TextBrush : Theme.MutedBrush;
                _navIcons[i].Opacity = active ? 1.0 : 0.7;
            }
            if (!_app.Settings.General.ReduceMotion)
            {
                page.Opacity = 0;
                var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180));
                page.BeginAnimation(OpacityProperty, anim);
            }
            UpdateStatus();
        }

        public PageView CurrentPage { get { return _currentPage; } }

        public void RefreshActive()
        {
            if (_currentPage != null) _currentPage.Refresh();
            UpdateStatus();
        }

        private void UpdateStatus()
        {
            try
            {
                int pins = _app.Dock != null ? _app.Dock.Count : 0;
                string taskbar = _app.Settings.Taskbar.Enabled ? "任务栏 " + ModeName() : "任务栏默认";
                _status.Text = "Dock " + pins + " 项 · " + taskbar;
            }
            catch { }
        }

        private string ModeName()
        {
            if (_app.Settings.Taskbar.Mode == "blur") return "磨砂";
            if (_app.Settings.Taskbar.Mode == "acrylic") return "亚克力";
            return "通透";
        }

        public void ShowToast(string message, bool error)
        {
            try
            {
                var b = new Border();
                b.CornerRadius = new CornerRadius(12);
                b.Background = new SolidColorBrush(Color.FromArgb(246, 26, 34, 49));
                b.BorderBrush = new SolidColorBrush(Color.FromArgb(48, 255, 255, 255));
                b.BorderThickness = new Thickness(1);
                b.Padding = new Thickness(16, 11, 16, 11);
                b.Margin = new Thickness(0, 0, 0, 26);
                b.HorizontalAlignment = HorizontalAlignment.Center;
                b.VerticalAlignment = VerticalAlignment.Bottom;
                b.Effect = new DropShadowEffect { BlurRadius = 26, ShadowDepth = 0, Opacity = 0.45, Color = Colors.Black };
                var g = new StackPanel { Orientation = Orientation.Horizontal };
                var dot = new System.Windows.Shapes.Ellipse { Width = 7, Height = 7, Fill = error ? Theme.DangerBrush : Theme.SuccessBrush, Margin = new Thickness(0, 0, 9, 0), VerticalAlignment = VerticalAlignment.Center };
                g.Children.Add(dot);
                g.Children.Add(UiKit.Text(message, 12, Theme.TextBrush, FontWeights.Medium));
                b.Child = g;
                _toastLayer.Children.Add(b);
                b.Opacity = 0;
                var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160));
                b.BeginAnimation(OpacityProperty, anim);
                var timer = new DispatcherTimer();
                timer.Interval = TimeSpan.FromSeconds(3.2);
                timer.Tick += delegate
                {
                    timer.Stop();
                    var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
                    fade.Completed += delegate { _toastLayer.Children.Remove(b); };
                    b.BeginAnimation(OpacityProperty, fade);
                };
                timer.Start();
            }
            catch { }
        }

        private void SaveBounds()
        {
            if (_app.Settings == null) return;
            _app.Settings.Window.Maximized = WindowState == WindowState.Maximized;
            if (WindowState == WindowState.Normal)
            {
                _app.Settings.Window.Left = Left;
                _app.Settings.Window.Top = Top;
                _app.Settings.Window.Width = Width;
                _app.Settings.Window.Height = Height;
            }
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            SaveBounds();
            SettingsStore.Save(_app.Settings);
            if (!_app.Exiting && _app.Settings.General.CloseToTray)
            {
                e.Cancel = true;
                Hide();
                if (_app.Tray != null) _app.Tray.Balloon("桌序仍在托盘运行，Dock 继续为你服务", false);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
        }
    }
}
