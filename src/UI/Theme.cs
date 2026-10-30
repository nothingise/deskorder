using System;
using System.Windows;
using System.Windows.Media;

namespace DeskOrder.UI
{
    public static class Theme
    {
        public static readonly Color Bg = Color.FromRgb(10, 13, 20);
        public static readonly Color Surface = Color.FromRgb(18, 23, 34);
        public static readonly Color Surface2 = Color.FromRgb(23, 30, 44);
        public static readonly Color Surface3 = Color.FromRgb(30, 39, 57);
        public static readonly Color TextColor = Color.FromRgb(242, 245, 250);
        public static readonly Color Muted = Color.FromRgb(152, 163, 184);
        public static readonly Color Dim = Color.FromRgb(110, 122, 144);
        public static readonly Color Accent = Color.FromRgb(124, 155, 255);
        public static readonly Color Accent2 = Color.FromRgb(87, 224, 200);
        public static readonly Color Danger = Color.FromRgb(255, 107, 118);
        public static readonly Color Success = Color.FromRgb(123, 228, 149);

        public static readonly FontFamily UiFont = new FontFamily("Microsoft YaHei UI");
        public static readonly FontFamily IconFont = new FontFamily("Segoe Fluent Icons");

        public static readonly Brush BgBrush = Solid(Bg);
        public static readonly Brush SurfaceBrush = Solid(Surface);
        public static readonly Brush Surface2Brush = Solid(Surface2);
        public static readonly Brush Surface3Brush = Solid(Surface3);
        public static readonly Brush TextBrush = Solid(TextColor);
        public static readonly Brush MutedBrush = Solid(Muted);
        public static readonly Brush DimBrush = Solid(Dim);
        public static readonly Brush AccentBrush = Solid(Accent);
        public static readonly Brush Accent2Brush = Solid(Accent2);
        public static readonly Brush DangerBrush = Solid(Danger);
        public static readonly Brush SuccessBrush = Solid(Success);
        public static readonly Brush StrokeBrush = new SolidColorBrush(Color.FromArgb(22, 255, 255, 255));
        public static readonly Brush StrokeSoft = new SolidColorBrush(Color.FromArgb(12, 255, 255, 255));
        public static readonly Brush AccentGradient = Gradient(Accent, Accent2);
        public static readonly Brush TextOnAccent = Solid(Color.FromRgb(8, 13, 24));

        public static SolidColorBrush Solid(Color c)
        {
            var b = new SolidColorBrush(c);
            b.Freeze();
            return b;
        }

        public static Brush Gradient(Color a, Color b)
        {
            var g = new LinearGradientBrush(a, b, new Point(0, 0), new Point(1, 1));
            g.Freeze();
            return g;
        }

        public static Color Alpha(Color c, int alpha)
        {
            return Color.FromArgb((byte)Math.Max(0, Math.Min(255, alpha)), c.R, c.G, c.B);
        }

        public static SolidColorBrush Hex(string hex)
        {
            try
            {
                if (!hex.StartsWith("#")) hex = "#" + hex;
                if (hex.Length == 7) hex = "#FF" + hex.Substring(1);
                return Solid((Color)ColorConverter.ConvertFromString(hex));
            }
            catch { return Solid(Surface2); }
        }

        public static FontFamily Ui(double size)
        {
            return UiFont;
        }
    }
}
