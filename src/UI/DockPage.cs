using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DeskOrder.App;
using DeskOrder.Core;
using DeskOrder.Interop;

namespace DeskOrder.UI
{
    public class DockPage : PageView
    {
        private readonly List<DesktopItem> _desktopItems = new List<DesktopItem>();
        private bool _moveOnImport = true;
        private DockPreviewControl _preview;

        public DockPage(DeskOrderApp app, MainWindow host) : base(app, host)
        {
            Build();
        }

        public override string Title { get { return "Dock 整理"; } }
        public override string Subtitle { get { return "常用留在手边，杂物收进 Dock。"; } }

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

            root.Children.Add(UiKit.PageHeader("Dock 整理", "把桌面快捷方式收进 Dock 管理目录，桌面图标消失，但仍可随时还原。"));

            // Preview
            var previewCard = UiKit.Card(20, 18);
            previewCard.Margin = new Thickness(0, 20, 0, 0);
            var pv = new StackPanel();
            var pvHead = new Grid();
            pvHead.Children.Add(UiKit.Text("Dock 预览", 14.5, Theme.TextBrush, FontWeights.SemiBold));
            var pvHint = UiKit.Text("拖拽排序 · 右键更多 · 悬停放大", 10.5, Theme.DimBrush, null);
            pvHint.HorizontalAlignment = HorizontalAlignment.Right;
            pvHint.VerticalAlignment = VerticalAlignment.Center;
            pvHead.Children.Add(pvHint);
            pv.Children.Add(pvHead);
            _preview = new DockPreviewControl { Height = 236, Margin = new Thickness(0, 14, 0, 0) };
            _preview.Rebuild(App.Dock.Pins);
            pv.Children.Add(_preview);
            previewCard.Child = pv;
            root.Children.Add(previewCard);

            // Desktop import
            var importCard = UiKit.Card(20, 22);
            importCard.Margin = new Thickness(0, 16, 0, 0);
            var importStack = new StackPanel();
            var importHead = new Grid();
            importHead.Children.Add(UiKit.Text("从桌面整理", 15.5, Theme.TextBrush, FontWeights.SemiBold));
            var refresh = UiKit.GhostButton("刷新", delegate { Refresh(); });
            refresh.HorizontalAlignment = HorizontalAlignment.Right;
            importHead.Children.Add(refresh);
            importStack.Children.Add(importHead);
            var importSub = UiKit.Text("只移动 .lnk / .url 快捷方式；文档、文件夹保持不动。公共桌面的项目只固定，不移动。", 11, Theme.MutedBrush, null);
            importSub.Margin = new Thickness(0, 6, 0, 0);
            importStack.Children.Add(importSub);

            _desktopItems.Clear();
            _desktopItems.AddRange(DesktopService.EnumerateShortcuts());
            if (_desktopItems.Count == 0)
            {
                var empty = UiKit.Text("桌面没有可整理的快捷方式。可以把任意文件拖到 Dock 上固定。", 11.5, Theme.DimBrush, null);
                empty.Margin = new Thickness(0, 14, 0, 0);
                importStack.Children.Add(empty);
            }
            else
            {
                var list = new StackPanel { Margin = new Thickness(0, 12, 0, 0) };
                foreach (DesktopItem item in _desktopItems)
                {
                    var row = new Grid();
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    DesktopItem captured = item;
                    var cb = UiKit.CheckItem(item.DisplayName + (item.IsPublic ? "   · 公共桌面" : ""), item.Selected, !item.IsPublic, delegate (bool value) { captured.Selected = value; });
                    row.Children.Add(cb);
                    var kind = UiKit.Text(item.IsPublic ? "仅固定" : "可移动", 10, Theme.DimBrush, null);
                    kind.VerticalAlignment = VerticalAlignment.Center;
                    Grid.SetColumn(kind, 1);
                    row.Children.Add(kind);
                    list.Children.Add(row);
                }
                importStack.Children.Add(list);

                var options = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0), VerticalAlignment = VerticalAlignment.Center };
                var moveSwitch = new ToggleSwitch();
                moveSwitch.IsChecked = _moveOnImport;
                moveSwitch.Toggled += delegate { _moveOnImport = moveSwitch.IsChecked; };
                var moveLabel = UiKit.Text("移入 Dock 管理目录（桌面图标消失）", 11.5, Theme.MutedBrush, null);
                moveLabel.VerticalAlignment = VerticalAlignment.Center;
                moveLabel.Margin = new Thickness(9, 0, 0, 0);
                options.Children.Add(moveSwitch);
                options.Children.Add(moveLabel);
                importStack.Children.Add(options);
            }

            var importActions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };
            var doImport = UiKit.PrimaryButton("整理选中项", delegate { DoImport(); });
            importActions.Children.Add(doImport);
            var undo = UiKit.GhostButton("撤销上次整理", delegate { UndoImport(); });
            undo.Margin = new Thickness(10, 0, 0, 0);
            undo.IsEnabled = App.Dock.HasImports;
            undo.Opacity = App.Dock.HasImports ? 1.0 : 0.45;
            importActions.Children.Add(undo);
            importStack.Children.Add(importActions);
            importCard.Child = importStack;
            root.Children.Add(importCard);

            // Pins list
            var pinsCard = UiKit.Card(20, 22);
            pinsCard.Margin = new Thickness(0, 16, 0, 0);
            var pinsStack = new StackPanel();
            var pinsHead = new Grid();
            pinsHead.Children.Add(UiKit.Text("Dock 项目", 15.5, Theme.TextBrush, FontWeights.SemiBold));
            var pinActions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, VerticalAlignment = VerticalAlignment.Center };
            var addFile = UiKit.SmallPrimaryButton("添加文件…", delegate { PickFiles(); });
            pinActions.Children.Add(addFile);
            var addFolder = UiKit.GhostButton("添加文件夹…", delegate { PickFolder(); });
            addFolder.Margin = new Thickness(8, 0, 0, 0);
            pinActions.Children.Add(addFolder);
            var sortPins = UiKit.GhostButton("按名称整理", delegate { SortPins(); });
            sortPins.Margin = new Thickness(8, 0, 0, 0);
            pinActions.Children.Add(sortPins);
            pinsHead.Children.Add(pinActions);
            pinsStack.Children.Add(pinsHead);
            var pins = App.Dock.Pins;
            if (pins.Count == 0)
            {
                var empty = UiKit.Text("Dock 还是空的。点右上角「添加文件…」，把任意程序、快捷方式或文件夹固定在 Dock 上。", 11.5, Theme.DimBrush, null);
                empty.Margin = new Thickness(0, 10, 0, 0);
                pinsStack.Children.Add(empty);
            }
            else
            {
                for (int i = 0; i < pins.Count; i++)
                {
                    DockPin pin = pins[i];
                    var row = new Grid { Margin = new Thickness(0, 8, 0, 0) };
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(38) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var icon = new Border { Width = 30, Height = 30, CornerRadius = new CornerRadius(8), Background = new SolidColorBrush(Color.FromArgb(22, 255, 255, 255)), VerticalAlignment = VerticalAlignment.Center };
                    BitmapSource src = null;
                    if (!string.IsNullOrEmpty(pin.CustomIcon)) src = IconService.LoadImageFile(pin.CustomIcon);
                    if (src == null) src = IconService.GetIcon(pin.Path, 48);
                    if (src != null) icon.Child = new Image { Source = src, Width = 24, Height = 24, Stretch = Stretch.Uniform };
                    else icon.Child = UiKit.Text("?", 12, Theme.MutedBrush, null);
                    row.Children.Add(icon);
                    var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
                    var name = UiKit.Text(pin.DisplayName, 12.5, Theme.TextBrush, FontWeights.Medium);
                    name.TextTrimming = TextTrimming.CharacterEllipsis;
                    info.Children.Add(name);
                    var path = UiKit.Text(pin.Path, 10, Theme.DimBrush, null);
                    path.TextTrimming = TextTrimming.CharacterEllipsis;
                    path.Margin = new Thickness(0, 2, 0, 0);
                    info.Children.Add(path);
                    Grid.SetColumn(info, 1);
                    row.Children.Add(info);
                    var buttons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                    int index = i;
                    var up = UiKit.IconButton("↑", 26, 11, delegate { MovePin(pin, -1); });
                    var down = UiKit.IconButton("↓", 26, 11, delegate { MovePin(pin, 1); });
                    var remove = UiKit.IconButton("×", 26, 12, delegate { RemovePin(pin); });
                    buttons.Children.Add(up);
                    buttons.Children.Add(down);
                    if (pin.Managed)
                    {
                        var restore = UiKit.IconButton("↩", 26, 12, delegate { RestorePin(pin); });
                        buttons.Children.Add(restore);
                    }
                    buttons.Children.Add(remove);
                    Grid.SetColumn(buttons, 2);
                    row.Children.Add(buttons);
                    pinsStack.Children.Add(row);
                }
            }
            pinsCard.Child = pinsStack;
            root.Children.Add(pinsCard);

            // Appearance
            var look = UiKit.Card(20, 22);
            look.Margin = new Thickness(0, 16, 0, 0);
            var lookStack = new StackPanel();
            lookStack.Children.Add(UiKit.Text("外观与行为", 15.5, Theme.TextBrush, FontWeights.SemiBold));
            lookStack.Children.Add(UiKit.Divider());

            var sizeRow = new Grid();
            sizeRow.ColumnDefinitions.Add(new ColumnDefinition());
            sizeRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var sizeLabel = new StackPanel();
            sizeLabel.Children.Add(UiKit.Text("图标大小", 13, Theme.TextBrush, FontWeights.Medium));
            sizeLabel.Children.Add(UiKit.Text("Dock 会自动缩小以适配屏幕", 10.5, Theme.DimBrush, null));
            sizeRow.Children.Add(sizeLabel);
            string[] sizes = new string[] { "小", "中", "大" };
            int sizeIndex = App.Settings.Dock.IconSize <= 44 ? 0 : (App.Settings.Dock.IconSize >= 52 ? 2 : 1);
            var sizeSeg = new Segmented(sizes, sizeIndex, delegate (int i)
            {
                App.Settings.Dock.IconSize = i == 0 ? 40 : (i == 1 ? 48 : 56);
                SettingsStore.Save(App.Settings);
                if (App.DockWindow != null) App.DockWindow.ApplySettings();
                if (_preview != null) _preview.Rebuild(App.Dock.Pins);
            });
            Grid.SetColumn(sizeSeg, 1);
            sizeRow.Children.Add(sizeSeg);
            lookStack.Children.Add(sizeRow);

            AddSliderRow(lookStack, "图标间距", App.Settings.Dock.Spacing, 4, 18, delegate (double v)
            {
                App.Settings.Dock.Spacing = (int)Math.Round(v);
                SettingsStore.Save(App.Settings);
                if (App.DockWindow != null) App.DockWindow.ApplySettings();
            });
            AddSliderRow(lookStack, "底板不透明度", App.Settings.Dock.Opacity, 40, 100, delegate (double v)
            {
                App.Settings.Dock.Opacity = (int)Math.Round(v);
                SettingsStore.Save(App.Settings);
                if (App.DockWindow != null) App.DockWindow.ApplySettings();
            });

            var autoHide = new ToggleSwitch();
            autoHide.IsChecked = App.Settings.Dock.AutoHide;
            autoHide.Toggled += delegate
            {
                App.Settings.Dock.AutoHide = autoHide.IsChecked;
                SettingsStore.Save(App.Settings);
                if (App.DockWindow != null) App.DockWindow.ApplySettings();
                if (!autoHide.IsChecked && App.DockWindow != null) App.DockWindow.ShowDock();
            };
            lookStack.Children.Add(UiKit.SettingRow("自动隐藏", "鼠标贴近屏幕底边时滑出", autoHide));

            var magnify = new ToggleSwitch();
            magnify.IsChecked = App.Settings.Dock.Magnify;
            magnify.Toggled += delegate { App.Settings.Dock.Magnify = magnify.IsChecked; SettingsStore.Save(App.Settings); };
            lookStack.Children.Add(UiKit.SettingRow("悬停放大", "图标随鼠标放大并抬起", magnify));

            var fullscreen = new ToggleSwitch();
            fullscreen.IsChecked = App.Settings.Dock.HideOnFullscreen;
            fullscreen.Toggled += delegate { App.Settings.Dock.HideOnFullscreen = fullscreen.IsChecked; SettingsStore.Save(App.Settings); };
            lookStack.Children.Add(UiKit.SettingRow("全屏避让", "游戏或演示时自动隐藏 Dock", fullscreen));

            var musicButton = new ToggleSwitch();
            musicButton.IsChecked = App.Settings.Dock.MusicButton;
            musicButton.Toggled += delegate
            {
                App.Settings.Dock.MusicButton = musicButton.IsChecked;
                SettingsStore.Save(App.Settings);
                if (App.DockWindow != null) App.DockWindow.RefreshPins();
            };
            lookStack.Children.Add(UiKit.SettingRow("显示音乐按钮", "在 Dock 上保留音乐卡片入口", musicButton));

            var dockBeat = new ToggleSwitch();
            dockBeat.IsChecked = App.Settings.Music.DockBeat;
            dockBeat.Toggled += delegate { App.Settings.Music.DockBeat = dockBeat.IsChecked; SettingsStore.Save(App.Settings); };
            lookStack.Children.Add(UiKit.SettingRow("Dock 随音乐律动", "低音驱动光晕，重拍让图标轻抬", dockBeat));
            look.Child = lookStack;
            root.Children.Add(look);
        }

        private void AddSliderRow(StackPanel parent, string title, double value, double min, double max, Action<double> changed)
        {
            var g = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
            var label = UiKit.Text(title, 12.5, Theme.TextBrush, FontWeights.Medium);
            label.VerticalAlignment = VerticalAlignment.Center;
            g.Children.Add(label);
            var slider = new ThinSlider((value - min) / (max - min));
            slider.VerticalAlignment = VerticalAlignment.Center;
            slider.ValueChanged += delegate { changed(min + slider.Value * (max - min)); };
            Grid.SetColumn(slider, 1);
            g.Children.Add(slider);
            parent.Children.Add(g);
        }

        private void DoImport()
        {
            var selected = _desktopItems.Where(delegate (DesktopItem i) { return i.Selected; }).ToList();
            if (selected.Count == 0) { App.Notify("先勾选要整理的快捷方式", true); return; }
            string batch;
            int count = App.Dock.ImportShortcuts(selected, _moveOnImport, out batch);
            if (count > 0) AfterChange("已整理 " + count + " 项到 Dock" + (_moveOnImport ? "，桌面图标已收起" : ""), false);
            else AfterChange("没有需要整理的项", false);
        }

        private void UndoImport()
        {
            int count = App.Dock.UndoLastImport();
            AfterChange(count > 0 ? "已把 " + count + " 个快捷方式移回桌面" : "没有可撤销的整理记录", false);
        }

        private void RemovePin(DockPin pin)
        {
            if (pin.Managed)
            {
                var result = MessageBox.Show(Host, "要把这个快捷方式移回桌面吗？\n\n是：移回桌面\n否：仅从 Dock 移除（文件保留在 Dock 管理目录）\n取消：保留",
                    "从 Dock 移除", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
                if (result == MessageBoxResult.Cancel) return;
                App.Dock.Remove(pin.Id, result == MessageBoxResult.Yes);
            }
            else App.Dock.Remove(pin.Id, false);
            Log.Info("Dock pin removed from page: " + pin.DisplayName);
            AfterChange("已从 Dock 移除 " + pin.DisplayName, false);
        }

        private void RestorePin(DockPin pin)
        {
            App.Dock.Remove(pin.Id, true);
            AfterChange("已把 " + pin.DisplayName + " 移回桌面", false);
        }

        private void MovePin(DockPin pin, int delta)
        {
            int before = IndexOf(pin.Id);
            App.Dock.Move(pin.Id, delta);
            if (IndexOf(pin.Id) == before)
            {
                App.Notify(delta < 0 ? "已经在最前面了" : "已经在最后面了", false);
                return;
            }
            AfterChange("", false);
        }

        private int IndexOf(string id)
        {
            List<DockPin> pins = App.Dock.Pins;
            for (int i = 0; i < pins.Count; i++) if (pins[i].Id == id) return i;
            return -1;
        }

        private void SortPins()
        {
            App.Dock.SortByName();
            AfterChange("已按名称重新整理 Dock", false);
        }

        private void PickFiles()
        {
            try { AddPaths(PathPicker.PickFiles("添加到 Dock", Window.GetWindow(this))); }
            catch (Exception ex) { App.Notify("打开文件选择器失败：" + ex.Message, true); }
        }

        private void PickFolder()
        {
            try
            {
                string folder = PathPicker.PickFolder("选择要固定到 Dock 的文件夹", Window.GetWindow(this));
                if (!string.IsNullOrEmpty(folder)) AddPaths(new string[] { folder });
            }
            catch (Exception ex) { App.Notify("打开文件夹选择器失败：" + ex.Message, true); }
        }

        /// <summary>Adds paths to the dock. Public so the UI test can drive it without a file dialog.</summary>
        public void AddPaths(string[] paths)
        {
            if (paths == null || paths.Length == 0) return;
            int before = App.Dock.Count;
            string failed = "";
            foreach (string path in paths)
            {
                try
                {
                    if (App.Dock.Add(path, false) == null) failed = path;
                }
                catch (Exception ex) { failed = path + "（" + ex.Message + "）"; }
            }
            int added = App.Dock.Count - before;
            Log.Info("Dock add from page: requested=" + paths.Length + " added=" + added + " failed=" + failed);
            AfterChange(added > 0 ? "已添加 " + added + " 项到 Dock" : (string.IsNullOrEmpty(failed) ? "这些项目已经在 Dock 里了" : "无法添加：" + failed), added == 0 && !string.IsNullOrEmpty(failed));
        }

        private void AfterChange(string message, bool error)
        {
            string save = App.Dock.LastSaveError;
            if (!string.IsNullOrEmpty(save))
            {
                App.NotifyOnce("dock-save-page", "改动已生效，但无法保存到 " + AppPaths.Root + "：" + save, true);
            }
            else if (!string.IsNullOrEmpty(message))
            {
                App.Notify(message, error);
            }
            Host.RefreshActive();
        }
    }
}
