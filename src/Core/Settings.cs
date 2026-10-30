using System;
using System.IO;

namespace DeskOrder.Core
{
    public class AppSettings
    {
        public int Version { get; set; }
        public DockSettings Dock { get; set; }
        public TaskbarSettings Taskbar { get; set; }
        public MusicSettings Music { get; set; }
        public GeneralSettings General { get; set; }
        public WindowSettings Window { get; set; }

        public AppSettings()
        {
            Version = 1;
            Dock = new DockSettings();
            Taskbar = new TaskbarSettings();
            Music = new MusicSettings();
            General = new GeneralSettings();
            Window = new WindowSettings();
        }

        public void Normalize()
        {
            if (Version < 1) Version = 1;
            if (Dock == null) Dock = new DockSettings();
            if (Taskbar == null) Taskbar = new TaskbarSettings();
            if (Music == null) Music = new MusicSettings();
            if (General == null) General = new GeneralSettings();
            if (Window == null) Window = new WindowSettings();

            Dock.IconSize = Clamp(Dock.IconSize, 36, 64);
            Dock.Spacing = Clamp(Dock.Spacing, 2, 24);
            Dock.Opacity = Clamp(Dock.Opacity, 40, 100);
            if (string.IsNullOrEmpty(Dock.MonitorDevice)) Dock.MonitorDevice = "primary";

            Taskbar.Opacity = Clamp(Taskbar.Opacity, 0, 90);
            if (Taskbar.Mode != "clear" && Taskbar.Mode != "blur" && Taskbar.Mode != "acrylic") Taskbar.Mode = "clear";
            if (!IsHexColor(Taskbar.Tint)) Taskbar.Tint = "#101418";

            if (Music.Source != "system" && Music.Source != "app") Music.Source = "system";
            if (Music.Style != "bars" && Music.Style != "wave") Music.Style = "bars";
            Music.Intensity = ClampF(Music.Intensity, 0.4, 1.8);
            Music.Volume = ClampF(Music.Volume, 0.0, 1.0);

            if (Music.Repeat != "all" && Music.Repeat != "one" && Music.Repeat != "off") Music.Repeat = "all";
            Music.WidgetWidth = Clamp(Music.WidgetWidth, 360, 640);
            Music.WidgetHeight = Clamp(Music.WidgetHeight, 200, 420);
            Music.VisualizerOffset = Clamp(Music.VisualizerOffset, 0, 160);

            Window.Width = ClampF(Window.Width, 1080, 2600);
            Window.Height = ClampF(Window.Height, 700, 1700);
        }

        private static int Clamp(int v, int lo, int hi) { return v < lo ? lo : (v > hi ? hi : v); }
        private static double ClampF(double v, double lo, double hi) { return v < lo ? lo : (v > hi ? hi : v); }

        private static bool IsHexColor(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            if (value.Length != 7 || value[0] != '#') return false;
            for (int i = 1; i < 7; i++)
            {
                char c = value[i];
                bool ok = (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
                if (!ok) return false;
            }
            return true;
        }
    }

    public class DockSettings
    {
        public int IconSize { get; set; }
        public int Spacing { get; set; }
        public int Opacity { get; set; }
        public bool AutoHide { get; set; }
        public bool Magnify { get; set; }
        public bool AlwaysOnTop { get; set; }
        public bool HideOnFullscreen { get; set; }
        public bool ShowLabels { get; set; }
        public bool MusicButton { get; set; }
        public bool MoveOnDrop { get; set; }
        public string MonitorDevice { get; set; }

        public DockSettings()
        {
            IconSize = 48;
            Spacing = 10;
            Opacity = 88;
            AutoHide = false;
            Magnify = true;
            AlwaysOnTop = true;
            HideOnFullscreen = true;
            ShowLabels = false;
            MusicButton = true;
            MoveOnDrop = true;
            MonitorDevice = "primary";
        }
    }

    public class TaskbarSettings
    {
        public bool Enabled { get; set; }
        public string Mode { get; set; }
        public string Tint { get; set; }
        public int Opacity { get; set; }
        public bool Secondary { get; set; }
        public bool RestoreOnExit { get; set; }
        public bool UseBridgeChild { get; set; }
        public bool AutoHideTaskbar { get; set; }

        public TaskbarSettings()
        {
            Enabled = false;
            Mode = "clear";
            Tint = "#101418";
            Opacity = 0;
            Secondary = true;
            RestoreOnExit = true;
            UseBridgeChild = true;
            AutoHideTaskbar = false;
        }
    }

    public class MusicSettings
    {
        public string Source { get; set; }
        public string Style { get; set; }
        public double Intensity { get; set; }
        public double Volume { get; set; }
        public string Repeat { get; set; }
        public bool Shuffle { get; set; }
        public bool WidgetVisible { get; set; }
        public bool ShowOnDesktop { get; set; }
        public bool ShowVisualizer { get; set; }
        public int VisualizerOffset { get; set; }
        public bool HideVisualizerOnFullscreen { get; set; }
        public double WidgetLeft { get; set; }
        public double WidgetTop { get; set; }
        public int WidgetWidth { get; set; }
        public int WidgetHeight { get; set; }
        public bool DockBeat { get; set; }
        public string LastTrack { get; set; }

        public MusicSettings()
        {
            Source = "system";
            Style = "bars";
            Intensity = 1.0;
            Volume = 0.8;
            Repeat = "all";
            Shuffle = false;
            WidgetVisible = false;
            ShowOnDesktop = false;
            ShowVisualizer = true;
            VisualizerOffset = 12;
            HideVisualizerOnFullscreen = true;
            WidgetLeft = -1;
            WidgetTop = -1;
            WidgetWidth = 424;
            WidgetHeight = 272;
            DockBeat = true;
            LastTrack = "";
        }
    }

    public class GeneralSettings
    {
        public bool Startup { get; set; }
        public bool CloseToTray { get; set; }
        public bool HotkeyDock { get; set; }
        public bool HotkeyMusic { get; set; }
        public bool HideDesktopIcons { get; set; }
        public bool FirstRun { get; set; }
        public bool ReduceMotion { get; set; }

        public GeneralSettings()
        {
            Startup = false;
            CloseToTray = true;
            HotkeyDock = true;
            HotkeyMusic = true;
            HideDesktopIcons = false;
            FirstRun = true;
            ReduceMotion = false;
        }
    }

    public class WindowSettings
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public bool Maximized { get; set; }

        public WindowSettings()
        {
            Left = -1;
            Top = -1;
            Width = 1240;
            Height = 820;
            Maximized = false;
        }
    }

    public static class SettingsStore
    {
        public static AppSettings Load()
        {
            AppSettings settings = Json.Read<AppSettings>(AppPaths.SettingsFile);
            if (settings == null)
            {
                settings = new AppSettings();
                Save(settings);
                return settings;
            }
            settings.Normalize();
            return settings;
        }

        public static void Save(AppSettings settings)
        {
            if (settings == null) return;
            try
            {
                settings.Normalize();
                Json.Write(AppPaths.SettingsFile, settings);
            }
            catch (Exception ex)
            {
                Log.Error("Cannot save settings", ex);
            }
        }
    }
}

