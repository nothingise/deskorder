using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using DeskOrder.App;
using DeskOrder.Core;

namespace DeskOrder.UI
{
    public class OverviewPage : PageView
    {
        public OverviewPage(DeskOrderApp app, MainWindow host) : base(app, host)
        {
            Build();
        }

        public override string Title { get { return "概览"; } }
        public override string Subtitle { get { return "桌面状态与快捷入口"; } }

        public override void Refresh()
        {
            Build();
        }

        private void Build()
        {
            Content = null;
            var scroll = new ScrollViewer();
            scroll.VerticalScrollBarVisibility = ScrollBarVisibility.Hidden;
            scroll.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scroll.Padding = new Thickness(30, 6, 30, 30);
            var root = new StackPanel();
            root.MaxWidth = 1020;
            root.HorizontalAlignment = HorizontalAlignment.Stretch;
            scroll.Content = root;
            Content = scroll;

            var header = UiKit.PageHeader("概览", "一眼看到桌面、任务栏与音乐的状态。");
            root.Children.Add(header);

            // Hero
            var hero = new Border();
            hero.CornerRadius = new CornerRadius(22);
            hero.Margin = new Thickness(0, 20, 0, 0);
            hero.BorderBrush = new SolidColorBrush(Color.FromArgb(44, 255, 255, 255));
            hero.BorderThickness = new Thickness(1);
            hero.Background = new LinearGradientBrush(Color.FromRgb(28, 40, 76), Color.FromRgb(12, 35, 38), new Point(0, 0), new Point(1, 1));
            hero.Effect = new DropShadowEffect { BlurRadius = 30, ShadowDepth = 0, Opacity = 0.35, Color = Colors.Black };
            var heroGrid = new Grid();
            heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            heroGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.05, GridUnitType.Star) });
            heroGrid.Margin = new Thickness(28, 26, 26, 24);
            hero.Child = heroGrid;

            var left = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            var greeting = UiKit.Text(TimeGreeting(), 11.5, Theme.Solid(Color.FromArgb(210, 210, 224, 245)), FontWeights.Medium);
            left.Children.Add(greeting);
            var headline = UiKit.Text("让桌面轻一点。", 29, Theme.TextBrush, FontWeights.SemiBold);
            headline.Margin = new Thickness(0, 8, 0, 0);
            left.Children.Add(headline);
            var sub = UiKit.Text("Dock 接管常用与杂物，任务栏沉入壁纸，音乐让桌面有呼吸。", 12, Theme.Solid(Color.FromArgb(205, 222, 232, 246)), null);
            sub.Margin = new Thickness(0, 10, 0, 0);
            sub.MaxWidth = 360;
            left.Children.Add(sub);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 20, 0, 0) };
            var tidy = UiKit.PrimaryButton("整理桌面", delegate { Host.Navigate(1); });
            var dockSettings = UiKit.GhostButton("Dock 设置", delegate { Host.Navigate(1); });
            dockSettings.Margin = new Thickness(10, 0, 0, 0);
            actions.Children.Add(tidy);
            actions.Children.Add(dockSettings);
            left.Children.Add(actions);
            heroGrid.Children.Add(left);

            var preview = new DockPreviewControl();
            preview.Rebuild(App.Dock.Pins);
            preview.Margin = new Thickness(20, 0, 0, 0);
            preview.HorizontalAlignment = HorizontalAlignment.Stretch;
            preview.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(preview, 1);
            heroGrid.Children.Add(preview);
            root.Children.Add(hero);

            // Stats
            int pins = App.Dock.Count;
            int shortcuts = SafeShortcutCount();
            string taskbar = App.Settings.Taskbar.Enabled ? ModeName() : "默认";
            string music = App.Player.Current != null ? App.Player.Current.DisplayTitle : "未播放";
            var stats = new Grid { Margin = new Thickness(0, 16, 0, 0) };
            stats.ColumnDefinitions.Add(new ColumnDefinition());
            stats.ColumnDefinitions.Add(new ColumnDefinition());
            stats.ColumnDefinitions.Add(new ColumnDefinition());
            stats.ColumnDefinitions.Add(new ColumnDefinition());
            AddStat(stats, 0, "DOCK 项目", pins.ToString(), "已固定", Theme.Accent);
            AddStat(stats, 1, "桌面快捷方式", shortcuts.ToString(), shortcuts > 0 ? "可收进 Dock" : "干净", Theme.Accent2);
            AddStat(stats, 2, "任务栏", taskbar, App.Settings.Taskbar.Enabled ? "已启用" : "未修改", App.Settings.Taskbar.Enabled ? Theme.Success : Theme.Muted);
            AddStat(stats, 3, "音乐", Shorten(music, 10), App.Player.IsPlaying ? "播放中" : "环境静音", App.Player.IsPlaying ? Theme.Success : Theme.Muted);
            root.Children.Add(stats);

            // Quick controls
            var quick = UiKit.Card(20, 22);
            quick.Margin = new Thickness(0, 16, 0, 0);
            var quickStack = new StackPanel();
            quickStack.Children.Add(UiKit.Text("快捷开关", 15.5, Theme.TextBrush, FontWeights.SemiBold));
            var autoHide = new ToggleSwitch();
            autoHide.IsChecked = App.Settings.Dock.AutoHide;
            autoHide.Toggled += delegate
            {
                App.Settings.Dock.AutoHide = autoHide.IsChecked;
                SettingsStore.Save(App.Settings);
                if (!autoHide.IsChecked && App.DockWindow != null) App.DockWindow.ShowDock();
                if (App.DockWindow != null) App.DockWindow.ApplySettings();
                Host.RefreshActive();
            };
            quickStack.Children.Add(UiKit.SettingRow("Dock 自动隐藏", "鼠标贴近屏幕底边时滑出，平时只留一线", autoHide));

            var taskbarSwitch = new ToggleSwitch();
            taskbarSwitch.IsChecked = App.Settings.Taskbar.Enabled;
            taskbarSwitch.Toggled += delegate
            {
                App.SetTaskbarEnabled(taskbarSwitch.IsChecked);
                Host.RefreshActive();
            };
            quickStack.Children.Add(UiKit.SettingRow("任务栏透明", "让任务栏沉入壁纸（Windows 11 实验性）", taskbarSwitch));

            var pure = new ToggleSwitch();
            pure.IsChecked = App.Settings.General.HideDesktopIcons;
            pure.Toggled += delegate
            {
                App.SetPureDesktop(pure.IsChecked);
                Host.RefreshActive();
            };
            quickStack.Children.Add(UiKit.SettingRow("纯净桌面", "隐藏全部桌面图标，把舞台留给壁纸与 Dock", pure));

            var musicSwitch = new ToggleSwitch();
            musicSwitch.IsChecked = App.Settings.Music.WidgetVisible;
            musicSwitch.Toggled += delegate
            {
                if (musicSwitch.IsChecked) App.ShowMusicWidget(true);
                else App.HideMusicWidget();
                Host.RefreshActive();
            };
            quickStack.Children.Add(UiKit.SettingRow("音乐卡片", "桌面上的律动播放器", musicSwitch));
            quick.Child = quickStack;
            root.Children.Add(quick);

            // Bottom row
            var bottom = new Grid { Margin = new Thickness(0, 16, 0, 0) };
            bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.15, GridUnitType.Star) });
            bottom.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var cleanup = UiKit.Card(20, 22);
            var cleanupStack = new StackPanel();
            cleanupStack.Children.Add(UiKit.Text("桌面收纳", 15.5, Theme.TextBrush, FontWeights.SemiBold));
            var csub = UiKit.Text(shortcuts > 0 ? "桌面还有 " + shortcuts + " 个快捷方式。收进 Dock，图标会消失，但随时可以移回桌面。" : "桌面已经很干净。新的快捷方式可以拖进 Dock，或在这里收纳。", 11.5, Theme.MutedBrush, null);
            csub.Margin = new Thickness(0, 6, 0, 0);
            cleanupStack.Children.Add(csub);
            var cbtn = UiKit.GhostButton("去整理", delegate { Host.Navigate(1); });
            cbtn.Margin = new Thickness(0, 16, 0, 0);
            cbtn.HorizontalAlignment = HorizontalAlignment.Left;
            cleanupStack.Children.Add(cbtn);
            cleanup.Child = cleanupStack;
            bottom.Children.Add(cleanup);

            var now = UiKit.Card(20, 22);
            now.Margin = new Thickness(16, 0, 0, 0);
            var nowStack = new StackPanel();
            var nowHead = new Grid();
            nowHead.Children.Add(UiKit.Text("正在播放", 15.5, Theme.TextBrush, FontWeights.SemiBold));
            var wave = new SpectrumControl { Height = 22, Width = 76, HorizontalAlignment = HorizontalAlignment.Right, Audio = App.Audio, Player = App.Player, Intensity = 0.8 };
            nowHead.Children.Add(wave);
            nowStack.Children.Add(nowHead);
            var track = App.Player.Current;
            var nowTitle = UiKit.Text(track == null ? "尚未选择音乐" : track.DisplayTitle, 13.5, Theme.TextBrush, FontWeights.Medium);
            nowTitle.Margin = new Thickness(0, 10, 0, 0);
            nowTitle.TextTrimming = TextTrimming.CharacterEllipsis;
            nowStack.Children.Add(nowTitle);
            var nowArtist = UiKit.Text(track == null ? "播放本地音乐，或让系统音频驱动律动" : (string.IsNullOrEmpty(track.Artist) ? "未知艺术家" : track.Artist), 11, Theme.DimBrush, null);
            nowArtist.Margin = new Thickness(0, 4, 0, 0);
            nowStack.Children.Add(nowArtist);
            var nowActions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 12, 0, 0) };
            var play = UiKit.GhostButton(App.Player.IsPlaying ? "暂停" : "播放", delegate { App.Player.Toggle(); Host.RefreshActive(); });
            var openMusic = UiKit.GhostButton("音乐库", delegate { Host.Navigate(2); });
            openMusic.Margin = new Thickness(8, 0, 0, 0);
            nowActions.Children.Add(play);
            nowActions.Children.Add(openMusic);
            nowStack.Children.Add(nowActions);
            now.Child = nowStack;
            Grid.SetColumn(now, 1);
            bottom.Children.Add(now);
            root.Children.Add(bottom);

            var tip = UiKit.Text("小提示：把桌面或资源管理器里的快捷方式直接拖到 Dock 上，也可以固定。", 10.5, Theme.DimBrush, null);
            tip.Margin = new Thickness(2, 18, 0, 0);
            root.Children.Add(tip);
        }

        private void AddStat(Grid grid, int column, string label, string value, string hint, Color accent)
        {
            var card = UiKit.Card(16, 16);
            card.Margin = new Thickness(column == 0 ? 0 : 5, 0, column == 3 ? 0 : 5, 0);
            var stack = new StackPanel();
            var l = UiKit.Text(label, 9.5, Theme.DimBrush, FontWeights.SemiBold);
            stack.Children.Add(l);
            var v = UiKit.Text(value, 20, Theme.TextBrush, FontWeights.SemiBold);
            v.Margin = new Thickness(0, 8, 0, 0);
            v.TextTrimming = TextTrimming.CharacterEllipsis;
            stack.Children.Add(v);
            var h = UiKit.Text(hint, 10, Theme.Solid(accent), null);
            h.Margin = new Thickness(0, 3, 0, 0);
            stack.Children.Add(h);
            card.Child = stack;
            Grid.SetColumn(card, column);
            grid.Children.Add(card);
        }

        private int SafeShortcutCount()
        {
            try { return DesktopService.EnumerateShortcuts().Count; } catch { return 0; }
        }

        private string ModeName()
        {
            if (App.Settings.Taskbar.Mode == "blur") return "磨砂";
            if (App.Settings.Taskbar.Mode == "acrylic") return "亚克力";
            return "通透";
        }

        private static string TimeGreeting()
        {
            int h = DateTime.Now.Hour;
            if (h < 6) return "夜深了";
            if (h < 11) return "上午好";
            if (h < 14) return "中午好";
            if (h < 18) return "下午好";
            return "晚上好";
        }

        private static string Shorten(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text.Length <= max ? text : text.Substring(0, max) + "…";
        }
    }
}
