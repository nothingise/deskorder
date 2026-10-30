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

namespace DeskOrder.UI
{
    public class MusicPage : PageView
    {
        private Border _cover;
        private TextBlock _title;
        private TextBlock _artist;
        private TextBlock _time;
        private ThinSlider _progress;
        private Border _playButton;
        private StackPanel _playHost;
        private StackPanel _playlist;
        private TextBlock _status;
        private SpectrumControl _spectrum;
        private bool _updatingProgress;
        private string _lastPlaylistSignature = "";

        public MusicPage(DeskOrderApp app, MainWindow host) : base(app, host)
        {
            Build();
            App.Player.TrackChanged += OnTrackChanged;
            App.Player.StateChanged += OnStateChanged;
            App.Player.PositionChanged += OnPosition;
            App.Player.Changed += OnPlayerChanged;
            Unsubscribe.Add(delegate { App.Player.TrackChanged -= OnTrackChanged; });
            Unsubscribe.Add(delegate { App.Player.StateChanged -= OnStateChanged; });
            Unsubscribe.Add(delegate { App.Player.PositionChanged -= OnPosition; });
            Unsubscribe.Add(delegate { App.Player.Changed -= OnPlayerChanged; });
            OnTrackChanged(null, EventArgs.Empty);
            RefreshPlaylist();
        }

        public override string Title { get { return "音乐"; } }
        public override string Subtitle { get { return "本地播放器与系统音频律动"; } }

        public override void Refresh()
        {
            OnTrackChanged(null, EventArgs.Empty);
            RefreshPlaylist();
            if (_status != null) UpdateStatus();
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

            root.Children.Add(UiKit.PageHeader("音乐", "播放本地音乐；频谱默认跟随系统音频，任何播放器的音乐都会让 Dock 呼吸。"));

            // Player
            var card = UiKit.Card(22, 24);
            card.Margin = new Thickness(0, 20, 0, 0);
            var top = new Grid();
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(158) });
            top.ColumnDefinitions.Add(new ColumnDefinition());
            card.Child = top;

            _cover = new Border();
            _cover.Width = 148;
            _cover.Height = 148;
            _cover.CornerRadius = new CornerRadius(20);
            _cover.Background = new LinearGradientBrush(Color.FromRgb(48, 66, 118), Color.FromRgb(18, 92, 86), new Point(0, 0), new Point(1, 1));
            _cover.VerticalAlignment = VerticalAlignment.Top;
            var fallback = UiKit.Text("♪", 50, Theme.TextBrush, FontWeights.Normal);
            fallback.FontFamily = new FontFamily("Segoe UI Symbol");
            fallback.HorizontalAlignment = HorizontalAlignment.Center;
            fallback.VerticalAlignment = VerticalAlignment.Center;
            _cover.Child = fallback;
            top.Children.Add(_cover);

            var info = new StackPanel();
            info.Margin = new Thickness(4, 0, 0, 0);
            Grid.SetColumn(info, 1);
            top.Children.Add(info);

            var head = new Grid();
            _title = UiKit.Text("未在播放", 24, Theme.TextBrush, FontWeights.SemiBold);
            _title.TextTrimming = TextTrimming.CharacterEllipsis;
            head.Children.Add(_title);
            _status = UiKit.Text("", 10.5, Theme.DimBrush, null);
            _status.HorizontalAlignment = HorizontalAlignment.Right;
            _status.VerticalAlignment = VerticalAlignment.Center;
            head.Children.Add(_status);
            info.Children.Add(head);
            _artist = UiKit.Text("选择一首歌，或播放任意应用", 12, Theme.DimBrush, null);
            _artist.Margin = new Thickness(0, 6, 0, 0);
            info.Children.Add(_artist);

            var progRow = new Grid { Margin = new Thickness(0, 18, 0, 0) };
            progRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            progRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _progress = new ThinSlider(0);
            _progress.VerticalAlignment = VerticalAlignment.Center;
            _progress.ValueChanged += delegate
            {
                if (_updatingProgress) return;
                if (App.Player.Current != null) App.Player.Seek(_progress.Value * Math.Max(1, App.Player.Duration));
            };
            progRow.Children.Add(_progress);
            _time = UiKit.Text("0:00 / 0:00", 10.5, Theme.DimBrush, null);
            _time.Margin = new Thickness(12, 0, 0, 0);
            _time.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(_time, 1);
            progRow.Children.Add(_time);
            info.Children.Add(progRow);

            var controls = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var prev = RoundButton("M 2,4 L 4,4 L 4,20 L 2,20 Z M 18,4 L 18,20 L 6,12 Z", 34, delegate { App.Player.Previous(); });
            _playButton = RoundButton(null, 44, delegate { App.Player.Toggle(); });
            _playHost = new StackPanel { Orientation = Orientation.Horizontal };
            _playButton.Child = _playHost;
            var next = RoundButton("M 16,4 L 18,4 L 18,20 L 16,20 Z M 2,4 L 2,20 L 14,12 Z", 34, delegate { App.Player.Next(true); });
            controls.Children.Add(prev);
            controls.Children.Add(_playButton);
            controls.Children.Add(next);

            var vol = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(18, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var vlabel = UiKit.Text("音量", 10.5, Theme.DimBrush, null);
            vlabel.VerticalAlignment = VerticalAlignment.Center;
            vol.Children.Add(vlabel);
            var volume = new ThinSlider(App.Settings.Music.Volume);
            volume.Width = 100;
            volume.Margin = new Thickness(10, 0, 0, 0);
            volume.VerticalAlignment = VerticalAlignment.Center;
            volume.ValueChanged += delegate { App.Player.SetVolume(volume.Value); };
            vol.Children.Add(volume);
            controls.Children.Add(vol);

            var actions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(12, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
            var widget = UiKit.GhostButton("音乐卡片", delegate { App.ShowMusicWidget(true); });
            actions.Children.Add(widget);
            controls.Children.Add(actions);
            info.Children.Add(controls);

            _spectrum = new SpectrumControl();
            _spectrum.Height = 76;
            _spectrum.Margin = new Thickness(0, 18, 0, 0);
            _spectrum.Audio = App.Audio;
            _spectrum.Player = App.Player;
            _spectrum.VisualStyle = App.Settings.Music.Style;
            _spectrum.Intensity = App.Settings.Music.Intensity;
            _spectrum.PlayerOnly = App.Settings.Music.Source == "app";
            info.Children.Add(_spectrum);
            root.Children.Add(card);

            // Playlist
            var plCard = UiKit.Card(20, 22);
            plCard.Margin = new Thickness(0, 16, 0, 0);
            var plStack = new StackPanel();
            var plHead = new Grid();
            plHead.Children.Add(UiKit.Text("播放列表", 15.5, Theme.TextBrush, FontWeights.SemiBold));
            var plActions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var addFiles = UiKit.GhostButton("添加文件", delegate { AddFiles(); });
            var addFolder = UiKit.GhostButton("添加文件夹", delegate { AddFolder(); });
            addFolder.Margin = new Thickness(8, 0, 0, 0);
            var clear = UiKit.GhostButton("清空", delegate { ClearList(); });
            clear.Margin = new Thickness(8, 0, 0, 0);
            plActions.Children.Add(addFiles);
            plActions.Children.Add(addFolder);
            plActions.Children.Add(clear);
            plHead.Children.Add(plActions);
            plStack.Children.Add(plHead);
            var plHint = UiKit.Text("支持 mp3 / wav / wma；也可以把音乐文件拖到音乐卡片。", 10.5, Theme.DimBrush, null);
            plHint.Margin = new Thickness(0, 5, 0, 0);
            plStack.Children.Add(plHint);

            var plScroll = new ScrollViewer { MaxHeight = 320, Margin = new Thickness(0, 12, 0, 0), VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
            _playlist = new StackPanel();
            plScroll.Content = _playlist;
            plStack.Children.Add(plScroll);
            plCard.Child = plStack;
            root.Children.Add(plCard);

            // Options
            var opt = UiKit.Card(20, 22);
            opt.Margin = new Thickness(0, 16, 0, 0);
            var optStack = new StackPanel();
            optStack.Children.Add(UiKit.Text("律动设置", 15.5, Theme.TextBrush, FontWeights.SemiBold));

            var sourceRow = new Grid { Margin = new Thickness(0, 16, 0, 0) };
            sourceRow.ColumnDefinitions.Add(new ColumnDefinition());
            sourceRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var sourceLabel = new StackPanel();
            sourceLabel.Children.Add(UiKit.Text("频谱来源", 13, Theme.TextBrush, FontWeights.Medium));
            sourceLabel.Children.Add(UiKit.Text("系统音频能跟随 QQ 音乐、浏览器以及桌序自身", 10.5, Theme.DimBrush, null));
            sourceRow.Children.Add(sourceLabel);
            var sourceSeg = new Segmented(new string[] { "系统音频", "仅桌序" }, App.Settings.Music.Source == "app" ? 1 : 0, delegate (int i)
            {
                App.Settings.Music.Source = i == 0 ? "system" : "app";
                SettingsStore.Save(App.Settings);
                if (_spectrum != null) _spectrum.PlayerOnly = i == 1;
                if (App.MusicWindow != null) App.MusicWindow.ApplySettings();
                if (App.Visualizer != null) App.Visualizer.ApplySettings();
            });
            Grid.SetColumn(sourceSeg, 1);
            sourceRow.Children.Add(sourceSeg);
            optStack.Children.Add(sourceRow);

            var styleRow = new Grid { Margin = new Thickness(0, 14, 0, 0) };
            styleRow.ColumnDefinitions.Add(new ColumnDefinition());
            styleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            styleRow.Children.Add(UiKit.Text("视觉样式", 13, Theme.TextBrush, FontWeights.Medium));
            var styleSeg = new Segmented(new string[] { "频谱柱", "镜像波" }, App.Settings.Music.Style == "wave" ? 1 : 0, delegate (int i)
            {
                App.Settings.Music.Style = i == 0 ? "bars" : "wave";
                SettingsStore.Save(App.Settings);
                if (_spectrum != null) _spectrum.VisualStyle = App.Settings.Music.Style;
                if (App.MusicWindow != null) App.MusicWindow.ApplySettings();
                if (App.Visualizer != null) App.Visualizer.ApplySettings();
            });
            Grid.SetColumn(styleSeg, 1);
            styleRow.Children.Add(styleSeg);
            optStack.Children.Add(styleRow);

            var visualizerSwitch = new ToggleSwitch();
            visualizerSwitch.IsChecked = App.Settings.Music.ShowVisualizer;
            visualizerSwitch.Toggled += delegate { App.SetVisualizer(visualizerSwitch.IsChecked); };
            optStack.Children.Add(UiKit.SettingRow("桌面透明律动条", "只画频谱，无底板、无边框；启动时直接显示", visualizerSwitch));

            var intensity = new ThinSlider((App.Settings.Music.Intensity - 0.4) / 1.4);
            intensity.Width = 220;
            intensity.Margin = new Thickness(0, 16, 0, 0);
            intensity.ValueChanged += delegate
            {
                double v = 0.4 + intensity.Value * 1.4;
                App.Settings.Music.Intensity = v;
                SettingsStore.Save(App.Settings);
                if (_spectrum != null) _spectrum.Intensity = v;
                if (App.Visualizer != null) App.Visualizer.ApplySettings();
            };
            optStack.Children.Add(UiKit.SettingRow("律动强度", "频谱与 Dock 光晕的幅度", intensity));

            var offset = new ThinSlider(App.Settings.Music.VisualizerOffset / 160.0);
            offset.Width = 220;
            offset.Margin = new Thickness(0, 16, 0, 0);
            offset.ValueChanged += delegate
            {
                int px = (int)Math.Round(offset.Value * 160.0);
                App.Settings.Music.VisualizerOffset = px;
                SettingsStore.Save(App.Settings);
                if (App.Visualizer != null) App.Visualizer.ApplySettings();
            };
            optStack.Children.Add(UiKit.SettingRow("律动条距 Dock", "0 = 贴住 Dock；镜像波觉得太远就调小", offset));

            var fullscreenSwitch = new ToggleSwitch();
            fullscreenSwitch.IsChecked = App.Settings.Music.HideVisualizerOnFullscreen;
            fullscreenSwitch.Toggled += delegate
            {
                App.Settings.Music.HideVisualizerOnFullscreen = fullscreenSwitch.IsChecked;
                SettingsStore.Save(App.Settings);
            };
            optStack.Children.Add(UiKit.SettingRow("全屏时自动隐藏", "游戏、视频或演示全屏时收起律动条", fullscreenSwitch));

            var audioStatus = new TextBlock();
            audioStatus.FontFamily = Theme.UiFont;
            audioStatus.FontSize = 10.5;
            audioStatus.Foreground = Theme.DimBrush;
            audioStatus.Margin = new Thickness(0, 8, 0, 0);
            audioStatus.Text = "系统音频：" + (App.Audio.IsRunning ? (App.Audio.IsActive ? "正在律动" : "等待声音") : "未启动") + (string.IsNullOrEmpty(App.Audio.LastError) ? "" : " · " + App.Audio.LastError);
            optStack.Children.Add(audioStatus);
            opt.Child = optStack;
            root.Children.Add(opt);

            UpdateStatus();
        }

        private Border RoundButton(string geometry, double size, Action click)
        {
            var b = new Border();
            b.Width = size;
            b.Height = size;
            b.CornerRadius = new CornerRadius(size / 2);
            b.Background = new SolidColorBrush(Color.FromArgb(22, 255, 255, 255));
            b.Margin = new Thickness(0, 0, 10, 0);
            b.Cursor = System.Windows.Input.Cursors.Hand;
            b.VerticalAlignment = VerticalAlignment.Center;
            if (geometry != null)
            {
                b.Child = UiKit.PathIcon(geometry, Theme.TextBrush, null, 0);
                ((System.Windows.Shapes.Path)b.Child).Stretch = Stretch.None;
                ((System.Windows.Shapes.Path)b.Child).Width = 24;
                ((System.Windows.Shapes.Path)b.Child).Height = 24;
                ((System.Windows.Shapes.Path)b.Child).HorizontalAlignment = HorizontalAlignment.Center;
                ((System.Windows.Shapes.Path)b.Child).VerticalAlignment = VerticalAlignment.Center;
            }
            b.MouseLeftButtonUp += delegate { click(); };
            b.MouseEnter += delegate { b.Background = new SolidColorBrush(Color.FromArgb(38, 255, 255, 255)); };
            b.MouseLeave += delegate { b.Background = new SolidColorBrush(Color.FromArgb(22, 255, 255, 255)); };
            return b;
        }

        private void OnTrackChanged(object sender, EventArgs e)
        {
            TrackInfo t = App.Player.Current;
            if (_title == null) return;
            _title.Text = t == null ? "未在播放" : t.DisplayTitle;
            _artist.Text = t == null ? "选择一首歌，或播放任意应用" : (string.IsNullOrEmpty(t.Artist) ? "未知艺术家" : t.Artist + (string.IsNullOrEmpty(t.Album) ? "" : " · " + t.Album));
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
                var fallback = UiKit.Text("♪", 50, Theme.TextBrush, FontWeights.Normal);
                fallback.FontFamily = new FontFamily("Segoe UI Symbol");
                fallback.HorizontalAlignment = HorizontalAlignment.Center;
                fallback.VerticalAlignment = VerticalAlignment.Center;
                _cover.Child = fallback;
                _cover.Background = new LinearGradientBrush(Color.FromRgb(48, 66, 118), Color.FromRgb(18, 92, 86), new Point(0, 0), new Point(1, 1));
            }
            RefreshPlayGlyph();
            UpdateStatus();
            RefreshPlaylistSelection();
        }

        private void OnStateChanged(object sender, EventArgs e)
        {
            RefreshPlayGlyph();
            UpdateStatus();
        }

        private void OnPlayerChanged(object sender, EventArgs e)
        {
            string sig = BuildPlaylistSignature();
            if (sig != _lastPlaylistSignature)
            {
                _lastPlaylistSignature = sig;
                RefreshPlaylist();
            }
        }

        private void OnPosition(object sender, EventArgs e)
        {
            if (_progress == null) return;
            double dur = Math.Max(0.001, App.Player.Duration);
            double pos = Math.Max(0, Math.Min(dur, App.Player.Position));
            _updatingProgress = true;
            _progress.Value = pos / dur;
            _updatingProgress = false;
            _time.Text = Format(pos) + " / " + Format(dur);
        }

        private void RefreshPlayGlyph()
        {
            if (_playButton == null) return;
            var glyph = UiKit.PlayGlyph(App.Player.IsPlaying, 18, Theme.TextOnAccent);
            glyph.HorizontalAlignment = HorizontalAlignment.Center;
            glyph.VerticalAlignment = VerticalAlignment.Center;
            var g = new Grid();
            g.Children.Add(glyph);
            _playButton.Child = g;
            _playButton.Background = Theme.AccentGradient;
        }

        private void UpdateStatus()
        {
            if (_status == null) return;
            string state = App.Player.IsPlaying ? "播放中" : (App.Player.Current != null ? "已暂停" : "未播放");
            string audio = App.Audio.IsActive ? "系统音频律动" : "静音";
            _status.Text = state + " · " + audio;
            _status.Foreground = App.Player.IsPlaying ? Theme.SuccessBrush : Theme.DimBrush;
        }

        private string BuildPlaylistSignature()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(App.Player.Index);
            foreach (TrackInfo t in App.Player.Tracks)
            {
                sb.Append('|').Append(t.DisplayTitle).Append('~').Append(t.MetadataLoaded ? "1" : "0");
            }
            return sb.ToString();
        }

        private void RefreshPlaylist()
        {
            if (_playlist == null) return;
            _playlist.Children.Clear();
            _lastPlaylistSignature = BuildPlaylistSignature();
            if (App.Player.Tracks.Count == 0)
            {
                var empty = UiKit.Text("播放列表是空的。添加文件或文件夹开始。", 11.5, Theme.DimBrush, null);
                empty.Margin = new Thickness(0, 14, 0, 10);
                _playlist.Children.Add(empty);
                return;
            }
            for (int i = 0; i < App.Player.Tracks.Count; i++)
            {
                int index = i;
                TrackInfo t = App.Player.Tracks[i];
                bool current = i == App.Player.Index;
                var row = new Border();
                row.CornerRadius = new CornerRadius(10);
                row.Padding = new Thickness(10, 8, 8, 8);
                row.Margin = new Thickness(0, 3, 0, 3);
                row.Background = current ? new SolidColorBrush(Color.FromArgb(30, 124, 155, 255)) : Brushes.Transparent;
                row.Cursor = System.Windows.Input.Cursors.Hand;
                var g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
                var num = UiKit.Text((i + 1).ToString("00"), 10, current ? Theme.AccentBrush : Theme.DimBrush, FontWeights.Medium);
                num.VerticalAlignment = VerticalAlignment.Center;
                g.Children.Add(num);
                var stack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                var title = UiKit.Text(t.DisplayTitle, 12.5, current ? Theme.TextBrush : Theme.Solid(Color.FromArgb(220, 236, 241, 250)), FontWeights.Medium);
                title.TextTrimming = TextTrimming.CharacterEllipsis;
                stack.Children.Add(title);
                var artist = UiKit.Text(string.IsNullOrEmpty(t.Artist) ? "未知艺术家" : t.Artist, 10, Theme.DimBrush, null);
                artist.TextTrimming = TextTrimming.CharacterEllipsis;
                stack.Children.Add(artist);
                Grid.SetColumn(stack, 1);
                g.Children.Add(stack);
                var dur = UiKit.Text(t.Duration > 0 ? "· " + Format(t.Duration) : "", 10, Theme.DimBrush, null);
                dur.VerticalAlignment = VerticalAlignment.Center;
                dur.HorizontalAlignment = HorizontalAlignment.Right;
                Grid.SetColumn(dur, 2);
                g.Children.Add(dur);
                var remove = UiKit.IconButton("×", 26, 11, delegate { App.Player.RemoveAt(index); RefreshPlaylist(); });
                Grid.SetColumn(remove, 3);
                g.Children.Add(remove);
                row.Child = g;
                row.MouseLeftButtonUp += delegate
                {
                    App.Player.PlayIndex(index);
                    RefreshPlaylistSelection();
                };
                _playlist.Children.Add(row);
            }
        }

        private void RefreshPlaylistSelection()
        {
            for (int i = 0; i < _playlist.Children.Count; i++)
            {
                var row = _playlist.Children[i] as Border;
                if (row == null) continue;
                row.Background = i == App.Player.Index ? new SolidColorBrush(Color.FromArgb(30, 124, 155, 255)) : Brushes.Transparent;
            }
        }

        private void AddFiles()
        {
            var dlg = new System.Windows.Forms.OpenFileDialog();
            dlg.Multiselect = true;
            dlg.Filter = "音频|*.mp3;*.wav;*.wma|所有文件|*.*";
            dlg.Title = "添加音乐";
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            int n = App.Player.AddFiles(dlg.FileNames);
            App.Notify(n > 0 ? "已添加 " + n + " 首音乐" : "没有可添加的音频", n == 0);
            RefreshPlaylist();
        }

        private void AddFolder()
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog();
            dlg.Description = "选择音乐文件夹";
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK) return;
            int n = App.Player.AddFolder(dlg.SelectedPath);
            App.Notify(n > 0 ? "已添加 " + n + " 首音乐" : "文件夹里没有支持的音频", n == 0);
            RefreshPlaylist();
        }

        private void ClearList()
        {
            if (App.Player.Tracks.Count == 0) return;
            var result = MessageBox.Show("清空播放列表？不会删除任何音频文件。", "清空播放列表", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;
            App.Player.Clear();
            RefreshPlaylist();
        }

        private static string Format(double seconds)
        {
            if (double.IsNaN(seconds) || seconds < 0) seconds = 0;
            int total = (int)seconds;
            return (total / 60) + ":" + (total % 60).ToString("00");
        }
    }
}
