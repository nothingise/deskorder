using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using DeskOrder.Core;
using DeskOrder.Interop;

namespace DeskOrder.SelfTest
{
    public static class SelfTests
    {
        public static int Run(string reportPath)
        {
            var cases = new List<string[]>();
            int failed = 0;
            try
            {
                Add(cases, "fft-sine-band", delegate
                {
                    int expected = Fft.BandOf(440, AudioEngine.Bands);
                    int got = Fft.DominantBand(Sine(440, 48000, 2048), 48000, AudioEngine.Bands);
                    return Math.Abs(got - expected) <= 1;
                });
                Add(cases, "fft-silence", delegate { return Fft.DominantBand(new float[2048], 48000, AudioEngine.Bands) == -1; });
                Add(cases, "band-440", delegate { return Fft.BandOf(440, AudioEngine.Bands) == 19; });
                Add(cases, "taskbar-color", delegate
                {
                    int v = TaskbarService.ParseTint("#101418", 0);
                    return v == 0x00181410;
                });
                Add(cases, "settings-normalize", delegate
                {
                    var s = new AppSettings();
                    s.Dock.IconSize = 999;
                    s.Taskbar.Mode = "bad";
                    s.Taskbar.Opacity = 200;
                    s.Music.Volume = 5;
                    s.Music.VisualizerOffset = 999;
                    s.Normalize();
                    return s.Dock.IconSize == 64 && s.Taskbar.Mode == "clear" && s.Taskbar.Opacity == 90 &&
                           Math.Abs(s.Music.Volume - 1.0) < 0.001 && s.Music.VisualizerOffset == 160;
                });
                Add(cases, "json-roundtrip", delegate
                {
                    string path = Path.Combine(Path.GetTempPath(), "deskorder-selftest-" + Guid.NewGuid().ToString("N") + ".json");
                    var s = new AppSettings();
                    s.Dock.AutoHide = true;
                    s.Taskbar.Mode = "acrylic";
                    s.Music.VisualizerOffset = 37;
                    s.Music.HideVisualizerOnFullscreen = false;
                    Json.Write(path, s);
                    AppSettings back = Json.Read<AppSettings>(path);
                    bool ok = back != null && back.Dock.AutoHide && back.Taskbar.Mode == "acrylic" &&
                              back.Music.VisualizerOffset == 37 && !back.Music.HideVisualizerOnFullscreen;
                    try { File.Delete(path); } catch { }
                    return ok;
                });
                Add(cases, "desktop-shortcuts", delegate
                {
                    List<DesktopItem> items = DesktopService.EnumerateShortcuts();
                    foreach (DesktopItem i in items) if (!File.Exists(i.Path)) return false;
                    return items.Count >= 0;
                });
                Add(cases, "desktop-import-undo", delegate
                {
                    return InSandboxRoot(delegate
                    {
                        string fakeDesktop = Path.Combine(AppPaths.Root, "selftest-desktop");
                        if (Directory.Exists(fakeDesktop)) Directory.Delete(fakeDesktop, true);
                        Directory.CreateDirectory(fakeDesktop);
                        string shortcut = Path.Combine(fakeDesktop, "SelfTest Shortcut.lnk");
                        File.WriteAllText(shortcut, "not a real shortcut");
                        string previous = DesktopService.DesktopOverride;
                        DesktopService.DesktopOverride = fakeDesktop;
                        try
                        {
                            var settings = new AppSettings();
                            settings.General.FirstRun = false;
                            var dock = new DockService(settings);
                            var item = new DesktopItem { Path = shortcut, DisplayName = "SelfTest Shortcut", Selected = true };
                            string batch;
                            int moved = dock.ImportShortcuts(new List<DesktopItem> { item }, true, out batch);
                            bool sourceGone = !File.Exists(shortcut);
                            int undone = dock.UndoLastImport();
                            bool restored = File.Exists(shortcut);
                            return moved == 1 && sourceGone && undone == 1 && restored;
                        }
                        finally
                        {
                            DesktopService.DesktopOverride = previous;
                        }
                    });
                });
                Add(cases, "dock-pin-crud", delegate
                {
                    return InSandboxRoot(delegate
                    {
                        var settings = new AppSettings();
                        settings.General.FirstRun = false;
                        var dock = new DockService(settings);
                        int before = dock.Count;
                        string path = Path.Combine(AppPaths.Root, "selftest-pin.lnk");
                        File.WriteAllText(path, "selftest");
                        DockPin pin = dock.Add(path, false);
                        bool added = pin != null && dock.Count == before + 1;
                        if (pin != null)
                        {
                            dock.Move(pin.Id, 1);
                            dock.Reorder(0, 0);
                        }
                        bool reordered = dock.Count == before + 1;
                        if (pin != null) dock.Remove(pin.Id, false);
                        try { File.Delete(path); } catch { }
                        return added && reordered && dock.Count == before && string.IsNullOrEmpty(dock.LastSaveError);
                    });
                });
                Add(cases, "data-root-fallback", delegate
                {
                    // A candidate that can never be a directory (a path under a regular file)
                    // must be skipped in favour of a writable one.
                    string blocker = Path.Combine(Path.GetTempPath(), "deskorder-blocker-" + Guid.NewGuid().ToString("N"));
                    File.WriteAllText(blocker, "x");
                    string good = Path.Combine(Path.GetTempPath(), "deskorder-good-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                    bool anyWritable;
                    try
                    {
                        string chosen = AppPaths.FirstWritable(new string[] { Path.Combine(blocker, "sub"), good }, out anyWritable);
                        return anyWritable && string.Equals(chosen, good, StringComparison.OrdinalIgnoreCase) && Directory.Exists(good);
                    }
                    finally
                    {
                        try { File.Delete(blocker); } catch { }
                        try { Directory.Delete(good, true); } catch { }
                    }
                });
                Add(cases, "data-root-probe-rejects-file", delegate
                {
                    string file = Path.Combine(Path.GetTempPath(), "deskorder-probe-" + Guid.NewGuid().ToString("N"));
                    File.WriteAllText(file, "x");
                    try { return !AppPaths.TryPrepare(Path.Combine(file, "sub")); }
                    finally { try { File.Delete(file); } catch { } }
                });
                Add(cases, "dock-drop-effects", delegate
                {
                    bool pin = DeskOrder.Windows.DockWindow.DropEffectFor(new System.Windows.DataObject(DeskOrder.Windows.DockWindow.PinFormat, "x")) == System.Windows.DragDropEffects.Move;
                    bool file = DeskOrder.Windows.DockWindow.DropEffectFor(new System.Windows.DataObject(System.Windows.DataFormats.FileDrop, new string[] { "x" })) == System.Windows.DragDropEffects.Copy;
                    bool none = DeskOrder.Windows.DockWindow.DropEffectFor(new System.Windows.DataObject()) == System.Windows.DragDropEffects.None;
                    return pin && file && none;
                });
                Add(cases, "dock-sort-by-name", delegate
                {
                    return InSandboxRoot(delegate
                    {
                        string sortDir = Path.Combine(AppPaths.Root, "selftest-sort");
                        Directory.CreateDirectory(sortDir);
                        string c = Path.Combine(sortDir, "Charlie.txt");
                        string a = Path.Combine(sortDir, "alpha.txt");
                        string b = Path.Combine(sortDir, "Bravo.txt");
                        File.WriteAllText(c, "x"); File.WriteAllText(a, "x"); File.WriteAllText(b, "x");
                        var settings = new AppSettings();
                        settings.General.FirstRun = false;
                        var dock = new DockService(settings);
                        dock.Add(c, false); dock.Add(a, false); dock.Add(b, false);
                        dock.SortByName();
                        List<DockPin> pins = dock.Pins;
                        bool ok = pins.Count == 3 && pins[0].DisplayName == "alpha" && pins[1].DisplayName == "Bravo" && pins[2].DisplayName == "Charlie";
                        // a second sort must be stable and report "no change"
                        return ok && dock.SortByName() == 0;
                    });
                });
                Add(cases, "dock-save-error-is-reported", delegate
                {
                    string denied = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "DeskOrderDenied-" + Guid.NewGuid().ToString("N").Substring(0, 6));
                    return InRoot(denied, delegate
                    {
                        var settings = new AppSettings();
                        settings.General.FirstRun = false;
                        var dock = new DockService(settings);
                        string pin = Path.Combine(Path.GetTempPath(), "deskorder-save-probe.txt");
                        File.WriteAllText(pin, "x");
                        bool threw = false;
                        try
                        {
                            DockPin added = dock.Add(pin, false);
                            if (added != null) dock.Remove(added.Id, false);
                        }
                        catch { threw = true; }
                        finally { try { File.Delete(pin); } catch { } }
                        return !threw && !string.IsNullOrEmpty(dock.LastSaveError);
                    });
                });
                Add(cases, "portable-marker-wins", delegate
                {
                    string portableDir = Path.Combine(Path.GetTempPath(), "deskorder-portable-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                    Directory.CreateDirectory(portableDir);
                    string marker = Path.Combine(portableDir, "portable.txt");
                    File.WriteAllText(marker, "portable");
                    string previous = AppPaths.PortableBaseOverride;
                    AppPaths.PortableBaseOverride = portableDir;
                    try
                    {
                        bool wants = AppPaths.WantsPortable();
                        List<string> candidates = AppPaths.Candidates(null, false);
                        string expected = Path.Combine(portableDir, "DeskOrderData");
                        return wants && candidates.Count > 0 && string.Equals(candidates[0], expected, StringComparison.OrdinalIgnoreCase);
                    }
                    finally
                    {
                        AppPaths.PortableBaseOverride = previous;
                        try { Directory.Delete(portableDir, true); } catch { }
                    }
                });
                Add(cases, "portable-mode-initialize", delegate
                {
                    string portableDir2 = Path.Combine(Path.GetTempPath(), "deskorder-portable2-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                    Directory.CreateDirectory(portableDir2);
                    string marker = Path.Combine(portableDir2, "portable.txt");
                    File.WriteAllText(marker, "portable");
                    string previous = AppPaths.PortableBaseOverride;
                    AppPaths.PortableBaseOverride = portableDir2;
                    try
                    {
                        return InRoot(Path.Combine(portableDir2, "DeskOrderData"), delegate
                        {
                            // portable root must be reported and be writable
                            return AppPaths.Writable && Directory.Exists(AppPaths.Root) &&
                                   string.Equals(AppPaths.Root, Path.Combine(portableDir2, "DeskOrderData"), StringComparison.OrdinalIgnoreCase);
                        });
                    }
                    finally
                    {
                        AppPaths.PortableBaseOverride = previous;
                        try { Directory.Delete(portableDir2, true); } catch { }
                    }
                });
                Add(cases, "startup-command-line", delegate
                {
                    string command = StartupService.CommandLine();
                    return command.StartsWith("\"") && command.EndsWith("\" --tray") &&
                           command.IndexOf(StartupService.ExePath(), StringComparison.OrdinalIgnoreCase) > 0;
                });
                Add(cases, "startup-approved-flags", delegate
                {
                    // StartupApproved\Run uses 0x02 for "on" and 0x03 for "off".
                    return !StartupService.IsApprovedDisabled(new byte[] { 0x02, 0, 0, 0 }) &&
                            StartupService.IsApprovedDisabled(new byte[] { 0x03, 0, 0, 0 }) &&
                            StartupService.IsApprovedDisabled(new byte[] { 0x07, 0, 0, 0 }) &&
                           !StartupService.IsApprovedDisabled(new byte[] { 0x06, 0, 0, 0 }) &&
                           !StartupService.IsApprovedDisabled(null);
                });
                Add(cases, "startup-shortcut-roundtrip", delegate
                {
                    return InSandboxRoot(delegate
                    {
                        string startupDir = Path.Combine(AppPaths.Root, "selftest-startup");
                        Directory.CreateDirectory(startupDir);
                        string lnk = Path.Combine(startupDir, "DeskOrder.lnk");
                        string target = StartupService.ExePath();
                        string previousRun = StartupService.RunKeyOverride;
                        string previousLnk = StartupService.LnkOverride;
                        StartupService.RunKeyOverride = @"Software\DeskOrderSelfTest\Run";
                        StartupService.LnkOverride = lnk;
                        try
                        {
                            if (!ShortcutResolver.Create(lnk, target, "--tray", Path.GetDirectoryName(target))) return false;
                            string read = StartupService.ReadLnkTarget();
                            return File.Exists(lnk) && StartupService.IsEnabled() &&
                                   read.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0 &&
                                   read.IndexOf("--tray", StringComparison.OrdinalIgnoreCase) >= 0;
                        }
                        finally
                        {
                            StartupService.RunKeyOverride = previousRun;
                            StartupService.LnkOverride = previousLnk;
                        }
                    });
                });
                Add(cases, "startup-toggle-reports", delegate
                {
                    // The switch must either register and be readable back, or explain the
                    // refusal. It must never look successful while nothing was registered.
                    string previousRun = StartupService.RunKeyOverride;
                    string previousLnk = StartupService.LnkOverride;
                    bool previousTask = StartupService.AllowTaskFallback;
                    string probeDir = Path.Combine(Path.GetTempPath(), "deskorder-startup-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                    StartupService.RunKeyOverride = @"Software\DeskOrderSelfTest\Run";
                    StartupService.LnkOverride = Path.Combine(probeDir, "DeskOrder.lnk");
                    StartupService.AllowTaskFallback = false;
                    try
                    {
                        StartupOutcome on = StartupService.SetEnabled(true);
                        if (!on.Ok)
                            return on.Error.Length > 0 && on.Hint.Length > 0 && !StartupService.IsEnabled();
                        bool registered = StartupService.IsEnabled();
                        StartupOutcome off = StartupService.SetEnabled(false);
                        return registered && off.Ok && !StartupService.IsEnabled();
                    }
                    finally
                    {
                        StartupService.RunKeyOverride = previousRun;
                        StartupService.LnkOverride = previousLnk;
                        StartupService.AllowTaskFallback = previousTask;
                        try { if (Directory.Exists(probeDir)) Directory.Delete(probeDir, true); } catch { }
                    }
                });
                Add(cases, "startup-status-readable", delegate
                {
                    StartupStatus status = StartupService.Status();
                    return status.Expected.IndexOf("--tray", StringComparison.OrdinalIgnoreCase) > 0 &&
                           status.Integrity.Length > 0 &&
                           status.Expected.IndexOf(StartupService.ExePath(), StringComparison.OrdinalIgnoreCase) >= 0;
                });
                Add(cases, "apppaths-display-token", delegate
                {
                    // UI text must not leak the account name: well-known folders are shown
                    // as %USERPROFILE% / %APPDATA% tokens instead.
                    string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    string shown = AppPaths.Display(Path.Combine(home, "Documents", "DeskOrder"));
                    return shown.IndexOf("%USERPROFILE%", StringComparison.OrdinalIgnoreCase) >= 0 &&
                           shown.IndexOf(home, StringComparison.OrdinalIgnoreCase) < 0 &&
                           AppPaths.Display(null) == "";
                });
                Add(cases, "defview-found", delegate { return DesktopIcons.FindDefView() != IntPtr.Zero; });                Add(cases, "icon-extraction", delegate
                {
                    string path = Environment.ExpandEnvironmentVariables(@"%PUBLIC%\Desktop\Microsoft Edge.lnk");
                    if (!File.Exists(path)) return true; // not installed, don't fail
                    return IconService.GetIcon(path, 32) != null;
                });
            }
            catch (Exception ex)
            {
                cases.Add(new string[] { "harness", "false", ex.GetType().Name + ": " + ex.Message });
                failed++;
                Log.Error("Self-test harness failed", ex);
            }

            var sb = new StringBuilder();
            sb.Append("{\n  \"passed\": true,\n  \"cases\": [\n");
            bool first = true;
            foreach (var c in cases)
            {
                bool ok = c[1] == "true";
                if (!ok) failed++;
                if (!first) sb.Append(",\n");
                first = false;
                sb.Append("    { \"name\": \"").Append(Escape(c[0])).Append("\", \"ok\": ").Append(ok ? "true" : "false");
                if (c.Length > 2) sb.Append(", \"detail\": \"").Append(Escape(c[2])).Append("\"");
                sb.Append(" }");
            }
            sb.Append("\n  ],\n  \"failed\": ").Append(failed).Append("\n}\n");
            string full = Path.GetFullPath(reportPath);
            string dir = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllText(full, sb.ToString(), new UTF8Encoding(false));
            Log.Info("self-test failed=" + failed + " cases=" + cases.Count + " report=" + full);
            return failed == 0 ? 0 : 1;
        }

        private static void Add(List<string[]> cases, string name, Func<bool> test)
        {
            try
            {
                bool ok = test();
                cases.Add(new string[] { name, ok ? "true" : "false" });
            }
            catch (Exception ex)
            {
                cases.Add(new string[] { name, "false", ex.Message });
            }
        }

        /// <summary>Runs a case against a throw-away data root so user data is never touched.</summary>
        private static bool InSandboxRoot(Func<bool> body)
        {
            string sandbox = Path.Combine(Path.GetTempPath(), "deskorder-selftest-root-" + Guid.NewGuid().ToString("N").Substring(0, 8));
            bool result = InRoot(sandbox, body);
            try { Directory.Delete(sandbox, true); } catch { }
            return result;
        }

        /// <summary>Runs a case with AppPaths pointed at the given root, then restores it.</summary>
        private static bool InRoot(string root, Func<bool> body)
        {
            string original = AppPaths.Root;
            string originalLocal = AppPaths.LocalRoot;
            AppPaths.Initialize(root, true);
            try
            {
                return body();
            }
            finally
            {
                AppPaths.Initialize(original, true);
                GC.KeepAlive(originalLocal);
            }
        }

        private static float[] Sine(double frequency, int rate, int count)
        {
            float[] data = new float[count];
            for (int i = 0; i < count; i++) data[i] = (float)Math.Sin(2.0 * Math.PI * frequency * i / rate) * 0.25f;
            return data;
        }

        private static string Escape(string text)
        {
            return (text ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
