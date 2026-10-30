using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using DeskOrder.App;
using DeskOrder.Core;
using DeskOrder.Interop;
using DeskOrder.UI;

namespace DeskOrder.Windows
{
    public class DockWindow : Window
    {
        private readonly DeskOrderApp _app;
        private readonly Grid _root;
        private readonly Border _glow;
        private readonly Border _shell;
        private readonly Border _shadow;
        private readonly ScrollViewer _scroll;
        private readonly StackPanel _items;
        private readonly DispatcherTimer _hideTimer;
        private readonly DispatcherTimer _runningTimer;
        private readonly DispatcherTimer _beatTimer;
        private readonly DispatcherTimer _layoutTimer;
        private readonly List<DockEntry> _entries = new List<DockEntry>();
        private DockEntry _musicEntry;
        private double _scale = 1.0;
        private Rect _workArea;
        private bool _autoHidden;
        private bool _userHidden;
        private bool _suppressAuto;
        private DateTime _leaveAt = DateTime.MinValue;
        private int _hoverIndex = -1;
        private bool _menuOpen;
        private bool _autoHideArmed;
        private double _iconSize;
        private double _slot;
        private double _dockHeight;
        private double _lastWorkWidth;
        private double _lastWorkHeight;
        private readonly Dictionary<Grid, DockEntry> _entryByRoot = new Dictionary<Grid, DockEntry>();

        public DockWindow(DeskOrderApp app)
        {
            _app = app;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ShowInTaskbar = false;
            ShowActivated = false;
            ResizeMode = ResizeMode.NoResize;
            Topmost = app.Settings.Dock.AlwaysOnTop;
            SizeToContent = SizeToContent.Manual;
            SnapsToDevicePixels = true;
            UseLayoutRounding = true;

            _root = new Grid();
            _root.Background = null;
            Content = _root;

            _glow = new Border();
            _glow.CornerRadius = new CornerRadius(26);
            _glow.Opacity = 0;
            _glow.IsHitTestVisible = false;
            _glow.Visibility = Visibility.Collapsed;
            _root.Children.Add(_glow);

            _shadow = new Border();
            _shadow.CornerRadius = new CornerRadius(26);
            _shadow.Background = new SolidColorBrush(Color.FromArgb(100, 0, 0, 0));
            _shadow.Effect = new DropShadowEffect { BlurRadius = 30, ShadowDepth = 0, Opacity = 0.42, Color = Colors.Black };
            _shadow.IsHitTestVisible = false;
            _shadow.Visibility = Visibility.Collapsed;
            _root.Children.Add(_shadow);

            _shell = new Border();
            _shell.CornerRadius = new CornerRadius(22);
            _shell.BorderThickness = new Thickness(1);
            _shell.SnapsToDevicePixels = true;
            _shell.Margin = new Thickness(0);
            _root.Children.Add(_shell);

            _scroll = new ScrollViewer();
            _scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Hidden;
            _scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Disabled;
            _scroll.CanContentScroll = false;
            _scroll.Focusable = false;
            _scroll.PreviewMouseWheel += delegate (object s, MouseWheelEventArgs e)
            {
                _scroll.ScrollToHorizontalOffset(Math.Max(0, _scroll.HorizontalOffset - e.Delta));
                e.Handled = true;
            };
            _items = new StackPanel();
            _items.Orientation = Orientation.Horizontal;
            _items.VerticalAlignment = VerticalAlignment.Center;
            _items.Margin = new Thickness(10, 0, 10, 0);
            _scroll.Content = _items;
            _shell.Child = _scroll;
            _shell.MouseRightButtonUp += delegate (object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                ShowDockMenu();
            };

            AllowDrop = true;
            DragEnter += OnDragEnter;
            DragOver += OnDragEnter;
            Drop += OnDrop;
            PreviewMouseLeftButtonDown += delegate
            {
                _leaveAt = DateTime.MinValue;
                if (!IsActive)
                {
                    try { Activate(); } catch { }
                }
            };
            PreviewMouseLeftButtonUp += OnPreviewClick;
            MouseEnter += delegate { _leaveAt = DateTime.MinValue; };
            MouseLeave += delegate { _leaveAt = DateTime.UtcNow; };
            Loaded += OnLoaded;
            SourceInitialized += OnSourceInitialized;
            Closing += delegate (object s, System.ComponentModel.CancelEventArgs e) { if (!_app.Settings.Dock.AutoHide) { } };

            _hideTimer = new DispatcherTimer(DispatcherPriority.Background);
            _hideTimer.Interval = TimeSpan.FromMilliseconds(100);
            _hideTimer.Tick += OnHideTick;
            _hideTimer.Start();

            _runningTimer = new DispatcherTimer(DispatcherPriority.Background);
            _runningTimer.Interval = TimeSpan.FromSeconds(2);
            _runningTimer.Tick += delegate { UpdateRunning(); };
            _runningTimer.Start();

            _beatTimer = new DispatcherTimer(DispatcherPriority.Render);
            _beatTimer.Interval = TimeSpan.FromMilliseconds(33);
            _beatTimer.Tick += OnBeat;
            _beatTimer.Start();

            _layoutTimer = new DispatcherTimer(DispatcherPriority.Background);
            _layoutTimer.Interval = TimeSpan.FromSeconds(3);
            _layoutTimer.Tick += delegate { CheckDisplayChange(); };
            _layoutTimer.Start();

            if (_app.Player != null)
            {
                _app.Player.TrackChanged += delegate { UpdateMusicEntry(); };
                _app.Player.StateChanged += delegate { UpdateMusicEntry(); };
            }

            RefreshPins();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _scale = GetScale();
            ApplyChrome();
            LayoutDock();
            _autoHideArmed = false;
            if (_app.Settings.Dock.AutoHide)
            {
                _autoHidden = true;
                PlaceDockPhysical(false);
            }
            else
            {
                _autoHidden = false;
                PlaceDockPhysical(true);
            }
            StartEntrance();
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            try
            {
                IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                Win32.EnableWindowChrome(hwnd, true, false);
                int corner = 2;
                Win32.DwmSetWindowAttribute(hwnd, 33, ref corner, 4);
                int ncPolicy = 1;
                Win32.DwmSetWindowAttribute(hwnd, 2, ref ncPolicy, 4);
                int noBorder = unchecked((int)0xFFFFFFFE);
                Win32.DwmSetWindowAttribute(hwnd, 34, ref noBorder, 4);
            }
            catch { }
        }

        private void ApplyChrome()
        {
            try
            {
                IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                Win32.EnableWindowChrome(hwnd, true, false);
            }
            catch { }
        }

        private void PlaceDockPhysical(bool shown)
        {
            try
            {
                if (!IsVisible) return;
                IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;
                UpdateLayout();
                Win32.RECT wr;
                if (!Win32.GetWindowRect(hwnd, out wr)) return;
                int w = wr.Width;
                int h = wr.Height;
                if (w < 50 || h < 30) return;
                IntPtr mon = Win32.MonitorFromWindow(hwnd, Win32.MONITOR_DEFAULTTONEAREST);
                var info = new Win32.MONITORINFO();
                info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Win32.MONITORINFO));
                Win32.GetMonitorInfo(mon, ref info);
                int gap = 10;
                int x = info.rcWork.Left + (info.rcWork.Width - w) / 2;
                int y = shown ? (info.rcWork.Bottom - h - gap) : (info.rcMonitor.Bottom + 24);
                Win32.SetWindowPos(hwnd, Win32.HWND_TOPMOST, x, y, w, h, Win32.SWP_NOACTIVATE | Win32.SWP_SHOWWINDOW);
                if (shown) ReassertTopmost();
                Log.Info("Dock placed shown=" + shown + " rect=" + x + "," + y + "," + w + "x" + h);
            }
            catch (Exception ex) { Log.Warn("PlaceDockPhysical: " + ex.Message); }
        }

        private void ReassertTopmost()
        {
            try
            {
                if (!IsVisible) return;
                IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                if (hwnd == IntPtr.Zero) return;
                Win32.SetWindowPos(hwnd, Win32.HWND_TOPMOST, 0, 0, 0, 0,
                    Win32.SWP_NOMOVE | Win32.SWP_NOSIZE | Win32.SWP_NOACTIVATE);
            }
            catch { }
        }

        private double GetScale()
        {
            try
            {
                PresentationSource src = PresentationSource.FromVisual(this);
                if (src != null && src.CompositionTarget != null) return src.CompositionTarget.TransformToDevice.M11;
            }
            catch { }
            return 1.0;
        }

        private void StartEntrance()
        {
            if (_app.Settings.General.ReduceMotion) return;
            Opacity = 0;
            var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(220));
            anim.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            BeginAnimation(OpacityProperty, anim);
        }

        private Rect GetWorkArea()
        {
            return SystemParameters.WorkArea;
        }

        public void RefreshPins()
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(RefreshPins)); return; }
            _items.Children.Clear();
            _entries.Clear();
            _entryByRoot.Clear();
            _hoverIndex = -1;

            List<DockPin> pins = _app.Dock.Pins;
            _iconSize = _app.Settings.Dock.IconSize;
            _slot = _iconSize + _app.Settings.Dock.Spacing;
            _dockHeight = _iconSize + 40;

            for (int i = 0; i < pins.Count; i++)
            {
                DockPin pin = pins[i];
                DockEntry entry = CreateEntry(pin, false, null, i);
                _entries.Add(entry);
                _items.Children.Add(entry.Root);
            }

            if (_app.Settings.Dock.MusicButton)
            {
                DockEntry music = CreateEntry(null, true, "music", _entries.Count);
                _musicEntry = music;
                _entries.Add(music);
                _items.Children.Add(music.Root);
                if (_entries.Count > 1)
                {
                    Border sep = CreateSeparator();
                    _items.Children.Insert(_items.Children.Count - 1, sep);
                }
            }

            DockEntry settings = CreateEntry(null, true, "settings", _entries.Count);
            _entries.Add(settings);
            _items.Children.Add(settings.Root);

            UpdateMusicEntry();
            UpdateRunning();
            LayoutDock();
            if (IsLoaded && IsVisible) PlaceDockPhysical(!_autoHidden);
        }

        private Border CreateSeparator()
        {
            var b = new Border();
            b.Width = 1;
            b.Height = 28;
            b.Background = new SolidColorBrush(Color.FromArgb(36, 255, 255, 255));
            b.Margin = new Thickness(8, 0, 8, 0);
            b.VerticalAlignment = VerticalAlignment.Center;
            return b;
        }

        private DockEntry CreateEntry(DockPin pin, bool special, string specialKind, int index)
        {
            var root = new Grid();
            root.Width = _slot;
            root.Height = _iconSize + 22;
            root.Background = Brushes.Transparent;
            root.VerticalAlignment = VerticalAlignment.Center;
            root.Tag = pin != null ? (object)pin.Id : specialKind;

            var iconGrid = new Grid();
            iconGrid.Width = _iconSize;
            iconGrid.Height = _iconSize;
            iconGrid.HorizontalAlignment = HorizontalAlignment.Center;
            iconGrid.VerticalAlignment = VerticalAlignment.Bottom;
            iconGrid.Margin = new Thickness(0, 0, 0, 10);
            iconGrid.RenderTransformOrigin = new Point(0.5, 1.0);
            var scale = new ScaleTransform(1, 1);
            var translate = new TranslateTransform(0, 0);
            var group = new TransformGroup();
            group.Children.Add(scale);
            group.Children.Add(translate);
            iconGrid.RenderTransform = group;

            var iconHost = new Border();
            iconHost.CornerRadius = new CornerRadius(12);
            iconHost.Background = Brushes.Transparent;
            iconGrid.Children.Add(iconHost);

            Image img = new Image();
            img.Stretch = Stretch.Uniform;
            img.Width = _iconSize - 4;
            img.Height = _iconSize - 4;
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            iconHost.Child = img;

            TextBlock fallback = new TextBlock();
            fallback.FontFamily = Theme.UiFont;
            fallback.FontSize = Math.Max(16, _iconSize * 0.38);
            fallback.FontWeight = FontWeights.SemiBold;
            fallback.Foreground = Brushes.White;
            fallback.HorizontalAlignment = HorizontalAlignment.Center;
            fallback.VerticalAlignment = VerticalAlignment.Center;
            fallback.Visibility = Visibility.Collapsed;
            iconGrid.Children.Add(fallback);

            var dot = new Ellipse();
            dot.Width = 4;
            dot.Height = 4;
            dot.Fill = Theme.Accent2Brush;
            dot.HorizontalAlignment = HorizontalAlignment.Center;
            dot.VerticalAlignment = VerticalAlignment.Bottom;
            dot.Margin = new Thickness(0, 0, 0, 3);
            dot.Visibility = Visibility.Collapsed;
            root.Children.Add(iconGrid);
            root.Children.Add(dot);

            var entry = new DockEntry();
            entry.Root = root;
            entry.IconHost = iconHost;
            entry.Image = img;
            entry.Fallback = fallback;
            entry.Dot = dot;
            entry.Scale = scale;
            entry.Translate = translate;
            entry.Pin = pin;
            entry.SpecialKind = specialKind;
            entry.Index = index;
            entry.Root.MouseEnter += delegate { SetHover(index); };
            entry.Root.MouseLeave += delegate { SetHover(-1); };
            entry.Root.MouseRightButtonUp += delegate (object s, MouseButtonEventArgs e)
            {
                e.Handled = true;
                ShowEntryMenu(entry, s as FrameworkElement);
            };
            entry.Root.PreviewMouseLeftButtonDown += delegate (object s, MouseButtonEventArgs e)
            {
                if (e.ClickCount > 1) return;
                _didDrag = false;
                _dragStart = e.GetPosition(this);
                _dragEntry = entry;
            };
            entry.Root.PreviewMouseMove += delegate (object s, MouseEventArgs e)
            {
                if (e.LeftButton != MouseButtonState.Pressed || _dragEntry != entry || entry.Pin == null) return;
                Point p = e.GetPosition(this);
                if (Math.Abs(p.X - _dragStart.X) + Math.Abs(p.Y - _dragStart.Y) < 6) return;
                var data = new DataObject(PinFormat, entry.Pin.Id);
                _dragEntry = null;
                _didDrag = true;
                DragDrop.DoDragDrop(entry.Root, data, DragDropEffects.Move);
            };
            entry.Root.AllowDrop = true;
            entry.Root.Cursor = Cursors.Hand;
            entry.Root.ToolTip = BuildTooltip(pin, specialKind);
            _entryByRoot[entry.Root] = entry;
            ToolTipService.SetInitialShowDelay(entry.Root, 250);

            if (special && specialKind == "music")
            {
                entry.Root.ToolTip = "音乐 · 点击打开音乐卡片";
                entry.MusicRing = CreateMusicRing(iconGrid);
                entry.MusicBars = CreateMusicBars(iconGrid);
                iconHost.Background = new SolidColorBrush(Color.FromArgb(30, 255, 255, 255));
                fallback.Text = "♪";
                fallback.FontFamily = new FontFamily("Segoe UI Symbol");
                fallback.FontSize = _iconSize * 0.50;
                fallback.Visibility = Visibility.Visible;
                fallback.Foreground = Theme.TextBrush;
            }
            else if (special && specialKind == "settings")
            {
                entry.Root.ToolTip = "桌序设置";
                iconHost.Background = Theme.AccentGradient;
                fallback.Text = "序";
                fallback.FontSize = _iconSize * 0.40;
                fallback.Visibility = Visibility.Visible;
                fallback.Foreground = Theme.TextOnAccent;
            }
            else if (pin != null)
            {
                BitmapSource source = null;
                if (!string.IsNullOrEmpty(pin.CustomIcon)) source = IconService.LoadImageFile(pin.CustomIcon);
                if (source == null && File.Exists(pin.Path)) source = IconService.GetIcon(pin.Path, 96);
                if (source != null) img.Source = source;
                else
                {
                    fallback.Text = string.IsNullOrEmpty(pin.DisplayName) ? "?" : pin.DisplayName.Substring(0, 1);
                    fallback.Visibility = Visibility.Visible;
                    fallback.Foreground = Theme.TextBrush;
                }
            }
            return entry;
        }

        private Ellipse CreateMusicRing(Grid iconGrid)
        {
            var ring = new Ellipse();
            ring.Width = _iconSize + 10;
            ring.Height = _iconSize + 10;
            ring.Stroke = Theme.Accent2Brush;
            ring.StrokeThickness = 1.4;
            ring.Opacity = 0;
            ring.HorizontalAlignment = HorizontalAlignment.Center;
            ring.VerticalAlignment = VerticalAlignment.Center;
            iconGrid.Children.Insert(0, ring);
            return ring;
        }

        private StackPanel CreateMusicBars(Grid iconGrid)
        {
            var bars = new StackPanel();
            bars.Orientation = Orientation.Horizontal;
            bars.HorizontalAlignment = HorizontalAlignment.Center;
            bars.VerticalAlignment = VerticalAlignment.Bottom;
            bars.Margin = new Thickness(0, 0, 0, 5);
            for (int i = 0; i < 4; i++)
            {
                var b = new Border();
                b.Width = 2.5;
                b.Height = 5 + i * 2;
                b.Margin = new Thickness(1.2, 0, 1.2, 0);
                b.CornerRadius = new CornerRadius(1.2);
                b.Background = Theme.Accent2Brush;
                b.VerticalAlignment = VerticalAlignment.Bottom;
                bars.Children.Add(b);
            }
            iconGrid.Children.Add(bars);
            return bars;
        }

        private object BuildTooltip(DockPin pin, string specialKind)
        {
            if (pin == null) return specialKind == "music" ? "音乐" : "设置";
            var tip = new ToolTip();
            tip.Background = Theme.Surface2Brush;
            tip.Foreground = Theme.TextBrush;
            tip.BorderBrush = Theme.StrokeBrush;
            tip.BorderThickness = new Thickness(1);
            tip.Padding = new Thickness(9, 5, 9, 5);
            tip.Content = UiKit.Text(pin.DisplayName, 11.5, Theme.TextBrush, FontWeights.Medium);
            return tip;
        }

        private Point _dragStart;
        private DockEntry _dragEntry;
        private bool _didDrag;

        private void SetHover(int index)
        {
            if (_hoverIndex == index) return;
            _hoverIndex = index;
            foreach (DockEntry e in _entries)
            {
                int d = index < 0 ? 99 : Math.Abs(e.Index - index);
                double scale = d == 0 ? 1.32 : (d == 1 ? 1.15 : (d == 2 ? 1.06 : 1.0));
                double lift = d == 0 ? 7 : (d == 1 ? 3 : 0);
                AnimateScale(e, scale, lift);
            }
        }

        private void AnimateScale(DockEntry e, double scale, double lift)
        {
            if (_app.Settings.General.ReduceMotion)
            {
                e.Scale.ScaleX = scale; e.Scale.ScaleY = scale; e.Translate.Y = -lift;
                return;
            }
            var s = new DoubleAnimation(scale, TimeSpan.FromMilliseconds(130));
            s.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            e.Scale.BeginAnimation(ScaleTransform.ScaleXProperty, s);
            e.Scale.BeginAnimation(ScaleTransform.ScaleYProperty, s);
            var t = new DoubleAnimation(-lift, TimeSpan.FromMilliseconds(130));
            t.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
            e.Translate.BeginAnimation(TranslateTransform.YProperty, t);
        }

        private void OnPreviewClick(object sender, MouseButtonEventArgs e)
        {
            if (_didDrag)
            {
                _didDrag = false;
                _dragEntry = null;
                return;
            }
            _dragEntry = null;
            DependencyObject d = e.OriginalSource as DependencyObject;
            DockEntry found = null;
            while (d != null)
            {
                Grid g = d as Grid;
                if (g != null)
                {
                    DockEntry candidate;
                    if (_entryByRoot.TryGetValue(g, out candidate)) { found = candidate; break; }
                }
                d = System.Windows.Media.VisualTreeHelper.GetParent(d);
            }
            if (found == null) return;
            e.Handled = true;
            OnEntryClick(found);
        }

        private void OnEntryClick(DockEntry entry)
        {
            try { Log.Info("Dock click " + (entry.Pin != null ? "pin=" + entry.Pin.DisplayName : "special=" + entry.SpecialKind)); } catch { }
            if (entry.Pin != null)
            {
                _app.Dock.Launch(entry.Pin);
                Bounce(entry);
                return;
            }
            if (entry.SpecialKind == "music") _app.ToggleMusicWidget();
            else if (entry.SpecialKind == "settings") _app.ShowMain(0);
        }

        private void Bounce(DockEntry entry)
        {
            if (_app.Settings.General.ReduceMotion) return;
            var anim = new DoubleAnimationUsingKeyFrames();
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(1.38, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(100))));
            anim.KeyFrames.Add(new EasingDoubleKeyFrame(1.32, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(190))));
            entry.Scale.BeginAnimation(ScaleTransform.ScaleXProperty, anim);
            entry.Scale.BeginAnimation(ScaleTransform.ScaleYProperty, anim);
        }

        private void ShowEntryMenu(DockEntry entry, FrameworkElement target)
        {
            var menu = NewMenu();
            if (entry.Pin != null)
            {
                var open = new MenuItem { Header = "打开" };
                open.Click += delegate { _app.Dock.Launch(entry.Pin); };
                menu.Items.Add(open);
                var location = new MenuItem { Header = "在文件夹中显示" };
                location.Click += delegate { ShowInExplorer(entry.Pin.Path); };
                menu.Items.Add(location);
                var icon = new MenuItem { Header = "更换图标…" };
                icon.Click += delegate { ChangeIcon(entry.Pin); };
                menu.Items.Add(icon);
                menu.Items.Add(new Separator());
                var left = new MenuItem { Header = "左移" };
                left.Click += delegate { MovePin(entry.Pin, -1); };
                menu.Items.Add(left);
                var right = new MenuItem { Header = "右移" };
                right.Click += delegate { MovePin(entry.Pin, 1); };
                menu.Items.Add(right);
                menu.Items.Add(new Separator());
                var remove = new MenuItem { Header = "从 Dock 移除" };
                remove.Click += delegate { RemovePin(entry.Pin); };
                menu.Items.Add(remove);
            }
            else
            {
                var music = new MenuItem { Header = _app.Player.IsPlaying ? "暂停" : "播放" };
                music.Click += delegate { _app.Player.Toggle(); };
                menu.Items.Add(music);
                if (entry.SpecialKind == "music")
                {
                    var show = new MenuItem { Header = "打开音乐卡片" };
                    show.Click += delegate { _app.ShowMusicWidget(true); };
                    menu.Items.Add(show);
                }
                else
                {
                    var settings = new MenuItem { Header = "打开设置" };
                    settings.Click += delegate { _app.ShowMain(4); };
                    menu.Items.Add(settings);
                }
            }
            menu.Items.Add(new Separator());
            AppendDockMenuItems(menu);
            menu.PlacementTarget = target ?? this;
            menu.IsOpen = true;
        }

        /// <summary>Right click on empty dock space: the place to add and tidy pins.</summary>
        private void ShowDockMenu()
        {
            ContextMenu menu = BuildDockMenu();
            menu.PlacementTarget = _shell;
            menu.IsOpen = true;
        }

        /// <summary>Builds the empty-space menu. Public so the UI test can inspect it.</summary>
        public ContextMenu BuildDockMenu()
        {
            var menu = NewMenu();
            var addFile = new MenuItem { Header = "添加文件…" };
            addFile.Click += delegate { AddFromPicker(true); };
            menu.Items.Add(addFile);
            var addFolder = new MenuItem { Header = "添加文件夹…" };
            addFolder.Click += delegate { AddFromPicker(false); };
            menu.Items.Add(addFolder);
            var sort = new MenuItem { Header = "按名称整理" };
            sort.Click += delegate
            {
                _app.Dock.SortByName();
                _app.Notify("已按名称重新整理 Dock", false);
            };
            menu.Items.Add(sort);
            var openPage = new MenuItem { Header = "打开 Dock 整理" };
            openPage.Click += delegate { _app.ShowMain(1); };
            menu.Items.Add(openPage);
            menu.Items.Add(new Separator());
            AppendDockMenuItems(menu);
            return menu;
        }

        private ContextMenu NewMenu()
        {
            var menu = new ContextMenu();
            menu.Background = Theme.Surface2Brush;
            menu.Foreground = Theme.TextBrush;
            menu.BorderBrush = Theme.StrokeBrush;
            menu.BorderThickness = new Thickness(1);
            menu.Padding = new Thickness(2);
            menu.Opened += delegate { _menuOpen = true; };
            menu.Closed += delegate { _menuOpen = false; };
            return menu;
        }

        private void AppendDockMenuItems(ContextMenu menu)
        {
            var pure = new MenuItem { Header = _app.Settings.General.HideDesktopIcons ? "显示桌面图标" : "隐藏桌面图标", IsCheckable = true, IsChecked = _app.Settings.General.HideDesktopIcons };
            pure.Click += delegate { _app.SetPureDesktop(!_app.Settings.General.HideDesktopIcons); };
            menu.Items.Add(pure);
            var taskbar = new MenuItem { Header = "任务栏透明", IsCheckable = true, IsChecked = _app.Settings.Taskbar.Enabled };
            taskbar.Click += delegate { _app.SetTaskbarEnabled(!_app.Settings.Taskbar.Enabled); };
            menu.Items.Add(taskbar);
            menu.Items.Add(new Separator());
            var exit = new MenuItem { Header = "退出桌序" };
            exit.Click += delegate { _app.Exit(); };
            menu.Items.Add(exit);
        }

        private void AddFromPicker(bool files)
        {
            try
            {
                string[] paths = files ? PathPicker.PickFiles("添加到 Dock", this) : Wrap(PathPicker.PickFolder("选择要固定到 Dock 的文件夹", this));
                if (paths == null || paths.Length == 0) return;
                int before = _app.Dock.Count;
                foreach (string p in paths) _app.Dock.Add(p, false);
                int added = _app.Dock.Count - before;
                string save = _app.Dock.LastSaveError;
                if (!string.IsNullOrEmpty(save)) _app.NotifyOnce("dock-save-menu", "已加入 Dock，但配置无法保存：" + save, true);
                else _app.Notify(added > 0 ? "已加入 Dock：" + added + " 项" : "这些项目已经在 Dock 里了", false);
            }
            catch (Exception ex) { _app.Notify("添加失败：" + ex.Message, true); }
        }

        private static string[] Wrap(string single)
        {
            if (string.IsNullOrEmpty(single)) return new string[0];
            return new string[] { single };
        }

        private void MovePin(DockPin pin, int delta)
        {
            try
            {
                int before = IndexOf(pin.Id);
                _app.Dock.Move(pin.Id, delta);
                if (IndexOf(pin.Id) == before) _app.Notify(delta < 0 ? "已经在最前面了" : "已经在最后面了", false);
            }
            catch (Exception ex) { _app.Notify("移动失败：" + ex.Message, true); }
        }

        private int IndexOf(string id)
        {
            List<DockPin> pins = _app.Dock.Pins;
            for (int i = 0; i < pins.Count; i++) if (pins[i].Id == id) return i;
            return -1;
        }

        private void RemovePin(DockPin pin)
        {
            if (pin.Managed)
            {
                var result = System.Windows.MessageBox.Show(this, "要把这个快捷方式移回桌面吗？\n\n是：移回桌面\n否：仅从 Dock 移除（文件留在 Dock 管理目录）\n取消：保留在 Dock",
                    "从 Dock 移除", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.Cancel) return;
                _app.Dock.Remove(pin.Id, result == MessageBoxResult.Yes);
            }
            else
            {
                _app.Dock.Remove(pin.Id, false);
            }
            string save = _app.Dock.LastSaveError;
            if (!string.IsNullOrEmpty(save)) _app.NotifyOnce("dock-save-menu", "已从 Dock 移除，但配置无法保存：" + save, true);
            else _app.Notify("已从 Dock 移除 " + pin.DisplayName, false);
        }

        private void ChangeIcon(DockPin pin)
        {
            var dialog = new System.Windows.Forms.OpenFileDialog();
            dialog.Title = "选择图标图片";
            dialog.Filter = "图片|*.png;*.jpg;*.jpeg;*.bmp;*.ico;*.gif|所有文件|*.*";
            if (dialog.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            try
            {
                string ext = System.IO.Path.GetExtension(dialog.FileName);
                string target = System.IO.Path.Combine(AppPaths.IconsDir, pin.Id + ext);
                File.Copy(dialog.FileName, target, true);
                _app.Dock.SetCustomIcon(pin.Id, target);
            }
            catch (Exception ex) { _app.Notify("更换图标失败：" + ex.Message, true); }
        }

        private static void ShowInExplorer(string path)
        {
            try
            {
                if (Directory.Exists(path)) Process.Start("explorer.exe", "\"" + path + "\"");
                else if (File.Exists(path)) Process.Start("explorer.exe", "/select,\"" + path + "\"");
            }
            catch { }
        }

        private void LayoutDock()
        {
            if (!IsLoaded && ActualWidth <= 0) { }
            Rect work = GetWorkArea();
            _workArea = work;
            _lastWorkWidth = work.Width;
            _lastWorkHeight = work.Height;
            _iconSize = _app.Settings.Dock.IconSize;
            _slot = _iconSize + _app.Settings.Dock.Spacing;
            _dockHeight = _iconSize + 40;

            double desiredWidth = 0;
            int count = _entries.Count;
            desiredWidth = count * _slot + 28;
            if (_app.Settings.Dock.MusicButton) desiredWidth += 1 + 16;

            double maxWidth = Math.Max(280, work.Width - 36);
            if (desiredWidth > maxWidth)
            {
                double factor = (maxWidth - 30) / Math.Max(1, desiredWidth - 30);
                _iconSize = Math.Max(36, Math.Floor(_app.Settings.Dock.IconSize * factor));
                _slot = _iconSize + Math.Max(6, _app.Settings.Dock.Spacing - 2);
                _dockHeight = _iconSize + 40;
                desiredWidth = count * _slot + 28;
                if (_app.Settings.Dock.MusicButton) desiredWidth += 17;
                foreach (DockEntry e in _entries)
                {
                    e.Root.Width = _slot;
                    e.Root.Height = _iconSize + 22;
                    e.Image.Width = _iconSize - 4;
                    e.Image.Height = _iconSize - 4;
                }
            }

            double contentWidth = Math.Min(desiredWidth, maxWidth);
            Width = contentWidth;
            Height = _dockHeight;
            _scroll.Width = contentWidth;
            _scroll.Height = _dockHeight - 10;
            _shell.Height = _dockHeight;
            _shell.Width = contentWidth;
            _glow.Width = _shell.Width + 20;
            _glow.Height = _shell.Height + 20;
            _shadow.Width = _shell.Width + 8;
            _shadow.Height = _shell.Height + 8;
            _glow.Margin = new Thickness(4, 12, 4, 0);
            _shadow.Margin = new Thickness(10, 18, 10, 6);

            double alpha = (int)Math.Round(Math.Max(40, Math.Min(100, _app.Settings.Dock.Opacity)) / 100.0 * 235.0);
            var bg = new LinearGradientBrush(Color.FromArgb((byte)alpha, 24, 31, 46), Color.FromArgb((byte)alpha, 9, 13, 21), new Point(0, 0), new Point(0, 1));
            _shell.Background = bg;
            _shell.BorderBrush = new SolidColorBrush(Color.FromArgb(18, 255, 255, 255));
            _shell.BorderThickness = new Thickness(1);

            var glowBrush = new RadialGradientBrush();
            glowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(90, 87, 224, 200), 0));
            glowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(40, 124, 155, 255), 0.55));
            glowBrush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 124, 155, 255), 1));
            _glow.Background = glowBrush;
            _glow.CornerRadius = new CornerRadius(28);

            if (Topmost != _app.Settings.Dock.AlwaysOnTop) Topmost = _app.Settings.Dock.AlwaysOnTop;
            UpdateMusicEntry();
        }

        private void CheckDisplayChange()
        {
            Rect work = GetWorkArea();
            if (Math.Abs(work.Width - _lastWorkWidth) > 1 || Math.Abs(work.Height - _lastWorkHeight) > 1)
            {
                _scale = GetScale();
                LayoutDock();
                if (IsVisible) PlaceDockPhysical(!_autoHidden);
            }
        }

        private void OnHideTick(object sender, EventArgs e)
        {
            if (_menuOpen) return;
            if (_app.Settings.Dock.HideOnFullscreen && IsFullscreenAppActive())
            {
                if (!_suppressAuto)
                {
                    _suppressAuto = true;
                    if (IsVisible) PlaceDockPhysical(false);
                }
                return;
            }
            if (_suppressAuto)
            {
                _suppressAuto = false;
                if (IsVisible)
                {
                    _autoHidden = false;
                    _autoHideArmed = false;
                    PlaceDockPhysical(true);
                }
            }
            if (!_app.Settings.Dock.AutoHide || _userHidden) return;
            Win32.POINT pt;
            Win32.GetCursorPos(out pt);
            Rect workPx = GetWorkAreaPixels();
            bool nearBottom = pt.Y >= (int)Math.Round(workPx.Bottom - 4);
            bool inside = IsCursorInsideDock(pt);
            if (nearBottom || inside) _autoHideArmed = true;
            if (nearBottom || inside)
            {
                _leaveAt = DateTime.MinValue;
                if (_autoHidden)
                {
                    _autoHidden = false;
                    PlaceDockPhysical(true);
                }
            }
            else if (!_autoHidden && !inside && _autoHideArmed)
            {
                if (_leaveAt == DateTime.MinValue) _leaveAt = DateTime.UtcNow;
                if ((DateTime.UtcNow - _leaveAt).TotalMilliseconds > 650)
                {
                    _autoHidden = true;
                    PlaceDockPhysical(false);
                }
            }
        }

        private Rect GetWorkAreaPixels()
        {
            try
            {
                IntPtr mon = Win32.MonitorFromWindow(new System.Windows.Interop.WindowInteropHelper(this).Handle, Win32.MONITOR_DEFAULTTONEAREST);
                var info = new Win32.MONITORINFO();
                info.cbSize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(Win32.MONITORINFO));
                Win32.GetMonitorInfo(mon, ref info);
                return new Rect(info.rcWork.Left, info.rcWork.Top, info.rcWork.Width, info.rcWork.Height);
            }
            catch { return new Rect(0, 0, SystemParameters.WorkArea.Width * _scale, SystemParameters.WorkArea.Height * _scale); }
        }

        private bool IsCursorInsideDock(Win32.POINT pt)
        {
            try
            {
                if (!IsVisible) return false;
                IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
                Win32.RECT r;
                if (!Win32.GetWindowRect(hwnd, out r)) return false;
                return pt.X >= r.Left - 8 && pt.X <= r.Right + 8 && pt.Y >= r.Top - 8 && pt.Y <= r.Bottom + 8;
            }
            catch { return false; }
        }

        private bool IsFullscreenAppActive()
        {
            return FullscreenDetector.IsActive();
        }

        private void UpdateRunning()
        {
            if (!IsVisible) return;
            try
            {
                foreach (DockEntry e in _entries)
                {
                    if (e.Pin == null) { if (e.Dot != null) e.Dot.Visibility = Visibility.Collapsed; continue; }
                    e.Dot.Visibility = _app.Dock.IsRunning(e.Pin) ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            catch { }
        }

        private void UpdateMusicEntry()
        {
            if (_musicEntry == null) return;
            DockEntry e = _musicEntry;
            BitmapSource cover = _app.Player != null && _app.Player.Current != null ? _app.Player.Current.Cover : null;
            if (cover != null)
            {
                e.Image.Source = cover;
                e.Fallback.Visibility = Visibility.Collapsed;
            }
            else
            {
                e.Image.Source = null;
                e.Fallback.Visibility = Visibility.Visible;
            }
            bool playing = _app.Player != null && _app.Player.IsPlaying;
            if (e.MusicBars != null) e.MusicBars.Opacity = playing ? 1.0 : 0.35;
        }

        private void OnBeat(object sender, EventArgs e)
        {
            if (!IsVisible || !_app.Settings.Music.DockBeat || _app.Audio == null) return;
            float bass = _app.Audio.Bass;
            float beat = _app.Audio.Beat;
            if (_musicEntry != null && _musicEntry.MusicRing != null)
            {
                _musicEntry.MusicRing.Opacity = 0.25 + bass * 0.6 + beat * 0.3;
            }
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            e.Effects = DropEffectFor(e.Data);
            e.Handled = true;
        }

        public const string PinFormat = "DeskOrder.Pin";

        /// <summary>
        /// Internal pin drags must be allowed as Move: the old handler only accepted
        /// FileDrop, so reordering by dragging an icon was rejected by our own window.
        /// </summary>
        public static DragDropEffects DropEffectFor(IDataObject data)
        {
            if (data == null) return DragDropEffects.None;
            try
            {
                if (data.GetDataPresent(PinFormat)) return DragDropEffects.Move;
                if (data.GetDataPresent(DataFormats.FileDrop)) return DragDropEffects.Copy;
            }
            catch { }
            return DragDropEffects.None;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            try
            {
                if (e.Data.GetDataPresent(PinFormat))
                {
                    string id = e.Data.GetData(PinFormat) as string;
                    Point p = e.GetPosition(_items);
                    HandlePinDrop(id, p.X);
                }
                else if (e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    HandleFileDrop((string[])e.Data.GetData(DataFormats.FileDrop));
                }
                e.Handled = true;
            }
            catch (Exception ex) { _app.Notify("拖入 Dock 失败：" + ex.Message, true); }
        }

        /// <summary>Moves the pin whose id is given to the slot under x. Public for tests.</summary>
        public bool HandlePinDrop(string id, double x)
        {
            if (string.IsNullOrEmpty(id)) return false;
            int from = -1;
            List<DockPin> pins = _app.Dock.Pins;
            for (int i = 0; i < pins.Count; i++) if (pins[i].Id == id) { from = i; break; }
            if (from < 0) return false;
            double slot = _slot > 1 ? _slot : 1;
            int to = (int)Math.Round((x - slot / 2) / slot);
            if (to < 0) to = 0;
            if (to > pins.Count - 1) to = pins.Count - 1;
            if (to == from) return false;
            _app.Dock.Reorder(from, to);
            Log.Info("Dock reorder by drag " + id + " " + from + " -> " + to);
            return true;
        }

        /// <summary>Adds dropped files/folders to the dock. Public for tests.</summary>
        public int HandleFileDrop(string[] files)
        {
            if (files == null) return 0;
            int before = _app.Dock.Count;
            foreach (string f in files)
            {
                try
                {
                    if (File.Exists(f) || Directory.Exists(f)) _app.Dock.Add(f, true);
                }
                catch (Exception ex) { Log.Warn("Dock drop add failed for " + f + ": " + ex.Message); }
            }
            int added = _app.Dock.Count - before;
            Log.Info("Dock drop added " + added + " of " + files.Length);
            string save = _app.Dock.LastSaveError;
            if (!string.IsNullOrEmpty(save)) _app.NotifyOnce("dock-save-drop", "已加入 Dock，但配置无法保存：" + save, true);
            else if (added > 0) _app.Notify("已加入 Dock：" + added + " 项", false);
            return added;
        }

        public bool IsDockVisible { get { return IsVisible && !_autoHidden && !_userHidden; } }

        public void ShowDock()
        {
            _userHidden = false;
            _autoHideArmed = false;
            Show();
            _autoHidden = false;
            LayoutDock();
            Topmost = _app.Settings.Dock.AlwaysOnTop;
            PlaceDockPhysical(true);
        }

        public void HideDock()
        {
            _userHidden = true;
            _autoHideArmed = false;
            if (_app.Settings.Dock.AutoHide)
            {
                _autoHidden = true;
                PlaceDockPhysical(false);
            }
            else Hide();
        }

        public void ApplySettings()
        {
            _layoutTimer.Stop();
            _iconSize = _app.Settings.Dock.IconSize;
            LayoutDock();
            _autoHideArmed = false;
            if (!_userHidden)
            {
                if (_app.Settings.Dock.AutoHide && _autoHidden) PlaceDockPhysical(false);
                else PlaceDockPhysical(true);
            }
            _layoutTimer.Start();
        }

        public void SuspendForMain(bool suspended)
        {
            if (suspended)
            {
                _suppressAuto = true;
                if (IsVisible) Hide();
            }
            else
            {
                _suppressAuto = false;
                if (!_userHidden && !IsVisible)
                {
                    ShowDock();
                }
            }
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            if (_app.Exiting) return;
            e.Cancel = true;
            HideDock();
        }

        private sealed class DockEntry
        {
            public Grid Root;
            public Border IconHost;
            public Image Image;
            public TextBlock Fallback;
            public Ellipse Dot;
            public ScaleTransform Scale;
            public TranslateTransform Translate;
            public DockPin Pin;
            public string SpecialKind;
            public int Index;
            public Ellipse MusicRing;
            public StackPanel MusicBars;
        }
    }
}
