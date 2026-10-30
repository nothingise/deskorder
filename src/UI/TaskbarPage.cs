using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DeskOrder.App;
using DeskOrder.Core;

namespace DeskOrder.UI
{
    public class TaskbarPage : PageView
    {
        private Border _previewBar;
        private TextBlock _status;

        public TaskbarPage(DeskOrderApp app, MainWindow host) : base(app, host)
        {
            Build();
        }

        public override string Title { get { return "任务栏"; } }
        public override string Subtitle { get { return "让任务栏沉入壁纸，只留下内容。"; } }

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

            root.Children.Add(UiKit.PageHeader("任务栏", "通透、磨砂或亚克力；不注入 Explorer，退出时自动恢复。"));

            // Preview
            var previewCard = UiKit.Card(20, 0);
            previewCard.Margin = new Thickness(0, 20, 0, 0);
            var stage = new Grid { Height = 260, ClipToBounds = true };
            stage.Children.Add(BuildWallpaper());
            var barHost = new Grid { VerticalAlignment = VerticalAlignment.Bottom };
            _previewBar = BuildTaskbarVisual();
            barHost.Children.Add(_previewBar);
            stage.Children.Add(barHost);
            var label = UiKit.Text("效果示意 · 实际以系统为准", 10, Theme.Solid(Color.FromArgb(190, 225, 234, 248)), null);
            label.HorizontalAlignment = HorizontalAlignment.Left;
            label.VerticalAlignment = VerticalAlignment.Top;
            label.Margin = new Thickness(18, 14, 0, 0);
            stage.Children.Add(label);
            previewCard.Child = stage;
            root.Children.Add(previewCard);

            // Main switch
            var mainCard = UiKit.Card(20, 22);
            mainCard.Margin = new Thickness(0, 16, 0, 0);
            var mainStack = new StackPanel();
            var mainHead = new Grid();
            mainStack.Children.Add(UiKit.Text("任务栏效果", 15.5, Theme.TextBrush, FontWeights.SemiBold));
            var toggle = new ToggleSwitch();
            toggle.IsChecked = App.Settings.Taskbar.Enabled;
            toggle.HorizontalAlignment = HorizontalAlignment.Right;
            toggle.VerticalAlignment = VerticalAlignment.Center;
            toggle.Toggled += delegate
            {
                App.SetTaskbarEnabled(toggle.IsChecked);
                Host.RefreshActive();
            };
            mainHead.Children.Add(toggle);
            mainStack.Children.Add(mainHead);
            _status = UiKit.Text(TaskbarStatus(), 11, App.Settings.Taskbar.Enabled ? (App.Taskbar.IsApplied ? Theme.SuccessBrush : Theme.DangerBrush) : Theme.DimBrush, null);
            _status.Margin = new Thickness(0, 6, 0, 0);
            mainStack.Children.Add(_status);
            if (App.Settings.Taskbar.Enabled && !App.Taskbar.IsApplied)
            {
                var tip = UiKit.Text("这个 Windows 版本没有接受透明效果。可以打开下方“沉浸模式”，让任务栏自动收起，只保留 Dock。", 11, Theme.DangerBrush, null);
                tip.TextWrapping = TextWrapping.Wrap;
                tip.Margin = new Thickness(0, 8, 0, 0);
                mainStack.Children.Add(tip);
            }
            if (App.Settings.Taskbar.Enabled)
            {
                var fallback = UiKit.GhostButton(App.Settings.Taskbar.AutoHideTaskbar ? "关闭沉浸模式" : "任务栏没变化？开启沉浸模式", delegate
                {
                    App.SetTaskbarAutoHide(!App.Settings.Taskbar.AutoHideTaskbar);
                });
                fallback.HorizontalAlignment = HorizontalAlignment.Left;
                fallback.Margin = new Thickness(0, 10, 0, 0);
                mainStack.Children.Add(fallback);
            }

            var modes = new Grid { Margin = new Thickness(0, 16, 0, 0) };
            modes.ColumnDefinitions.Add(new ColumnDefinition());
            modes.ColumnDefinitions.Add(new ColumnDefinition());
            modes.ColumnDefinitions.Add(new ColumnDefinition());
            AddModeCard(modes, 0, "clear", "通透", "完全透出壁纸，最干净");
            AddModeCard(modes, 1, "blur", "磨砂", "轻微模糊，保留可读性");
            AddModeCard(modes, 2, "acrylic", "亚克力", "Fluent 材质，颜色更柔和");
            mainStack.Children.Add(modes);

            var opacity = new ThinSlider(App.Settings.Taskbar.Opacity / 90.0);
            opacity.Width = 220;
            opacity.Margin = new Thickness(0, 16, 0, 0);
            opacity.IsEnabled = App.Settings.Taskbar.Mode != "clear";
            opacity.Opacity = App.Settings.Taskbar.Mode == "clear" ? 0.45 : 1.0;
            opacity.ValueChanged += delegate
            {
                int v = (int)Math.Round(opacity.Value * 90);
                App.SetTaskbarOpacity(v);
                UpdatePreview();
            };
            mainStack.Children.Add(UiKit.SettingRow("颜色浓度", "磨砂/亚克力模式的底色强度；通透模式不使用", opacity));

            var tint = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 0) };
            tint.Children.Add(UiKit.Text("底色", 12.5, Theme.TextBrush, FontWeights.Medium));
            string[] tints = new string[] { "#101418", "#0B0F14", "#141019", "#0F1412" };
            for (int i = 0; i < tints.Length; i++)
            {
                string hex = tints[i];
                var chip = new Border { Width = 24, Height = 24, CornerRadius = new CornerRadius(8), Background = Theme.Hex(hex), BorderBrush = new SolidColorBrush(Color.FromArgb(70, 255, 255, 255)), BorderThickness = new Thickness(1), Margin = new Thickness(10, 0, 0, 0), Cursor = System.Windows.Input.Cursors.Hand };
                if (string.Equals(App.Settings.Taskbar.Tint, hex, StringComparison.OrdinalIgnoreCase)) chip.BorderBrush = Theme.AccentBrush;
                chip.MouseLeftButtonUp += delegate
                {
                    App.SetTaskbarTint(hex);
                    Build();
                };
                tint.Children.Add(chip);
            }
            mainStack.Children.Add(tint);

            var secondary = new ToggleSwitch();
            secondary.IsChecked = App.Settings.Taskbar.Secondary;
            secondary.Toggled += delegate
            {
                App.Settings.Taskbar.Secondary = secondary.IsChecked;
                SettingsStore.Save(App.Settings);
                if (App.Settings.Taskbar.Enabled) App.Taskbar.Apply();
            };
            mainStack.Children.Add(UiKit.SettingRow("应用到副屏任务栏", "多显示器时一并处理", secondary));

            var immersive = new ToggleSwitch();
            immersive.IsChecked = App.Settings.Taskbar.AutoHideTaskbar;
            immersive.Toggled += delegate { App.SetTaskbarAutoHide(immersive.IsChecked); };
            mainStack.Children.Add(UiKit.SettingRow("沉浸模式：自动隐藏任务栏", "透明不可用时，用它把任务栏收起，只留 Dock", immersive));

            var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 18, 0, 0) };
            var preview = UiKit.GhostButton("预览 8 秒", delegate
            {
                App.PreviewTaskbar(8);
                App.Notify("任务栏预览已开始，8 秒后自动恢复", false);
            });
            var apply = UiKit.PrimaryButton(App.Settings.Taskbar.Enabled ? "重新应用" : "应用效果", delegate
            {
                App.SetTaskbarEnabled(true);
                Host.RefreshActive();
            });
            apply.Margin = new Thickness(10, 0, 0, 0);
            var restore = UiKit.GhostButton("恢复默认", delegate
            {
                App.SetTaskbarEnabled(false);
                Host.RefreshActive();
            });
            restore.Margin = new Thickness(10, 0, 0, 0);
            var repair = UiKit.GhostButton("修复显示", delegate
            {
                App.Taskbar.Repair();
                App.Notify("已请求任务栏重绘；如仍发黑，请重启资源管理器或重新应用", false);
            });
            repair.Margin = new Thickness(10, 0, 0, 0);
            actions.Children.Add(repair);
            actions.Children.Add(preview);
            actions.Children.Add(apply);
            actions.Children.Add(restore);
            mainStack.Children.Add(actions);

            var note = UiKit.Text("Windows 11 对任务栏背景的限制会随版本变化；如果应用后没有变化，请先点“预览 8 秒”。桌序不会注入 Explorer，也不会在未确认时重启资源管理器。", 10.5, Theme.DimBrush, null);
            note.Margin = new Thickness(0, 14, 0, 0);
            mainStack.Children.Add(note);
            mainCard.Child = mainStack;
            root.Children.Add(mainCard);

            // Pure desktop
            var pureCard = UiKit.Card(20, 22);
            pureCard.Margin = new Thickness(0, 16, 0, 0);
            var pureStack = new StackPanel();
            pureStack.Children.Add(UiKit.Text("纯净桌面", 15.5, Theme.TextBrush, FontWeights.SemiBold));
            var pureSub = UiKit.Text("隐藏全部桌面图标，只留下壁纸、Dock 与任务栏。退出桌序时会自动恢复。", 11.5, Theme.MutedBrush, null);
            pureSub.Margin = new Thickness(0, 6, 0, 0);
            pureStack.Children.Add(pureSub);
            var pure = new ToggleSwitch();
            pure.IsChecked = App.Settings.General.HideDesktopIcons;
            pure.Toggled += delegate
            {
                App.SetPureDesktop(pure.IsChecked);
                Host.RefreshActive();
            };
            pureStack.Children.Add(UiKit.SettingRow("隐藏桌面图标", "适合配合 Dock 使用", pure));
            pureCard.Child = pureStack;
            root.Children.Add(pureCard);
        }

        private Grid BuildWallpaper()
        {
            var g = new Grid();
            g.Background = new LinearGradientBrush(Color.FromRgb(31, 48, 87), Color.FromRgb(14, 55, 57), new Point(0, 0), new Point(1, 1));
            var e = new System.Windows.Shapes.Ellipse { Width = 360, Height = 360, Fill = new RadialGradientBrush(Color.FromArgb(110, 124, 155, 255), Color.FromArgb(0, 124, 155, 255)), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(80, -100, 0, 0) };
            g.Children.Add(e);
            var e2 = new System.Windows.Shapes.Ellipse { Width = 300, Height = 300, Fill = new RadialGradientBrush(Color.FromArgb(90, 87, 224, 200), Color.FromArgb(0, 87, 224, 200)), HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 70, -80) };
            g.Children.Add(e2);
            return g;
        }

        private Border BuildTaskbarVisual()
        {
            var bar = new Border();
            bar.Height = 52;
            bar.BorderThickness = new Thickness(0, 1, 0, 0);
            bar.BorderBrush = new SolidColorBrush(Color.FromArgb(36, 255, 255, 255));
            var grid = new Grid();
            grid.Margin = new Thickness(16, 0, 16, 0);
            var center = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            Color[] colors = new Color[] { Colors.White, Theme.Accent, Theme.Accent2, Color.FromRgb(255, 190, 120), Color.FromRgb(240, 140, 200) };
            for (int i = 0; i < colors.Length; i++)
            {
                var tile = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(7), Background = new SolidColorBrush(Color.FromArgb(230, colors[i].R, colors[i].G, colors[i].B)), Margin = new Thickness(7, 0, 7, 0), Opacity = 0.9 };
                center.Children.Add(tile);
            }
            grid.Children.Add(center);
            var left = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Center };
            var weather = new Border { Width = 66, Height = 24, CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(Color.FromArgb(34, 255, 255, 255)) };
            left.Children.Add(weather);
            grid.Children.Add(left);
            var right = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            right.Children.Add(UiKit.Text("中文", 10, Theme.Solid(Color.FromArgb(190, 240, 245, 252)), null));
            var time = UiKit.Text("20:05  ·  2026/10/29", 10, Theme.Solid(Color.FromArgb(220, 240, 245, 252)), null);
            time.Margin = new Thickness(12, 0, 0, 0);
            right.Children.Add(time);
            grid.Children.Add(right);
            bar.Child = grid;
            UpdatePreviewBar(bar);
            return bar;
        }

        private void UpdatePreview()
        {
            if (_previewBar != null) UpdatePreviewBar(_previewBar);
        }

        private void UpdatePreviewBar(Border bar)
        {
            if (bar == null) return;
            if (!App.Settings.Taskbar.Enabled)
            {
                bar.Background = new SolidColorBrush(Color.FromArgb(235, 20, 25, 35));
                return;
            }
            int alpha = App.Settings.Taskbar.Mode == "clear" ? 0 : (int)Math.Round(App.Settings.Taskbar.Opacity / 90.0 * 180);
            Color tint = Theme.Hex(App.Settings.Taskbar.Tint).Color;
            bar.Background = new SolidColorBrush(Color.FromArgb((byte)alpha, tint.R, tint.G, tint.B));
            if (App.Settings.Taskbar.Mode == "clear")
            {
                bar.BorderBrush = new SolidColorBrush(Color.FromArgb(24, 255, 255, 255));
            }
        }

        private void AddModeCard(Grid grid, int col, string mode, string title, string subtitle)
        {
            bool selected = App.Settings.Taskbar.Mode == mode;
            var card = new Border();
            card.CornerRadius = new CornerRadius(14);
            card.Padding = new Thickness(14, 12, 14, 12);
            card.Margin = new Thickness(col == 0 ? 0 : 5, 0, col == 2 ? 0 : 5, 0);
            card.Background = selected ? new SolidColorBrush(Color.FromArgb(28, 124, 155, 255)) : Theme.Surface2Brush;
            card.BorderBrush = selected ? Theme.AccentBrush : Theme.StrokeBrush;
            card.BorderThickness = new Thickness(selected ? 1.5 : 1);
            card.Cursor = System.Windows.Input.Cursors.Hand;
            var stack = new StackPanel();
            var head = new StackPanel { Orientation = Orientation.Horizontal };
            var swatch = new Border { Width = 18, Height = 18, CornerRadius = new CornerRadius(6), Background = mode == "clear" ? Brushes.Transparent : Theme.Hex(App.Settings.Taskbar.Tint), BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)), BorderThickness = new Thickness(1), Margin = new Thickness(0, 0, 8, 0) };
            head.Children.Add(swatch);
            head.Children.Add(UiKit.Text(title, 13, Theme.TextBrush, FontWeights.SemiBold));
            stack.Children.Add(head);
            var sub = UiKit.Text(subtitle, 10, Theme.DimBrush, null);
            sub.Margin = new Thickness(0, 6, 0, 0);
            sub.TextWrapping = TextWrapping.Wrap;
            stack.Children.Add(sub);
            card.Child = stack;
            card.MouseLeftButtonUp += delegate
            {
                App.SetTaskbarMode(mode);
                Build();
                if (App.Settings.Taskbar.Enabled) App.Taskbar.Apply();
            };
            Grid.SetColumn(card, col);
            grid.Children.Add(card);
        }

        private string TaskbarStatus()
        {
            if (!App.Settings.Taskbar.Enabled) return App.Settings.Taskbar.AutoHideTaskbar ? "透明未启用 · 沉浸模式已开启" : "未启用 · 系统任务栏保持原样";
            if (App.Taskbar.IsApplied) return App.Settings.Taskbar.AutoHideTaskbar ? "透明已应用 · 沉浸模式已开启" : "已应用到任务栏";
            string err = App.Taskbar.LastError;
            return "未生效" + (string.IsNullOrEmpty(err) ? " · 可尝试预览或切换模式" : " · " + err);
        }
    }
}
