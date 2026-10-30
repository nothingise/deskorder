using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace DeskOrder.UI
{
    public static class UiKit
    {
        public static TextBlock Text(string text, double size, Brush brush, FontWeight? weight)
        {
            var t = new TextBlock();
            t.Text = text;
            t.FontFamily = Theme.UiFont;
            t.FontSize = size;
            t.Foreground = brush ?? Theme.TextBrush;
            if (weight.HasValue) t.FontWeight = weight.Value;
            t.TextWrapping = TextWrapping.Wrap;
            t.UseLayoutRounding = true;
            TextOptions.SetTextFormattingMode(t, TextFormattingMode.Display);
            return t;
        }

        public static Border Card(double radius, double padding)
        {
            var b = new Border();
            b.CornerRadius = new CornerRadius(radius);
            b.Background = Theme.SurfaceBrush;
            b.BorderBrush = Theme.StrokeBrush;
            b.BorderThickness = new Thickness(1);
            b.Padding = new Thickness(padding);
            b.SnapsToDevicePixels = true;
            return b;
        }

        public static Border Card2(double radius, double padding)
        {
            Border b = Card(radius, padding);
            b.Background = Theme.Surface2Brush;
            return b;
        }

        public static StackPanel Stack(Orientation orientation, double gap)
        {
            var p = new StackPanel();
            p.Orientation = orientation;
            if (gap > 0) p.Margin = new Thickness(0);
            if (gap > 0) AddGap(p, gap);
            return p;
        }

        private static void AddGap(StackPanel p, double gap)
        {
            // Gaps are applied by callers via margins; this helper exists to keep layout code terse.
        }

        public static void Gap(StackPanel panel, double height)
        {
            var g = new Border();
            g.Height = height;
            panel.Children.Add(g);
        }

        public static Border PrimaryButton(string text, RoutedEventHandler click)
        {
            return ButtonCore(text, true, click, 40, 14);
        }

        public static Border SmallPrimaryButton(string text, RoutedEventHandler click)
        {
            return ButtonCore(text, true, click, 32, 12);
        }

        public static Border GhostButton(string text, RoutedEventHandler click)
        {
            return ButtonCore(text, false, click, 36, 12);
        }

        public static Border DangerButton(string text, RoutedEventHandler click)
        {
            Border b = ButtonCore(text, false, click, 36, 12);
            foreach (UIElement child in ((Grid)b.Child).Children)
            {
                var t = child as TextBlock;
                if (t != null) t.Foreground = Theme.DangerBrush;
            }
            return b;
        }

        private static Border ButtonCore(string text, bool primary, RoutedEventHandler click, double height, double fontSize)
        {
            var border = new Border();
            border.Height = height;
            border.CornerRadius = new CornerRadius(11);
            border.Padding = new Thickness(16, 0, 16, 0);
            border.Cursor = Cursors.Hand;
            border.SnapsToDevicePixels = true;
            border.Background = primary ? Theme.AccentGradient : Theme.Surface2Brush;
            border.BorderBrush = primary ? null : Theme.StrokeBrush;
            border.BorderThickness = primary ? new Thickness(0) : new Thickness(1);
            var grid = new Grid();
            var label = Text(text, fontSize, primary ? Theme.TextOnAccent : Theme.TextBrush, FontWeights.SemiBold);
            label.VerticalAlignment = VerticalAlignment.Center;
            label.HorizontalAlignment = HorizontalAlignment.Center;
            grid.Children.Add(label);
            border.Child = grid;
            border.MouseEnter += delegate { border.Opacity = 0.88; };
            border.MouseLeave += delegate { border.Opacity = 1.0; };
            border.MouseLeftButtonDown += delegate { border.RenderTransform = new ScaleTransform(0.98, 0.98); border.RenderTransformOrigin = new Point(0.5, 0.5); };
            border.MouseLeftButtonUp += delegate (object s, MouseButtonEventArgs e) { border.RenderTransform = Transform.Identity; };
            border.MouseLeftButtonUp += delegate (object s, MouseButtonEventArgs e) { if (click != null) click(s, e); };
            return border;
        }

        public static Border IconButton(string glyph, double size, double fontSize, RoutedEventHandler click)
        {
            var b = new Border();
            b.Width = size;
            b.Height = size;
            b.CornerRadius = new CornerRadius(size / 2);
            b.Background = Brushes.Transparent;
            b.Cursor = Cursors.Hand;
            var t = Text(glyph, fontSize, Theme.MutedBrush, FontWeights.Normal);
            t.FontFamily = Theme.IconFont;
            t.HorizontalAlignment = HorizontalAlignment.Center;
            t.VerticalAlignment = VerticalAlignment.Center;
            b.Child = t;
            b.MouseEnter += delegate { b.Background = Theme.Surface2Brush; t.Foreground = Theme.TextBrush; };
            b.MouseLeave += delegate { b.Background = Brushes.Transparent; t.Foreground = Theme.MutedBrush; };
            b.MouseLeftButtonUp += delegate (object s, MouseButtonEventArgs e) { e.Handled = true; if (click != null) click(s, e); };
            return b;
        }

        public static Border Pill(string text, Color color)
        {
            var b = new Border();
            b.CornerRadius = new CornerRadius(99);
            b.Background = Theme.Solid(Theme.Alpha(color, 30));
            b.Padding = new Thickness(9, 4, 9, 4);
            b.HorizontalAlignment = HorizontalAlignment.Left;
            b.Child = Text(text, 11.5, Theme.Solid(color), FontWeights.SemiBold);
            return b;
        }

        public static void Add(StackPanel panel, UIElement element, double topMargin)
        {
            if (element == null) return;
            FrameworkElement fe = element as FrameworkElement;
            if (fe != null && topMargin > 0) fe.Margin = new Thickness(0, topMargin, 0, 0);
            panel.Children.Add(element);
        }

        public static Border CheckItem(string text, bool isChecked, bool enabled, Action<bool> changed)
        {
            bool state = isChecked;
            var row = new Border();
            row.Padding = new Thickness(0, 4, 0, 4);
            row.Cursor = enabled ? Cursors.Hand : Cursors.Arrow;
            row.Opacity = enabled ? 1.0 : 0.55;
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });
            g.ColumnDefinitions.Add(new ColumnDefinition());
            var box = new Border();
            box.Width = 16;
            box.Height = 16;
            box.CornerRadius = new CornerRadius(4);
            box.BorderThickness = new Thickness(1);
            box.HorizontalAlignment = HorizontalAlignment.Left;
            box.VerticalAlignment = VerticalAlignment.Center;
            var check = new System.Windows.Shapes.Path();
            check.Data = System.Windows.Media.Geometry.Parse("M 2,6 L 5.5,9.5 L 12,3");
            check.Stroke = Theme.TextOnAccent;
            check.StrokeThickness = 1.6;
            check.StrokeStartLineCap = PenLineCap.Round;
            check.StrokeEndLineCap = PenLineCap.Round;
            check.Width = 10;
            check.Height = 10;
            check.Stretch = Stretch.Uniform;
            check.HorizontalAlignment = HorizontalAlignment.Center;
            check.VerticalAlignment = VerticalAlignment.Center;
            box.Child = check;
            var label = Text(text, 12.5, Theme.TextBrush, FontWeights.Normal);
            label.VerticalAlignment = VerticalAlignment.Center;
            label.Margin = new Thickness(4, 0, 0, 0);
            label.TextTrimming = TextTrimming.CharacterEllipsis;
            Grid.SetColumn(box, 0);
            g.Children.Add(box);
            Grid.SetColumn(label, 1);
            g.Children.Add(label);
            row.Child = g;
            Action refresh = delegate
            {
                box.Background = state ? Theme.AccentGradient : Brushes.Transparent;
                box.BorderBrush = state ? Brushes.Transparent : new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
                check.Visibility = state ? Visibility.Visible : Visibility.Collapsed;
            };
            refresh();
            if (enabled)
            {
                row.MouseLeftButtonUp += delegate
                {
                    state = !state;
                    refresh();
                    if (changed != null) changed(state);
                };
            }
            return row;
        }

        public static Border Divider()
        {
            var b = new Border();
            b.Height = 1;
            b.Background = Theme.StrokeSoft;
            b.Margin = new Thickness(0, 14, 0, 14);
            return b;
        }

        public static Border SettingRow(string title, string subtitle, UIElement control)
        {
            var g = new Grid();
            g.ColumnDefinitions.Add(new ColumnDefinition());
            g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            var stack = new StackPanel();
            stack.Children.Add(Text(title, 13.5, Theme.TextBrush, FontWeights.Medium));
            if (!string.IsNullOrEmpty(subtitle))
            {
                var s = Text(subtitle, 11.5, Theme.DimBrush, null);
                s.Margin = new Thickness(0, 3, 0, 0);
                stack.Children.Add(s);
            }
            g.Children.Add(stack);
            if (control != null)
            {
                FrameworkElement fe = control as FrameworkElement;
                if (fe != null) fe.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(control, 1);
                g.Children.Add(control);
            }
            var host = new Border();
            host.Padding = new Thickness(0, 13, 0, 13);
            host.Child = g;
            return host;
        }

        public static StackPanel PageHeader(string title, string subtitle)
        {
            var s = new StackPanel();
            s.Children.Add(Text(title, 27, Theme.TextBrush, FontWeights.SemiBold));
            if (!string.IsNullOrEmpty(subtitle))
            {
                var t = Text(subtitle, 12.5, Theme.MutedBrush, null);
                t.Margin = new Thickness(0, 7, 0, 0);
                s.Children.Add(t);
            }
            return s;
        }

        public static ScrollViewer Scroll(UIElement content)
        {
            var sv = new ScrollViewer();
            sv.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            sv.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            sv.CanContentScroll = false;
            sv.Content = content;
            sv.Style = ScrollStyle();
            return sv;
        }

        private static Style _scrollStyle;
        private static Style ScrollStyle()
        {
            if (_scrollStyle != null) return _scrollStyle;
            var style = new Style(typeof(ScrollViewer));
            style.Setters.Add(new Setter(ScrollViewer.BackgroundProperty, Brushes.Transparent));
            _scrollStyle = style;
            return style;
        }

        public static Border LabeledCard(string title, string subtitle, UIElement content)
        {
            Border card = Card(18, 22);
            var root = new StackPanel();
            root.Children.Add(Text(title, 15.5, Theme.TextBrush, FontWeights.SemiBold));
            if (!string.IsNullOrEmpty(subtitle))
            {
                var s = Text(subtitle, 11.5, Theme.DimBrush, null);
                s.Margin = new Thickness(0, 4, 0, 0);
                root.Children.Add(s);
            }
            if (content != null)
            {
                FrameworkElement c = content as FrameworkElement;
                if (c != null) c.Margin = new Thickness(0, 16, 0, 0);
                root.Children.Add(content);
            }
            card.Child = root;
            return card;
        }

        public static Path PathIcon(string data, Brush fill, Brush stroke, double thickness)
        {
            var p = new Path();
            p.Data = System.Windows.Media.Geometry.Parse(data);
            p.Fill = fill;
            p.Stroke = stroke;
            p.StrokeThickness = thickness;
            p.StrokeLineJoin = PenLineJoin.Round;
            p.StrokeStartLineCap = PenLineCap.Round;
            p.StrokeEndLineCap = PenLineCap.Round;
            return p;
        }

        public static FrameworkElement PlayGlyph(bool playing, double size, Brush brush)
        {
            var canvas = new Canvas();
            canvas.Width = size;
            canvas.Height = size;
            if (playing)
            {
                var a = new Border { Width = size * 0.26, Height = size * 0.7, CornerRadius = new CornerRadius(1.5), Background = brush };
                Canvas.SetLeft(a, size * 0.18); Canvas.SetTop(a, size * 0.15);
                var b = new Border { Width = size * 0.26, Height = size * 0.7, CornerRadius = new CornerRadius(1.5), Background = brush };
                Canvas.SetLeft(b, size * 0.56); Canvas.SetTop(b, size * 0.15);
                canvas.Children.Add(a); canvas.Children.Add(b);
            }
            else
            {
                var p = new Path();
                p.Data = System.Windows.Media.Geometry.Parse("M 6,3 L 6,19 L 19,11 Z");
                p.Fill = brush;
                p.Stretch = Stretch.Uniform;
                p.Width = size;
                p.Height = size;
                canvas.Children.Add(p);
            }
            return canvas;
        }
    }

    public class ToggleSwitch : Grid
    {
        private readonly Border _track;
        private readonly Border _knob;
        private bool _checked;
        private readonly TranslateTransform _knobTransform;

        public event EventHandler Toggled;

        public bool IsChecked
        {
            get { return _checked; }
            set { SetChecked(value, false); }
        }

        public ToggleSwitch()
        {
            Width = 40;
            Height = 23;
            Cursor = Cursors.Hand;
            VerticalAlignment = VerticalAlignment.Center;
            _track = new Border();
            _track.CornerRadius = new CornerRadius(11.5);
            _track.Background = Theme.Solid(Color.FromRgb(44, 54, 72));
            Children.Add(_track);
            _knob = new Border();
            _knob.Width = 17;
            _knob.Height = 17;
            _knob.CornerRadius = new CornerRadius(9);
            _knob.Background = Brushes.White;
            _knob.HorizontalAlignment = HorizontalAlignment.Left;
            _knob.VerticalAlignment = VerticalAlignment.Center;
            _knob.Margin = new Thickness(3, 0, 0, 0);
            _knobTransform = new TranslateTransform(0, 0);
            _knob.RenderTransform = _knobTransform;
            Children.Add(_knob);
            MouseLeftButtonUp += delegate { SetChecked(!_checked, true); };
        }

        public void SetChecked(bool value, bool animate)
        {
            if (_checked == value) return;
            _checked = value;
            _track.Background = value ? Theme.AccentGradient : Theme.Solid(Color.FromRgb(44, 54, 72));
            double target = value ? 17 : 0;
            if (animate)
            {
                var anim = new DoubleAnimation(target, TimeSpan.FromMilliseconds(140));
                anim.EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut };
                _knobTransform.BeginAnimation(TranslateTransform.XProperty, anim);
            }
            else
            {
                _knobTransform.X = target;
            }
            EventHandler h = Toggled;
            if (h != null) h(this, EventArgs.Empty);
        }
    }

    public class Segmented : Border
    {
        private readonly string[] _options;
        private readonly Border[] _segments;
        private int _selected;
        private readonly Action<int> _changed;

        public int Selected { get { return _selected; } }

        public Segmented(string[] options, int selected, Action<int> changed)
        {
            _options = options;
            _selected = selected;
            _changed = changed;
            CornerRadius = new CornerRadius(10);
            Background = Theme.Solid(Color.FromRgb(13, 18, 27));
            BorderBrush = Theme.StrokeSoft;
            BorderThickness = new Thickness(1);
            Padding = new Thickness(3);
            var panel = new StackPanel { Orientation = Orientation.Horizontal };
            _segments = new Border[options.Length];
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                var b = new Border();
                b.CornerRadius = new CornerRadius(8);
                b.Padding = new Thickness(14, 7, 14, 7);
                b.Margin = new Thickness(i == 0 ? 0 : 2, 0, 0, 0);
                b.Cursor = Cursors.Hand;
                var t = UiKit.Text(options[i], 12, Theme.MutedBrush, FontWeights.Medium);
                t.HorizontalAlignment = HorizontalAlignment.Center;
                b.Child = t;
                b.MouseLeftButtonUp += delegate { Select(index); };
                _segments[i] = b;
                panel.Children.Add(b);
            }
            Child = panel;
            Refresh();
        }

        public void Select(int index)
        {
            if (index < 0 || index >= _options.Length) return;
            _selected = index;
            Refresh();
            if (_changed != null) _changed(index);
        }

        private void Refresh()
        {
            for (int i = 0; i < _segments.Length; i++)
            {
                bool on = i == _selected;
                _segments[i].Background = on ? Theme.Surface3Brush : Brushes.Transparent;
                var t = _segments[i].Child as TextBlock;
                if (t != null) t.Foreground = on ? Theme.TextBrush : Theme.MutedBrush;
            }
        }
    }

    public class ThinSlider : Grid
    {
        private readonly Border _track;
        private readonly Border _fill;
        private readonly Border _thumb;
        private double _value;
        private bool _dragging;

        public event EventHandler ValueChanged;
        public double Value
        {
            get { return _value; }
            set { SetValueInternal(value, false); }
        }

        public ThinSlider(double value)
        {
            Height = 24;
            Cursor = Cursors.Hand;
            Background = Brushes.Transparent;
            _value = Math.Max(0, Math.Min(1, value));
            _track = new Border();
            _track.Height = 4;
            _track.CornerRadius = new CornerRadius(2);
            _track.Background = Theme.Solid(Color.FromRgb(42, 52, 70));
            _track.VerticalAlignment = VerticalAlignment.Center;
            Children.Add(_track);
            _fill = new Border();
            _fill.Height = 4;
            _fill.CornerRadius = new CornerRadius(2);
            _fill.Background = Theme.AccentGradient;
            _fill.HorizontalAlignment = HorizontalAlignment.Left;
            _fill.VerticalAlignment = VerticalAlignment.Center;
            Children.Add(_fill);
            _thumb = new Border();
            _thumb.Width = 13;
            _thumb.Height = 13;
            _thumb.CornerRadius = new CornerRadius(7);
            _thumb.Background = Brushes.White;
            _thumb.HorizontalAlignment = HorizontalAlignment.Left;
            _thumb.VerticalAlignment = VerticalAlignment.Center;
            _thumb.IsHitTestVisible = false;
            Children.Add(_thumb);
            SizeChanged += delegate { UpdateLayoutForValue(); };
            MouseLeftButtonDown += delegate (object s, MouseButtonEventArgs e) { _dragging = true; CaptureMouse(); SetFromPoint(e.GetPosition(this).X); };
            MouseMove += delegate (object s, MouseEventArgs e) { if (_dragging) SetFromPoint(e.GetPosition(this).X); };
            MouseLeftButtonUp += delegate { _dragging = false; ReleaseMouseCapture(); };
            UpdateLayoutForValue();
        }

        public void SetValueInternal(double value, bool notify)
        {
            value = Math.Max(0, Math.Min(1, value));
            if (Math.Abs(_value - value) < 0.0001) return;
            _value = value;
            UpdateLayoutForValue();
            if (notify && ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }

        private void SetFromPoint(double x)
        {
            double w = ActualWidth;
            if (w <= 1) return;
            double v = x / w;
            v = Math.Max(0, Math.Min(1, v));
            _value = v;
            UpdateLayoutForValue();
            if (ValueChanged != null) ValueChanged(this, EventArgs.Empty);
        }

        private void UpdateLayoutForValue()
        {
            if (ActualWidth <= 0) return;
            double usable = Math.Max(1, ActualWidth - 13);
            _fill.Width = _value * usable;
            _thumb.Margin = new Thickness(_value * usable, 0, 0, 0);
        }
    }
}
