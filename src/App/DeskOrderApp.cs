using System;
using System.Collections.Generic;
using System.Windows;
using DeskOrder.Core;
using DeskOrder.UI;
using DeskOrder.Windows;

namespace DeskOrder.App
{
    public class DeskOrderApp
    {
        public static DeskOrderApp Current;

        public Application WpfApp { get; private set; }
        public AppSettings Settings { get; private set; }
        public TaskbarService Taskbar { get; private set; }
        public AudioEngine Audio { get; private set; }
        public PlayerService Player { get; private set; }
        public DockService Dock { get; private set; }
        public TrayService Tray { get; private set; }
        public HotkeyService Hotkeys { get; private set; }
        public DockWindow DockWindow { get; private set; }
        public MusicWidget MusicWindow { get; private set; }
        public VisualizerWindow Visualizer { get; private set; }
        public MainWindow MainWindow { get; private set; }
        public bool Exiting { get; private set; }

        private bool _desktopIconsHidden;
        private bool _headless;
        private bool _exiting;
        private readonly List<string> _notified = new List<string>();

        public DeskOrderApp(Application app, AppSettings settings, bool headless)
        {
            WpfApp = app;
            Settings = settings;
            _headless = headless;
            Current = this;
        }

        public void Start(bool startHidden)
        {
            Taskbar = new TaskbarService(Settings);
            Audio = new AudioEngine();
            if (!_headless) Audio.Start();
            Player = new PlayerService(Settings, WpfApp.Dispatcher);
            Dock = new DockService(Settings);
            Dock.Changed += delegate { OnDockChanged(); };

            if (!_headless)
            {
                DockWindow = new DockWindow(this);
                DockWindow.Show();
                Tray = new TrayService(this);
                Tray.Show();
                if (AppPaths.IsFallback && !AppPaths.Portable)
                {
                    WpfApp.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (!_exiting) Notify("提示：" + AppPaths.FallbackNote + "，Dock 改动会保存在这里", false);
                    }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                }
                Hotkeys = new HotkeyService(this);
                Hotkeys.Apply(Settings);
                Taskbar.StartIfEnabled();
                if (Settings.Taskbar.AutoHideTaskbar) Taskbar.SetAutoHide(true);
                if (Settings.General.HideDesktopIcons)
                {
                    _desktopIconsHidden = DesktopIcons.SetVisible(false);
                }
                if (Settings.Music.ShowVisualizer)
                {
                    WpfApp.Dispatcher.BeginInvoke(new Action(delegate
                    {
                        if (!_exiting) ShowVisualizer();
                    }), System.Windows.Threading.DispatcherPriority.Loaded);
                }
            }

            if (Settings.General.FirstRun)
            {
                Settings.General.FirstRun = false;
                SettingsStore.Save(Settings);
                if (!_headless && !startHidden) ShowMain(0);
            }
            else if (!startHidden && !_headless)
            {
                ShowMain(0);
            }
        }

        public void ShowMain(int page)
        {
            if (_headless) return;
            if (MainWindow == null)
            {
                MainWindow = new MainWindow(this);
                MainWindow.Closed += delegate { MainWindow = null; };
            }
            if (DockWindow != null) DockWindow.SuspendForMain(true);
            MainWindow.Show();
            if (MainWindow.WindowState == WindowState.Minimized) MainWindow.WindowState = WindowState.Normal;
            MainWindow.Activate();
            MainWindow.Navigate(page);
        }

        public void ResumeDockAfterMain()
        {
            if (DockWindow != null && !_exiting) DockWindow.SuspendForMain(false);
        }

        public void OnDockChanged()
        {
            if (DockWindow != null) DockWindow.RefreshPins();
            if (MainWindow != null) MainWindow.RefreshActive();
            if (Tray != null) Tray.RefreshState();
            if (Dock != null && !string.IsNullOrEmpty(Dock.LastSaveError))
            {
                NotifyOnce("dock-save", "改动已生效但无法保存到 " + AppPaths.Root + "：" + Dock.LastSaveError, true);
            }
        }

        /// <summary>Shows a message at most once per run, for repeating background problems.</summary>
        public void NotifyOnce(string key, string message, bool error)
        {
            if (key != null && _notified.Contains(key)) return;
            if (key != null) _notified.Add(key);
            Log.Info("NOTIFY-ONCE " + key + " " + message);
            Notify(message, error);
        }

        public void ShowVisualizer()
        {
            if (_headless) return;
            if (Visualizer == null) Visualizer = new VisualizerWindow(this);
            Visualizer.ApplySettings();
            Visualizer.ShowOverlay();
            if (Tray != null) Tray.RefreshState();
        }

        public void HideVisualizer()
        {
            if (Visualizer != null) Visualizer.HideOverlay();
            if (Tray != null) Tray.RefreshState();
        }

        public void SetVisualizer(bool show)
        {
            Settings.Music.ShowVisualizer = show;
            SettingsStore.Save(Settings);
            if (show) ShowVisualizer();
            else HideVisualizer();
            if (MainWindow != null) MainWindow.RefreshActive();
        }

        public void ToggleVisualizer()
        {
            if (Visualizer != null && Visualizer.IsOverlayVisible) HideVisualizer();
            else ShowVisualizer();
        }

        public void SetMusicOnDesktop(bool show)
        {
            Settings.Music.ShowOnDesktop = show;
            SettingsStore.Save(Settings);
            if (show) ShowMusicWidget(false);
            else HideMusicWidget();
            if (MainWindow != null) MainWindow.RefreshActive();
            if (Tray != null) Tray.RefreshState();
        }

        public void ShowMusicWidget(bool activate)
        {
            if (_headless) return;
            if (MusicWindow == null)
            {
                MusicWindow = new MusicWidget(this);
                MusicWindow.Closed += delegate { MusicWindow = null; Settings.Music.WidgetVisible = false; SettingsStore.Save(Settings); };
            }
            MusicWindow.ShowNearDock();
            Settings.Music.WidgetVisible = true;
            SettingsStore.Save(Settings);
            if (activate) MusicWindow.Activate();
            if (Tray != null) Tray.RefreshState();
        }

        public void HideMusicWidget()
        {
            if (MusicWindow != null) MusicWindow.HideWidget();
        }

        public void ToggleMusicWidget()
        {
            if (MusicWindow != null && MusicWindow.IsVisible) HideMusicWidget();
            else ShowMusicWidget(true);
        }

        public void ToggleDock()
        {
            if (DockWindow == null) return;
            if (DockWindow.IsDockVisible) DockWindow.HideDock(); else DockWindow.ShowDock();
        }

        public void SetTaskbarEnabled(bool enabled)
        {
            Settings.Taskbar.Enabled = enabled;
            SettingsStore.Save(Settings);
            if (enabled)
            {
                bool ok = Taskbar.Apply();
                if (!ok) Notify("任务栏效果未生效：" + (Taskbar.LastError ?? "当前系统限制"), true);
            }
            else Taskbar.Disable();
            if (MainWindow != null) MainWindow.RefreshActive();
            if (Tray != null) Tray.RefreshState();
        }

        public void PreviewTaskbar(int seconds)
        {
            Taskbar.Preview(seconds, delegate
            {
                if (MainWindow != null) WpfApp.Dispatcher.BeginInvoke(new Action(delegate { MainWindow.RefreshActive(); }));
            });
            if (MainWindow != null) MainWindow.RefreshActive();
        }

        public void SetTaskbarMode(string mode)
        {
            Settings.Taskbar.Mode = mode;
            SettingsStore.Save(Settings);
            if (Settings.Taskbar.Enabled) Taskbar.Apply();
            if (MainWindow != null) MainWindow.RefreshActive();
        }

        public void SetTaskbarOpacity(int percent)
        {
            Settings.Taskbar.Opacity = percent;
            SettingsStore.Save(Settings);
            if (Settings.Taskbar.Enabled) Taskbar.Apply();
        }

        public void SetTaskbarTint(string hex)
        {
            Settings.Taskbar.Tint = hex;
            SettingsStore.Save(Settings);
            if (Settings.Taskbar.Enabled) Taskbar.Apply();
            if (MainWindow != null) MainWindow.RefreshActive();
        }

        public void SetTaskbarAutoHide(bool enabled)
        {
            bool ok = Taskbar.SetAutoHide(enabled);
            if (!ok)
            {
                Notify("任务栏自动隐藏未生效：" + (Taskbar.LastError ?? "系统限制"), true);
                if (MainWindow != null) MainWindow.RefreshActive();
                return;
            }
            Settings.Taskbar.AutoHideTaskbar = enabled;
            SettingsStore.Save(Settings);
            Notify(enabled ? "沉浸模式已开启：任务栏会自动隐藏" : "任务栏自动隐藏已关闭", false);
            if (MainWindow != null) MainWindow.RefreshActive();
        }

        public void SetPureDesktop(bool hidden)
        {
            bool ok = DesktopIcons.SetVisible(!hidden);
            if (!ok)
            {
                Notify("无法修改桌面图标，请从资源管理器正常启动桌序", true);
                if (MainWindow != null) MainWindow.RefreshActive();
                return;
            }
            Settings.General.HideDesktopIcons = hidden;
            _desktopIconsHidden = hidden && DesktopIcons.IsHidden();
            SettingsStore.Save(Settings);
            Notify(hidden ? "桌面图标已隐藏，按快捷键或点击 Dock 可恢复" : "桌面图标已恢复", false);
            if (MainWindow != null) MainWindow.RefreshActive();
            if (Tray != null) Tray.RefreshState();
        }

        public void Notify(string message, bool error)
        {
            Log.Info((error ? "NOTIFY-ERR " : "NOTIFY ") + message);
            if (MainWindow != null && MainWindow.IsVisible)
            {
                MainWindow.ShowToast(message, error);
            }
            else if (Tray != null) Tray.Balloon(message, error);
        }

        public void Exit()
        {
            if (_exiting) return;
            _exiting = true;
            Exiting = true;
            try
            {
                if (Settings.Taskbar.Enabled && Settings.Taskbar.RestoreOnExit) Taskbar.Disable();
                if (Taskbar.IsAutoHideApplied && Settings.Taskbar.RestoreOnExit) Taskbar.RestoreAutoHide();
                if (_desktopIconsHidden || Settings.General.HideDesktopIcons) DesktopIcons.SetVisible(true);
                if (Visualizer != null) Visualizer.CloseOverlay();
                if (Player != null) Player.Dispose();
                if (Audio != null) Audio.Dispose();
                if (Tray != null) Tray.Dispose();
                if (Hotkeys != null) Hotkeys.Dispose();
                SettingsStore.Save(Settings);
            }
            catch (Exception ex) { Log.Error("Exit cleanup", ex); }
            WpfApp.Shutdown();
        }
    }
}
