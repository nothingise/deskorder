using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DeskOrder.App;
using DeskOrder.Core;
using DeskOrder.SelfTest;
using DeskOrder.UI;
using DeskOrder.Windows;

namespace DeskOrder
{
    internal static class Program
    {
        private static Mutex _mutex;
        private static EventWaitHandle _activate;
        [STAThread]
        private static int Main(string[] args)
        {
            Dictionary<string, string> opts = Parse(args);
            string dataDir;
            opts.TryGetValue("data-dir", out dataDir);
            AppPaths.Initialize(dataDir, Has(opts, "data-strict"));
            Log.Initialize(AppPaths.LogsDir);
            Log.Info("data root=" + AppPaths.Root + " writable=" + AppPaths.Writable + " portable=" + AppPaths.Portable +
                     (AppPaths.IsFallback ? " requested=" + AppPaths.RequestedRoot : ""));

            // --desktop-dir points the desktop scan at a folder of your own; the public
            // desktop is then ignored as well, so screenshots and tests never show the
            // real machine's shortcuts.
            string desktopDir;
            if (opts.TryGetValue("desktop-dir", out desktopDir) && !string.IsNullOrWhiteSpace(desktopDir))
            {
                DesktopService.DesktopOverride = desktopDir;
                DesktopService.PublicDesktopOverride = Path.Combine(desktopDir, "public");
                Log.Info("desktop override=" + desktopDir);
            }

            if (Has(opts, "data-info"))
            {
                return DataInfo(Value(opts, "report"));
            }
            if (Has(opts, "dock-ui-test"))
            {
                string report = Value(opts, "dock-ui-test");
                if (string.IsNullOrEmpty(report)) report = Path.Combine(AppPaths.Root, "dock-ui-test.json");
                return DockUiTest.Run(report);
            }
            if (Has(opts, "dock-save-test"))
            {
                string report = Value(opts, "dock-save-test");
                if (string.IsNullOrEmpty(report)) report = Path.Combine(Path.GetTempPath(), "deskorder-dock-save-test.json");
                return DockSaveTest(report);
            }
            if (Has(opts, "version"))
            {
                Console.WriteLine("DeskOrder 1.0.0");
                return 0;
            }
            if (Has(opts, "capture-desktop"))
            {
                return CaptureDesktop(opts["capture-desktop"]);
            }
            if (Has(opts, "audio-test"))
            {
                string report = Value(opts, "audio-test");
                if (string.IsNullOrEmpty(report)) report = Path.Combine(AppPaths.Root, "audio-test.json");
                return AudioTest(report);
            }
            if (Has(opts, "probe-taskbar"))
            {
                int seconds = 6;
                int.TryParse(Value(opts, "probe-taskbar"), out seconds);
                string mode = Value(opts, "mode");
                return ProbeTaskbar(seconds, mode);
            }
            if (Has(opts, "screenshot-ui"))
            {
                string dir = Value(opts, "screenshot-ui");
                if (string.IsNullOrEmpty(dir)) dir = Path.Combine(AppPaths.Root, "screens");
                string size = Value(opts, "size");
                int width = 1240, height = 820;
                if (!string.IsNullOrEmpty(size))
                {
                    string[] parts = size.Split('x');
                    if (parts.Length == 2) { int.TryParse(parts[0], out width); int.TryParse(parts[1], out height); }
                }
                if (width < 1000) width = 1000;
                if (height < 680) height = 680;
                return ScreenshotUi(dir, width, height);
            }
            if (Has(opts, "probe-accent"))
            {
                return ProbeAccent();
            }
            if (Has(opts, "self-test"))
            {
                string path = Value(opts, "self-test");
                if (string.IsNullOrEmpty(path)) path = Path.Combine(AppPaths.Root, "self-test.json");
                return SelfTests.Run(path);
            }
            if (Has(opts, "player-test"))
            {
                string path = Value(opts, "player-test");
                if (string.IsNullOrEmpty(path)) path = Path.Combine(AppPaths.Root, "player-test.json");
                return PlayerTest(path);
            }
            if (Has(opts, "launch-test"))
            {
                return LaunchTest(opts["launch-test"], Value(opts, "report"), Has(opts, "keep-open"));
            }

            bool createdNew;
            _mutex = new Mutex(true, "Local\\DeskOrder.SingleInstance", out createdNew);
            if (!createdNew)
            {
                try
                {
                    EventWaitHandle handle = EventWaitHandle.OpenExisting("Local\\DeskOrder.Activate");
                    handle.Set();
                    handle.Dispose();
                }
                catch { }
                return 0;
            }
            _activate = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\DeskOrder.Activate");

            AppSettings settings = SettingsStore.Load();
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            app.DispatcherUnhandledException += delegate (object s, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
            {
                Log.Error("Unhandled UI exception", e.Exception);
                e.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += delegate (object s, UnhandledExceptionEventArgs e)
            {
                Log.Error("Unhandled domain exception", e.ExceptionObject as Exception);
            };

            var host = new DeskOrderApp(app, settings, false);
            StartupService.RefreshIfEnabled();
            host.Start(Has(opts, "tray"));
            ThreadPool.RegisterWaitForSingleObject(_activate, delegate
            {
                try { app.Dispatcher.BeginInvoke(new Action(delegate { host.ShowMain(0); })); } catch { }
            }, null, Timeout.Infinite, false);
            try { return app.Run(); }
            finally
            {
                GC.KeepAlive(_mutex);
                GC.KeepAlive(_activate);
            }
        }

        private static int DataInfo(string reportPath)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"requested\": \"").Append(Escape(AppPaths.RequestedRoot)).Append("\",\n");
            sb.Append("  \"root\": \"").Append(Escape(AppPaths.Root)).Append("\",\n");
            sb.Append("  \"localRoot\": \"").Append(Escape(AppPaths.LocalRoot)).Append("\",\n");
            sb.Append("  \"writable\": ").Append(AppPaths.Writable ? "true" : "false").Append(",\n");
            sb.Append("  \"portable\": ").Append(AppPaths.Portable ? "true" : "false").Append(",\n");
            sb.Append("  \"fallback\": ").Append(AppPaths.IsFallback ? "true" : "false").Append(",\n");
            sb.Append("  \"note\": \"").Append(Escape(AppPaths.FallbackNote)).Append("\",\n");
            sb.Append("  \"candidates\": [");
            List<string> candidates = AppPaths.Candidates(null, false);
            for (int i = 0; i < candidates.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append("\"").Append(Escape(candidates[i])).Append("\"");
            }
            sb.Append("]\n}\n");
            Console.WriteLine(sb.ToString());
            Log.Info("data-info root=" + AppPaths.Root + " writable=" + AppPaths.Writable + " fallback=" + AppPaths.IsFallback);
            if (!string.IsNullOrEmpty(reportPath))
            {
                string full = Path.GetFullPath(reportPath);
                string dir = Path.GetDirectoryName(full);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(full, sb.ToString(), new System.Text.UTF8Encoding(false));
            }
            return AppPaths.Writable ? 0 : 1;
        }

        /// <summary>
        /// Regression test for the bug where a read-only data directory made every Dock
        /// edit throw inside the click handler, so remove/move/add silently did nothing.
        /// </summary>
        private static int DockSaveTest(string reportPath)
        {
            string denied = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                                         "System32", "DeskOrderDenied-" + Guid.NewGuid().ToString("N").Substring(0, 6));
            AppPaths.Initialize(denied, true);
            Log.Initialize(AppPaths.LogsDir);

            var settings = new AppSettings();
            settings.General.FirstRun = false;
            var dock = new DockService(settings);
            int before = dock.Count;
            bool threw = false;
            string error = "";
            string pinPath = Path.Combine(Path.GetTempPath(), "deskorder-save-test-" + Guid.NewGuid().ToString("N").Substring(0, 6) + ".lnk");
            try
            {
                File.WriteAllText(pinPath, "x");
                DockPin pin = dock.Add(pinPath, false);
                if (pin != null)
                {
                    dock.Move(pin.Id, 1);
                    dock.Reorder(0, 0);
                    dock.SortByName();
                    dock.Remove(pin.Id, false);
                }
            }
            catch (Exception ex)
            {
                threw = true;
                error = ex.GetType().Name + ": " + ex.Message;
            }
            finally
            {
                try { File.Delete(pinPath); } catch { }
            }

            bool opsSurvived = !threw && dock.Count == before;
            bool errorReported = !string.IsNullOrEmpty(dock.LastSaveError);
            bool ok = opsSurvived && errorReported;

            var sb = new System.Text.StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"dataRoot\": \"").Append(Escape(AppPaths.Root)).Append("\",\n");
            sb.Append("  \"writable\": ").Append(AppPaths.Writable ? "true" : "false").Append(",\n");
            sb.Append("  \"threw\": ").Append(threw ? "true" : "false").Append(",\n");
            sb.Append("  \"exception\": \"").Append(Escape(error)).Append("\",\n");
            sb.Append("  \"opsSurvived\": ").Append(opsSurvived ? "true" : "false").Append(",\n");
            sb.Append("  \"saveErrorReported\": \"").Append(Escape(dock.LastSaveError)).Append("\",\n");
            sb.Append("  \"ok\": ").Append(ok ? "true" : "false").Append("\n}\n");
            Console.WriteLine(sb.ToString());
            Log.Info("dock-save-test ok=" + ok + " threw=" + threw + " err=" + dock.LastSaveError);
            if (!string.IsNullOrEmpty(reportPath))
            {
                string full = Path.GetFullPath(reportPath);
                string dir = Path.GetDirectoryName(full);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(full, sb.ToString(), new System.Text.UTF8Encoding(false));
            }
            return ok ? 0 : 1;
        }

        private static string Escape(string text)
        {
            return (text ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static int ProbeAccent()
        {
            try
            {
                var app = new Application();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var w = new Window();
                w.Width = 200;
                w.Height = 120;
                w.Left = -2000;
                w.Top = -2000;
                w.ShowInTaskbar = false;
                w.Show();
                IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(w).Handle;
                int[] states = new int[] { 2, 3, 4 };
                foreach (int state in states)
                {
                    bool ok = DeskOrder.Interop.Accent.Apply(hwnd, state, state == 2 ? 0 : 2, 0);
                    Log.Info("probe-accent state=" + state + " ok=" + ok);
                }
                DeskOrder.Interop.Accent.Disable(hwnd);
                w.Close();
                app.Shutdown();
                return 0;
            }
            catch (Exception ex) { Log.Error("probe-accent", ex); return 1; }
        }

        private static int LaunchTest(string target, string reportPath, bool keepOpen)
        {
            string resolved = "";
            string error = "";
            int before = 0;
            int after = 0;
            bool shortcut = !string.IsNullOrEmpty(target) &&
                            (target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ||
                             target.EndsWith(".url", StringComparison.OrdinalIgnoreCase));
            try
            {
                var dock = new DockService(new AppSettings());
                var pin = new DockPin();
                pin.Id = "launch-test";
                pin.Path = target;
                pin.DisplayName = Path.GetFileNameWithoutExtension(target);
                if (shortcut)
                {
                    try { resolved = DeskOrder.Interop.ShortcutResolver.ResolveTargetPath(target); } catch { }
                }
                else resolved = target;
                string baseName = string.IsNullOrEmpty(resolved) ? "" : Path.GetFileNameWithoutExtension(resolved);
                before = CountProcesses(baseName);
                dock.Launch(pin);
                Thread.Sleep(2500);
                after = CountProcesses(baseName);
                if (keepOpen)
                {
                    Log.Info("launch-test keep-open, sleeping");
                    Thread.Sleep(4000);
                }
            }
            catch (Exception ex)
            {
                error = ex.Message;
                Log.Error("launch-test failed", ex);
            }
            bool ok = string.IsNullOrEmpty(error);
            var sb = new System.Text.StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"target\": \"").Append((target ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\",\n");
            sb.Append("  \"shortcut\": ").Append(shortcut ? "true" : "false").Append(",\n");
            sb.Append("  \"resolved\": \"").Append((resolved ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\",\n");
            sb.Append("  \"before\": ").Append(before).Append(",\n");
            sb.Append("  \"after\": ").Append(after).Append(",\n");
            sb.Append("  \"error\": \"").Append((error ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\",\n");
            sb.Append("  \"ok\": ").Append(ok ? "true" : "false").Append("\n}\n");
            if (!string.IsNullOrEmpty(reportPath))
            {
                string full = Path.GetFullPath(reportPath);
                Directory.CreateDirectory(Path.GetDirectoryName(full));
                File.WriteAllText(full, sb.ToString(), new System.Text.UTF8Encoding(false));
                Log.Info("launch-test report written " + full);
            }
            Log.Info("launch-test ok=" + ok + " shortcut=" + shortcut + " resolved=" + resolved + " before=" + before + " after=" + after + " err=" + error);
            Console.WriteLine(sb.ToString());
            return ok ? 0 : 1;
        }

        private static int CountProcesses(string baseName)
        {
            if (string.IsNullOrEmpty(baseName)) return -1;
            int n = 0;
            try
            {
                foreach (System.Diagnostics.Process p in System.Diagnostics.Process.GetProcesses())
                {
                    try { if (string.Equals(p.ProcessName, baseName, StringComparison.OrdinalIgnoreCase)) n++; }
                    catch { }
                }
            }
            catch { }
            return n;
        }

        private static int PlayerTest(string reportPath)
        {
            string wav = Path.Combine(AppPaths.Root, "player-test-tone.wav");
            MakeTone(wav);
            try { if (File.Exists(AppPaths.PlaylistFile)) File.Delete(AppPaths.PlaylistFile); } catch { }
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var settings = new AppSettings();
            settings.Music.Volume = 0.35;
            var player = new PlayerService(settings, app.Dispatcher);
            int added = player.AddFiles(new string[] { wav });
            player.PlayIndex(0);
            bool sawPlaying = false;
            double maxPosition = 0;
            DateTime start = DateTime.UtcNow;
            var frame = new DispatcherFrame();
            var timer = new DispatcherTimer(DispatcherPriority.Background, app.Dispatcher);
            timer.Interval = TimeSpan.FromMilliseconds(180);
            timer.Tick += delegate
            {
                if (player.Position > maxPosition) maxPosition = player.Position;
                if (player.IsPlaying) sawPlaying = true;
                if ((DateTime.UtcNow - start).TotalSeconds > 3.2 || maxPosition > 0.35)
                {
                    timer.Stop();
                    frame.Continue = false;
                }
            };
            timer.Start();
            Dispatcher.PushFrame(frame);
            timer.Stop();
            bool ok = player.Tracks.Count > 0 && sawPlaying && maxPosition > 0.05;
            var sb = new System.Text.StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"added\": ").Append(added).Append(",\n");
            sb.Append("  \"sawPlaying\": ").Append(sawPlaying ? "true" : "false").Append(",\n");
            sb.Append("  \"maxPosition\": ").Append(maxPosition.ToString("F3")).Append(",\n");
            sb.Append("  \"duration\": ").Append(player.Duration.ToString("F3")).Append(",\n");
            sb.Append("  \"error\": \"").Append((player.LastError ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"")).Append("\",\n");
            sb.Append("  \"ok\": ").Append(ok ? "true" : "false").Append("\n}\n");
            string full = Path.GetFullPath(reportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, sb.ToString(), new System.Text.UTF8Encoding(false));
            Log.Info("player-test ok=" + ok + " added=" + added + " playing=" + sawPlaying + " pos=" + maxPosition.ToString("F3") + " err=" + player.LastError);
            player.Dispose();
            try { File.Delete(wav); } catch { }
            app.Shutdown();
            return ok ? 0 : 1;
        }

        private static int ScreenshotUi(string dir, int width, int height)
        {
            try
            {
                Directory.CreateDirectory(dir);
                var app = new Application();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                AppSettings settings = SettingsStore.Load();
                var host = new DeskOrderApp(app, settings, true);
                host.Start(true);
                if (host.Dock.Count == 0) host.Dock.SeedFromDesktop();
                settings.Window.Maximized = false;

                var window = new MainWindow(host);
                window.WindowState = WindowState.Normal;
                string[] names = new string[] { "overview", "dock", "music", "taskbar", "settings" };
                for (int i = 0; i < names.Length; i++)
                {
                    window.Navigate(i);
                    if (window.CurrentPage != null)
                    {
                        window.CurrentPage.BeginAnimation(UIElement.OpacityProperty, null);
                        window.CurrentPage.Opacity = 1;
                    }
                    var root = window.Content as FrameworkElement;
                    if (root == null) continue;
                    root.Measure(new System.Windows.Size(width, height));
                    root.Arrange(new Rect(0, 0, width, height));
                    root.UpdateLayout();
                    RenderToPng(root, width, height, Path.Combine(dir, names[i] + ".png"));
                }
                var widget = new MusicWidget(host);
                var wc = widget.Content as FrameworkElement;
                if (wc != null)
                {
                    wc.Measure(new System.Windows.Size(424, 272));
                    wc.Arrange(new Rect(0, 0, 424, 272));
                    wc.UpdateLayout();
                    RenderToPng(wc, 424, 272, Path.Combine(dir, "music-widget.png"));
                }
                var dock = new DockWindow(host);
                dock.RefreshPins();
                var dc = dock.Content as FrameworkElement;
                if (dc != null && dock.Width > 100 && dock.Height > 50)
                {
                    var grid = dc as Grid;
                    if (grid != null)
                    {
                        grid.Background = new System.Windows.Media.LinearGradientBrush(
                            System.Windows.Media.Color.FromRgb(26, 38, 70),
                            System.Windows.Media.Color.FromRgb(13, 47, 51),
                            new System.Windows.Point(0, 0), new System.Windows.Point(1, 1));
                    }
                    int dw = (int)Math.Round(dock.Width);
                    int dh = (int)Math.Round(dock.Height);
                    dc.Measure(new System.Windows.Size(dw, dh));
                    dc.Arrange(new Rect(0, 0, dw, dh));
                    dc.UpdateLayout();
                    RenderToPng(dc, dw, dh, Path.Combine(dir, "dock-window.png"));
                }
                Log.Info("screenshot-ui written to " + dir);
                host.Player.Dispose();
                host.Audio.Dispose();
                host.Taskbar.Dispose();
                app.Shutdown();
                return 0;
            }
            catch (Exception ex)
            {
                Log.Error("screenshot-ui failed", ex);
                return 1;
            }
        }

        private static void RenderToPng(FrameworkElement element, int width, int height, string path)
        {
            var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(width, height, 96, 96, System.Windows.Media.PixelFormats.Pbgra32);
            rtb.Render(element);
            var enc = new System.Windows.Media.Imaging.PngBitmapEncoder();
            enc.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(rtb));
            using (var fs = new FileStream(path, FileMode.Create)) enc.Save(fs);
        }

        private static int AudioTest(string reportPath)
        {
            string wav = Path.Combine(AppPaths.Root, "audio-test-tone.wav");
            MakeTone(wav);
            float maxLevel = 0, maxBass = 0, maxBeat = 0, expectedBandMax = 0;
            var engine = new AudioEngine();
            engine.Start();
            Thread.Sleep(600);
            int expected = Fft.BandOf(440, AudioEngine.Bands);
            float baselineBand = 0;
            DateTime baselineUntil = DateTime.UtcNow.AddMilliseconds(450);
            while (DateTime.UtcNow < baselineUntil)
            {
                float[] baseSpec = engine.GetSpectrum();
                for (int i = Math.Max(0, expected - 1); i <= Math.Min(baseSpec.Length - 1, expected + 1); i++)
                    if (baseSpec[i] > baselineBand) baselineBand = baseSpec[i];
                Thread.Sleep(25);
            }
            var player = new Thread(delegate ()
            {
                try { using (var sp = new System.Media.SoundPlayer(wav)) { sp.Play(); Thread.Sleep(1800); } }
                catch (Exception ex) { Log.Warn("test tone failed: " + ex.Message); }
            });
            player.IsBackground = true;
            player.Start();
            DateTime until = DateTime.UtcNow.AddSeconds(2.2);
            while (DateTime.UtcNow < until)
            {
                float[] live = engine.GetSpectrum();
                for (int i = Math.Max(0, expected - 1); i <= Math.Min(live.Length - 1, expected + 1); i++)
                    if (live[i] > expectedBandMax) expectedBandMax = live[i];
                if (engine.Level > maxLevel) maxLevel = engine.Level;
                if (engine.Bass > maxBass) maxBass = engine.Bass;
                if (engine.Beat > maxBeat) maxBeat = engine.Beat;
                Thread.Sleep(35);
            }
            float[] snapshot = engine.GetSpectrum();
            engine.Stop();
            int dominant = 0;
            for (int i = 1; i < snapshot.Length; i++) if (snapshot[i] > snapshot[dominant]) dominant = i;
            int sineBand = Fft.DominantBand(Sine(440, 48000, 2048), 48000, AudioEngine.Bands);
            bool fftOk = Math.Abs(sineBand - expected) <= 1;
            bool captureOk = expectedBandMax > 0.03f && maxLevel > 0.0005f && (expectedBandMax > baselineBand + 0.01f || expectedBandMax > 0.15f);
            var report = new System.Text.StringBuilder();
            report.Append("{\r\n");
            report.Append("  \"expectedBand\": " + expected + ",\r\n");
            report.Append("  \"dominantBand\": " + dominant + ",\r\n");
            report.Append("  \"maxLevel\": " + maxLevel.ToString("F6") + ",\r\n");
            report.Append("  \"maxBass\": " + maxBass.ToString("F4") + ",\r\n");
            report.Append("  \"maxBeat\": " + maxBeat.ToString("F4") + ",\r\n");
            report.Append("  \"baselineBand\": " + baselineBand.ToString("F4") + ",\r\n");
            report.Append("  \"expectedBandMax\": " + expectedBandMax.ToString("F4") + ",\r\n");
            report.Append("  \"fftSineOk\": " + (fftOk ? "true" : "false") + ",\r\n");
            report.Append("  \"captureOk\": " + (captureOk ? "true" : "false") + ",\r\n");
            report.Append("  \"device\": \"" + (engine.DeviceName ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\",\r\n");
            report.Append("  \"error\": \"" + (engine.LastError ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"\r\n");
            report.Append("}\r\n");
            string full = Path.GetFullPath(reportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full));
            File.WriteAllText(full, report.ToString(), new System.Text.UTF8Encoding(false));
            Log.Info("audio-test fft=" + fftOk + " capture=" + captureOk + " expected=" + expected + " dominant=" + dominant + " level=" + maxLevel.ToString("F5"));
            try { File.Delete(wav); } catch { }
            return fftOk && captureOk ? 0 : 1;
        }

        private static float[] Sine(double frequency, int rate, int count)
        {
            float[] data = new float[count];
            for (int i = 0; i < count; i++) data[i] = (float)Math.Sin(2.0 * Math.PI * frequency * i / rate) * 0.25f;
            return data;
        }

        private static void MakeTone(string path)
        {
            int rate = 48000, seconds = 1;
            short amp = 1600;
            using (var fs = new FileStream(path, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                int data = seconds * rate * 2 * 2;
                bw.Write(new char[] { 'R', 'I', 'F', 'F' }); bw.Write(36 + data); bw.Write(new char[] { 'W', 'A', 'V', 'E' });
                bw.Write(new char[] { 'f', 'm', 't', ' ' }); bw.Write(16); bw.Write((short)1); bw.Write((short)2); bw.Write(rate);
                bw.Write(rate * 2 * 2); bw.Write((short)4); bw.Write((short)16);
                bw.Write(new char[] { 'd', 'a', 't', 'a' }); bw.Write(data);
                for (int i = 0; i < rate; i++)
                {
                    short v = (short)(Math.Sin(2 * Math.PI * 440 * i / rate) * amp);
                    bw.Write(v); bw.Write(v);
                }
            }
        }

        private static int ProbeTaskbar(int seconds, string mode)
        {
            AppSettings settings = SettingsStore.Load();
            settings.Taskbar.Enabled = true;
            settings.Taskbar.Mode = string.IsNullOrEmpty(mode) ? "clear" : mode;
            var service = new TaskbarService(settings);
            bool ok = service.Apply();
            Log.Info("probe-taskbar applied=" + ok + " mode=" + settings.Taskbar.Mode + " seconds=" + seconds + " error=" + service.LastError);
            Thread.Sleep(Math.Max(1, seconds) * 1000);
            service.Disable();
            Log.Info("probe-taskbar restored");
            return ok ? 0 : 1;
        }

        private static int CaptureDesktop(string path)
        {
            try
            {
                Rectangle bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
                using (Bitmap bmp = new Bitmap(bounds.Width, bounds.Height))
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.CopyFromScreen(bounds.Location, System.Drawing.Point.Empty, bounds.Size);
                    string full = Path.GetFullPath(path);
                    string dir = Path.GetDirectoryName(full);
                    if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                    bmp.Save(full, ImageFormat.Png);
                }
                Log.Info("capture-desktop " + path);
                return 0;
            }
            catch (Exception ex)
            {
                Log.Error("capture-desktop failed", ex);
                return 1;
            }
        }

        private static Dictionary<string, string> Parse(string[] args)
        {
            var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (!a.StartsWith("--")) continue;
                string key = a.Substring(2);
                string value = "";
                int eq = key.IndexOf('=');
                if (eq >= 0)
                {
                    value = key.Substring(eq + 1);
                    key = key.Substring(0, eq);
                }
                else if (i + 1 < args.Length && !args[i + 1].StartsWith("--"))
                {
                    value = args[++i];
                }
                d[key] = value;
            }
            return d;
        }

        private static bool Has(Dictionary<string, string> opts, string key) { return opts.ContainsKey(key); }

        private static string Value(Dictionary<string, string> opts, string key)
        {
            string v;
            return opts.TryGetValue(key, out v) ? v : "";
        }
    }
}
