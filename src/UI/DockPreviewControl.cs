using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using DeskOrder.Core;
using DeskOrder.Interop;

namespace DeskOrder.UI
{
    public class DockPreviewControl : Grid
    {
        private readonly Border _wallpaper;
        private readonly StackPanel _icons;
        private readonly TextBlock _empty;
        private readonly Border _dock;

        public DockPreviewControl()
        {
            Height = 210;
            Margin = new Thickness(0);
            ClipToBounds = true;

            _wallpaper = new Border();
            _wallpaper.CornerRadius = new CornerRadius(18);
            var bg = new LinearGradientBrush(Color.FromRgb(24, 36, 68), Color.FromRgb(13, 45, 50), new Point(0, 0), new Point(1, 1));
            _wallpaper.Background = bg;
            Children.Add(_wallpaper);

            var glow = new Ellipse();
            glow.Width = 260;
            glow.Height = 260;
            glow.Fill = new RadialGradientBrush(Color.FromArgb(90, 124, 155, 255), Color.FromArgb(0, 124, 155, 255));
            glow.HorizontalAlignment = HorizontalAlignment.Right;
            glow.VerticalAlignment = VerticalAlignment.Top;
            glow.Margin = new Thickness(0, -80, 30, 0);
            glow.IsHitTestVisible = false;
            Children.Add(glow);
            var glow2 = new Ellipse();
            glow2.Width = 220;
            glow2.Height = 220;
            glow2.Fill = new RadialGradientBrush(Color.FromArgb(70, 87, 224, 200), Color.FromArgb(0, 87, 224, 200));
            glow2.HorizontalAlignment = HorizontalAlignment.Left;
            glow2.VerticalAlignment = VerticalAlignment.Bottom;
            glow2.Margin = new Thickness(-40, 0, 0, -50);
            glow2.IsHitTestVisible = false;
            Children.Add(glow2);

            var label = UiKit.Text("桌面预览", 10, Theme.Solid(Color.FromArgb(170, 235, 242, 255)), FontWeights.SemiBold);
            label.HorizontalAlignment = HorizontalAlignment.Left;
            label.VerticalAlignment = VerticalAlignment.Top;
            label.Margin = new Thickness(18, 16, 0, 0);
            Children.Add(label);

            var stageText = UiKit.Text("把常用的留在手边", 19, Theme.TextBrush, FontWeights.SemiBold);
            stageText.HorizontalAlignment = HorizontalAlignment.Left;
            stageText.VerticalAlignment = VerticalAlignment.Top;
            stageText.Margin = new Thickness(18, 42, 0, 0);
            Children.Add(stageText);
            var stageSub = UiKit.Text("快捷方式进 Dock，桌面自然空出来。", 10.5, Theme.Solid(Color.FromArgb(190, 210, 222, 240)), null);
            stageSub.HorizontalAlignment = HorizontalAlignment.Left;
            stageSub.VerticalAlignment = VerticalAlignment.Top;
            stageSub.Margin = new Thickness(18, 72, 0, 0);
            Children.Add(stageSub);

            _dock = new Border();
            _dock.CornerRadius = new CornerRadius(18);
            _dock.Background = new SolidColorBrush(Color.FromArgb(226, 16, 21, 33));
            _dock.BorderBrush = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255));
            _dock.BorderThickness = new Thickness(1);
            _dock.Padding = new Thickness(12, 8, 12, 8);
            _dock.HorizontalAlignment = HorizontalAlignment.Center;
            _dock.VerticalAlignment = VerticalAlignment.Bottom;
            _dock.Margin = new Thickness(0, 0, 0, 14);
            _dock.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 22, ShadowDepth = 0, Opacity = 0.45, Color = Colors.Black };
            _icons = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
            _dock.Child = _icons;
            Children.Add(_dock);

            _empty = UiKit.Text("把桌面快捷方式收进 Dock", 11, Theme.Solid(Color.FromArgb(170, 225, 234, 250)), null);
            _empty.HorizontalAlignment = HorizontalAlignment.Center;
            _empty.VerticalAlignment = VerticalAlignment.Bottom;
            _empty.Margin = new Thickness(0, 0, 0, 22);
            Children.Add(_empty);
        }

        public void Rebuild(List<DockPin> pins)
        {
            _icons.Children.Clear();
            if (pins == null || pins.Count == 0)
            {
                _empty.Visibility = Visibility.Visible;
                _dock.Visibility = Visibility.Collapsed;
                return;
            }
            _empty.Visibility = Visibility.Collapsed;
            _dock.Visibility = Visibility.Visible;
            int count = 0;
            foreach (DockPin pin in pins)
            {
                if (count >= 9) break;
                var img = new Image();
                img.Width = 26;
                img.Height = 26;
                img.Stretch = Stretch.Uniform;
                img.Margin = new Thickness(3, 0, 3, 0);
                BitmapSource src = null;
                if (!string.IsNullOrEmpty(pin.CustomIcon)) src = IconService.LoadImageFile(pin.CustomIcon);
                if (src == null) src = IconService.GetIcon(pin.Path, 48);
                if (src != null) img.Source = src;
                else
                {
                    var fallback = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(7), Background = Theme.AccentGradient, Margin = new Thickness(3, 0, 3, 0) };
                    var t = UiKit.Text(pin.DisplayName.Length > 0 ? pin.DisplayName.Substring(0, 1) : "?", 10, Theme.TextOnAccent, FontWeights.SemiBold);
                    t.HorizontalAlignment = HorizontalAlignment.Center;
                    t.VerticalAlignment = VerticalAlignment.Center;
                    fallback.Child = t;
                    _icons.Children.Add(fallback);
                    count++;
                    continue;
                }
                _icons.Children.Add(img);
                count++;
            }
            var sep = new Border { Width = 1, Height = 20, Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)), Margin = new Thickness(5, 0, 5, 0) };
            _icons.Children.Add(sep);
            var music = new Border { Width = 26, Height = 26, CornerRadius = new CornerRadius(13), Background = new SolidColorBrush(Color.FromArgb(40, 87, 224, 200)), Margin = new Thickness(3, 0, 3, 0) };
            var mt = UiKit.Text("♪", 12, Theme.Accent2Brush, FontWeights.SemiBold);
            mt.FontFamily = new FontFamily("Segoe UI Symbol");
            mt.HorizontalAlignment = HorizontalAlignment.Center;
            mt.VerticalAlignment = VerticalAlignment.Center;
            music.Child = mt;
            _icons.Children.Add(music);
        }
    }
}
