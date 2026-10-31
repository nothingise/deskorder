using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DeskOrder.App;
using DeskOrder.Core;

namespace DeskOrder.UI
{
    public class SettingsPage : PageView
    {
        public SettingsPage(DeskOrderApp app, MainWindow host) : base(app, host)
        {
            Build();
        }

        public override string Title { get { return "设置"; } }
        public override string Subtitle { get { return "启动、快捷键与本地数据。"; } }

        public override void Refresh()
        {
            Build();
        }

        private void Build()
        {
            var scroll = new ScrollViewer();
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            scroll.Padding = new Thickness(30, 6, 30, 30);
            var root = new StackPanel();
            root.MaxWidth = 1020;
            scroll.Content = root;
            Content = scroll;

            root.Children.Add(UiKit.PageHeader("设置", "桌序只在本机运行，不联网、不上传任何数据。"));

            var general = UiKit.Card(20, 22);
            general.Margin = new Thickness(0, 20, 0, 0);
            var gs = new StackPanel();
            gs.Children.Add(UiKit.Text("启动与常驻", 15.5, Theme.TextBrush, FontWeights.SemiBold));
            gs.Children.Add(UiKit.Divider());

            StartupStatus startupStatus = StartupService.Status();
            bool suppressStartupEvent = false;
            var startupSwitch = new ToggleSwitch();
            startupSwitch.IsChecked = startupStatus.Enabled;
            var startupNote = UiKit.Text(StartupService.Describe(startupStatus), 10.5, NoteBrush(startupStatus), null);
            startupNote.Margin = new Thickness(0, 10, 0, 0);
            Action refreshStartupNote = delegate
            {
                StartupStatus now = StartupService.Status();
                startupNote.Text = StartupService.Describe(now);
                startupNote.Foreground = NoteBrush(now);
            };
            startupSwitch.Toggled += delegate
            {
                if (suppressStartupEvent) return;
                StartupOutcome outcome = StartupService.SetEnabled(startupSwitch.IsChecked);
                if (!outcome.Ok)
                {
                    App.Notify("无法修改开机启动项", true);
                    startupNote.Text = "登记失败：" + outcome.Error + "\n" + outcome.Hint;
                    startupNote.Foreground = Theme.DangerBrush;
                    suppressStartupEvent = true;
                    try { startupSwitch.IsChecked = StartupService.IsEnabled(); }
                    finally { suppressStartupEvent = false; }
                    return;
                }
                App.Settings.General.Startup = startupSwitch.IsChecked;
                App.Settings.General.StartupMechanism = startupSwitch.IsChecked ? outcome.Kind.ToString() : "";
                SettingsStore.Save(App.Settings);
                refreshStartupNote();
                App.Notify(startupSwitch.IsChecked ? "开机启动已开启（" + outcome.Mechanism + "）" : "开机启动已关闭", false);
            };
            gs.Children.Add(UiKit.SettingRow("开机启动桌序", "启动后以 Dock + 托盘方式静默运行", startupSwitch));
            gs.Children.Add(startupNote);

            var startupActions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            startupActions.Children.Add(UiKit.GhostButton("重新登记", delegate
            {
                StartupOutcome again = StartupService.SetEnabled(true);
                if (again.Ok)
                {
                    suppressStartupEvent = true;
                    try { startupSwitch.IsChecked = true; }
                    finally { suppressStartupEvent = false; }
                    App.Settings.General.Startup = true;
                    App.Settings.General.StartupMechanism = again.Kind.ToString();
                    SettingsStore.Save(App.Settings);
                    refreshStartupNote();
                    App.Notify("开机启动已重新登记（" + again.Mechanism + "）", false);
                    return;
                }
                startupNote.Text = "登记失败：" + again.Error + "\n" + again.Hint;
                startupNote.Foreground = Theme.DangerBrush;
                App.Notify("无法登记开机启动项", true);
            }));
            var copyStartupCommand = UiKit.GhostButton("复制启动命令", delegate
            {
                try
                {
                    Clipboard.SetText(StartupService.ManualCommand());
                    App.Notify("已复制，可粘贴到普通（非沙箱）PowerShell 窗口执行", false);
                }
                catch { App.Notify("复制失败", true); }
            });
            copyStartupCommand.Margin = new Thickness(8, 0, 0, 0);
            startupActions.Children.Add(copyStartupCommand);
            gs.Children.Add(startupActions);

            var traySwitch = new ToggleSwitch();
            traySwitch.IsChecked = App.Settings.General.CloseToTray;
            traySwitch.Toggled += delegate
            {
                App.Settings.General.CloseToTray = traySwitch.IsChecked;
                SettingsStore.Save(App.Settings);
            };
            gs.Children.Add(UiKit.SettingRow("关闭主窗口时最小化到托盘", "Dock 与音乐卡片继续运行；从托盘退出", traySwitch));

            var reduce = new ToggleSwitch();
            reduce.IsChecked = App.Settings.General.ReduceMotion;
            reduce.Toggled += delegate
            {
                App.Settings.General.ReduceMotion = reduce.IsChecked;
                SettingsStore.Save(App.Settings);
            };
            gs.Children.Add(UiKit.SettingRow("减少动画", "关闭页面淡入、Dock 放大与滑动效果", reduce));
            general.Child = gs;
            root.Children.Add(general);

            var hotkeys = UiKit.Card(20, 22);
            hotkeys.Margin = new Thickness(0, 16, 0, 0);
            var hs = new StackPanel();
            hs.Children.Add(UiKit.Text("全局快捷键", 15.5, Theme.TextBrush, FontWeights.SemiBold));
            hs.Children.Add(UiKit.Divider());
            var dockHotkey = new ToggleSwitch();
            dockHotkey.IsChecked = App.Settings.General.HotkeyDock;
            dockHotkey.Toggled += delegate
            {
                App.Settings.General.HotkeyDock = dockHotkey.IsChecked;
                SettingsStore.Save(App.Settings);
                App.Hotkeys.Apply(App.Settings);
            };
            var dockRow = UiKit.SettingRow("Ctrl + Alt + D", "显示 / 隐藏 Dock", dockHotkey);
            hs.Children.Add(dockRow);
            var musicHotkey = new ToggleSwitch();
            musicHotkey.IsChecked = App.Settings.General.HotkeyMusic;
            musicHotkey.Toggled += delegate
            {
                App.Settings.General.HotkeyMusic = musicHotkey.IsChecked;
                SettingsStore.Save(App.Settings);
                App.Hotkeys.Apply(App.Settings);
            };
            hs.Children.Add(UiKit.SettingRow("Ctrl + Alt + M", "显示 / 隐藏音乐卡片", musicHotkey));
            var hkNote = UiKit.Text("如果快捷键被其他程序占用，桌序会在日志中记录，不影响其他功能。", 10.5, Theme.DimBrush, null);
            hkNote.Margin = new Thickness(0, 4, 0, 0);
            hs.Children.Add(hkNote);
            hotkeys.Child = hs;
            root.Children.Add(hotkeys);

            var data = UiKit.Card(20, 22);
            data.Margin = new Thickness(0, 16, 0, 0);
            var ds = new StackPanel();
            ds.Children.Add(UiKit.Text("本地数据", 15.5, Theme.TextBrush, FontWeights.SemiBold));
            ds.Children.Add(UiKit.Divider());
            var paths = UiKit.Text("配置与 Dock 项目：" + AppPaths.Display(AppPaths.Root) + "\n缓存：" + AppPaths.Display(AppPaths.LocalRoot), 10.5, Theme.DimBrush, null);
            paths.TextWrapping = TextWrapping.Wrap;
            ds.Children.Add(paths);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
            var openData = UiKit.GhostButton("打开数据目录", delegate { OpenFolder(AppPaths.Root); });
            var openLogs = UiKit.GhostButton("打开日志", delegate { OpenFolder(AppPaths.LogsDir); });
            openLogs.Margin = new Thickness(8, 0, 0, 0);
            var openCache = UiKit.GhostButton("打开缓存", delegate { OpenFolder(AppPaths.CacheDir); });
            openCache.Margin = new Thickness(8, 0, 0, 0);
            buttons.Children.Add(openData);
            buttons.Children.Add(openLogs);
            buttons.Children.Add(openCache);
            ds.Children.Add(buttons);
            data.Child = ds;
            root.Children.Add(data);

            var about = UiKit.Card(20, 22);
            about.Margin = new Thickness(0, 16, 0, 0);
            var ab = new StackPanel();
            ab.Children.Add(UiKit.Text("关于桌序", 15.5, Theme.TextBrush, FontWeights.SemiBold));
            ab.Children.Add(UiKit.Divider());
            ab.Children.Add(UiKit.Text("桌序 DeskOrder 1.0", 12.5, Theme.TextBrush, FontWeights.Medium));
            var desc = UiKit.Text("一个把 Dock、任务栏透明与音乐律动放在一起的桌面小工具。所有功能都在本机运行；Dock 整理的快捷方式会存放在桌序自己的目录，随时可以移回桌面。", 11, Theme.MutedBrush, null);
            desc.Margin = new Thickness(0, 7, 0, 0);
            desc.TextWrapping = TextWrapping.Wrap;
            ab.Children.Add(desc);
            var credits = UiKit.Text("参考项目（仅借鉴交互与功能边界，未复制代码）：\nBudsDock · GlassDesk · Bauble · MyDockFinder · TranslucentTB · TaskbarX · SpectrumNet · WPFNet5.SoundVisualization · FluentFlyout", 10, Theme.DimBrush, null);
            credits.Margin = new Thickness(0, 12, 0, 0);
            credits.TextWrapping = TextWrapping.Wrap;
            ab.Children.Add(credits);
            var exitRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
            var exit = UiKit.DangerButton("退出桌序", delegate { App.Exit(); });
            exitRow.Children.Add(exit);
            ab.Children.Add(exitRow);
            about.Child = ab;
            root.Children.Add(about);
        }

        private static Brush NoteBrush(StartupStatus status)
        {
            if (status != null && status.Problem.Length > 0) return Theme.DangerBrush;
            return Theme.DimBrush;
        }

        private static void OpenFolder(string path)
        {
            try
            {
                if (System.IO.Directory.Exists(path)) Process.Start("explorer.exe", path);
            }
            catch { }
        }
    }
}
