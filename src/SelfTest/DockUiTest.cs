using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DeskOrder.App;
using DeskOrder.Core;
using DeskOrder.UI;
using DeskOrder.Windows;

namespace DeskOrder.SelfTest
{
    /// <summary>
    /// Drives the real "Dock 整理" page with synthetic clicks (no physical mouse needed):
    /// move up/down, remove, add, drag-reorder and the drop-effect rules.
    /// </summary>
    public static class DockUiTest
    {
        private static readonly List<string[]> Results = new List<string[]>();

        public static int Run(string reportPath)
        {
            Results.Clear();
            var app = new Application();
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            string orderJson = "";

            // This test replaces the Dock contents to have a known list to click through.
            // Snapshots the data directory first so running it against a real profile
            // cannot destroy the user's pins (which is exactly what it used to do).
            Dictionary<string, byte[]> snapshot = SnapshotJson(AppPaths.Root);
            string testDir = Path.Combine(AppPaths.Root, "dock-ui-test");
            bool testDirExisted = Directory.Exists(testDir);
            try
            {
                AppSettings settings = SettingsStore.Load();
                settings.General.FirstRun = false;
                settings.Dock.AutoHide = false;
                var host = new DeskOrderApp(app, settings, true);
                host.Start(true);

                foreach (DockPin p in host.Dock.Pins) host.Dock.Remove(p.Id, false);

                string dir = testDir;
                Directory.CreateDirectory(dir);
                string alpha = MakeFile(dir, "DockTest Alpha.txt");
                string bravo = MakeFile(dir, "DockTest Bravo.txt");
                string charlie = MakeFile(dir, "DockTest Charlie.txt");
                host.Dock.Add(alpha, false);
                host.Dock.Add(bravo, false);
                host.Dock.Add(charlie, false);
                Check("setup-three-pins", host.Dock.Count == 3, "count=" + host.Dock.Count);

                var window = new MainWindow(host);
                window.Navigate(1);
                Layout(window);
                var page = window.CurrentPage as DockPage;
                Check("page-is-dock-page", page != null, page == null ? "null" : page.Title);
                if (page == null) throw new Exception("DockPage not created");

                Check("page-has-add-buttons", FindButton(page, "添加文件…") != null && FindButton(page, "按名称整理") != null, "");

                List<Border> up, down, remove;
                FindRowButtons(page, out up, out down, out remove);
                Check("page-row-buttons", up.Count == 3 && down.Count == 3 && remove.Count == 3,
                    "up=" + up.Count + " down=" + down.Count + " remove=" + remove.Count);

                Check("page-row-order-initial", Same(Names(page), new string[] { "DockTest Alpha", "DockTest Bravo", "DockTest Charlie" }), Join(Names(page)));

                // 1. move Alpha down one slot through the page button
                Click(down[0]);
                Layout(window);
                Check("page-move-down", Same(Names(host.Dock.Pins), new string[] { "DockTest Bravo", "DockTest Alpha", "DockTest Charlie" }), Join(Names(host.Dock.Pins)));
                Check("page-move-down-refreshed-ui", Same(Names(page), new string[] { "DockTest Bravo", "DockTest Alpha", "DockTest Charlie" }), Join(Names(page)));

                // 2. move Charlie up one slot
                FindRowButtons(page, out up, out down, out remove);
                Click(up[2]);
                Layout(window);
                Check("page-move-up", Same(Names(host.Dock.Pins), new string[] { "DockTest Bravo", "DockTest Charlie", "DockTest Alpha" }), Join(Names(host.Dock.Pins)));

                // 3. remove the middle pin through the page button
                FindRowButtons(page, out up, out down, out remove);
                Click(remove[1]);
                Layout(window);
                Check("page-remove", host.Dock.Count == 2 && Same(Names(host.Dock.Pins), new string[] { "DockTest Bravo", "DockTest Alpha" }), Join(Names(host.Dock.Pins)));
                Check("page-remove-refreshed-ui", Same(Names(page), new string[] { "DockTest Bravo", "DockTest Alpha" }), Join(Names(page)));

                // 4. add again (same code path as the "添加文件…" button)
                page.AddPaths(new string[] { charlie });
                Layout(window);
                Check("page-add", host.Dock.Count == 3 && Contains(Names(host.Dock.Pins), "DockTest Charlie"), Join(Names(host.Dock.Pins)));
                Check("page-add-refreshed-ui", Contains(Names(page), "DockTest Charlie"), Join(Names(page)));

                // 5. moving into the last slot must still refresh (edge case)
                FindRowButtons(page, out up, out down, out remove);
                Click(down[1]);
                Layout(window);
                Check("page-move-to-last-slot", Same(Names(host.Dock.Pins), new string[] { "DockTest Bravo", "DockTest Charlie", "DockTest Alpha" }), Join(Names(host.Dock.Pins)));
                Check("page-move-to-last-refreshed-ui", Same(Names(page), new string[] { "DockTest Bravo", "DockTest Charlie", "DockTest Alpha" }), Join(Names(page)));

                // 6. duplicates are rejected
                int before = host.Dock.Count;
                page.AddPaths(new string[] { charlie, Path.Combine(dir, "missing-file.txt") });
                Check("page-add-duplicate-and-missing", host.Dock.Count == before, "before=" + before + " after=" + host.Dock.Count);

                // 6. everything above must be persisted
                DockPinsFile file = Json.Read<DockPinsFile>(AppPaths.PinsFile);
                bool persisted = file != null && file.Pins != null && file.Pins.Count == host.Dock.Count;
                if (persisted)
                {
                    for (int i = 0; i < file.Pins.Count; i++)
                    {
                        if (file.Pins[i].Id != host.Dock.Pins[i].Id) persisted = false;
                    }
                }
                Check("pins-persisted-in-order", persisted, "save=" + host.Dock.LastSaveError);
                Check("no-save-error", string.IsNullOrEmpty(host.Dock.LastSaveError), host.Dock.LastSaveError);

                // 7. drag-reorder inside the dock window (what a dragged icon does)
                var dockWin = new DockWindow(host);
                bool effectPin = DockWindow.DropEffectFor(new DataObject(DockWindow.PinFormat, "x")) == DragDropEffects.Move;
                bool effectFile = DockWindow.DropEffectFor(new DataObject(DataFormats.FileDrop, new string[] { alpha })) == DragDropEffects.Copy;
                bool effectNone = DockWindow.DropEffectFor(new DataObject()) == DragDropEffects.None;
                Check("dock-drop-effect-rules", effectPin && effectFile && effectNone, "pin=" + effectPin + " file=" + effectFile + " none=" + effectNone);

                List<DockPin> pins = host.Dock.Pins;
                string lastId = pins[pins.Count - 1].Id;
                string lastName = pins[pins.Count - 1].DisplayName;
                bool moved = dockWin.HandlePinDrop(lastId, 0);
                Check("dock-drag-reorder", moved && host.Dock.Pins[0].DisplayName == lastName, Join(Names(host.Dock.Pins)));

                // 8. dropping files onto the dock adds them
                string dropped = MakeFile(dir, "DockTest Dropped.txt");
                int added = dockWin.HandleFileDrop(new string[] { dropped });
                Check("dock-file-drop-adds", added == 1 && Contains(Names(host.Dock.Pins), "DockTest Dropped"), Join(Names(host.Dock.Pins)));

                // 9. the empty-space menu offers the add / tidy actions
                ContextMenu menu = dockWin.BuildDockMenu();
                var headers = new List<string>();
                foreach (object item in menu.Items)
                {
                    var mi = item as MenuItem;
                    if (mi != null) headers.Add(Convert.ToString(mi.Header));
                }
                Check("dock-menu-add-entries",
                    Contains(headers, "添加文件…") && Contains(headers, "添加文件夹…") && Contains(headers, "按名称整理"),
                    Join(headers));

                // 10. tidy up: alphabetical
                host.Dock.SortByName();
                List<string> sorted = Names(host.Dock.Pins);
                bool isSorted = true;
                for (int i = 1; i < sorted.Count; i++)
                {
                    if (string.Compare(sorted[i - 1], sorted[i], StringComparison.CurrentCultureIgnoreCase) > 0) isSorted = false;
                }
                Check("dock-sort-by-name", isSorted, Join(sorted));

                orderJson = Join(Names(host.Dock.Pins));
                host.Player.Dispose();
                host.Audio.Dispose();
                host.Taskbar.Dispose();
            }
            catch (Exception ex)
            {
                Check("harness", false, ex.GetType().Name + ": " + ex.Message);
                Log.Error("dock-ui-test harness failed", ex);
            }
            finally
            {
                RestoreSnapshot(snapshot);
                if (!testDirExisted)
                {
                    try { if (Directory.Exists(testDir)) Directory.Delete(testDir, true); } catch { }
                }
            }

            int failed = 0;
            var sb = new StringBuilder();
            sb.Append("{\n  \"dataRoot\": \"").Append(Escape(AppPaths.Root)).Append("\",\n");
            sb.Append("  \"finalOrder\": \"").Append(Escape(orderJson)).Append("\",\n");
            sb.Append("  \"cases\": [\n");
            bool first = true;
            foreach (string[] r in Results)
            {
                if (r[1] != "true") failed++;
                if (!first) sb.Append(",\n");
                first = false;
                sb.Append("    { \"name\": \"").Append(Escape(r[0])).Append("\", \"ok\": ").Append(r[1]);
                if (r.Length > 2 && !string.IsNullOrEmpty(r[2])) sb.Append(", \"detail\": \"").Append(Escape(r[2])).Append("\"");
                sb.Append(" }");
            }
            sb.Append("\n  ],\n  \"failed\": ").Append(failed).Append("\n}\n");

            string full = Path.GetFullPath(reportPath);
            string parent = Path.GetDirectoryName(full);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
            File.WriteAllText(full, sb.ToString(), new UTF8Encoding(false));
            Log.Info("dock-ui-test failed=" + failed + " cases=" + Results.Count + " report=" + full);
            Console.WriteLine(sb.ToString());
            app.Shutdown();
            return failed == 0 ? 0 : 1;
        }

        private static string MakeFile(string dir, string name)
        {
            string path = Path.Combine(dir, name);
            File.WriteAllText(path, "dock test");
            return path;
        }

        /// <summary>Reads every *.json directly under the data root into memory.</summary>
        private static Dictionary<string, byte[]> SnapshotJson(string root)
        {
            var snapshot = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            try
            {
                if (!Directory.Exists(root)) return snapshot;
                foreach (string file in Directory.GetFiles(root, "*.json")) snapshot[file] = File.ReadAllBytes(file);
            }
            catch (Exception ex) { Log.Warn("dock-ui-test snapshot: " + ex.Message); }
            return snapshot;
        }

        private static void RestoreSnapshot(Dictionary<string, byte[]> snapshot)
        {
            try
            {
                foreach (KeyValuePair<string, byte[]> entry in snapshot)
                {
                    try { File.WriteAllBytes(entry.Key, entry.Value); }
                    catch (Exception ex) { Log.Error("dock-ui-test restore " + entry.Key, ex); }
                }
                // A file that did not exist before the test must not survive it either.
                foreach (string file in Directory.GetFiles(AppPaths.Root, "*.json"))
                {
                    if (!snapshot.ContainsKey(file) && Path.GetFileName(file) == "dock-ui-test.json")
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
                Log.Info("dock-ui-test restored " + snapshot.Count + " data file(s)");
            }
            catch (Exception ex) { Log.Error("dock-ui-test restore", ex); }
        }

        private static void Layout(Window window)
        {
            var root = window.Content as FrameworkElement;
            if (root == null) return;
            root.Measure(new Size(1240, 860));
            root.Arrange(new Rect(0, 0, 1240, 860));
            root.UpdateLayout();
        }

        private static void Click(UIElement element)
        {
            if (element == null) return;
            var args = new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left);
            args.RoutedEvent = UIElement.MouseLeftButtonUpEvent;
            element.RaiseEvent(args);
        }

        /// <summary>Row buttons in tree order: up/down/remove per pin row.</summary>
        private static void FindRowButtons(DependencyObject root, out List<Border> up, out List<Border> down, out List<Border> remove)
        {
            var all = new List<Border>();
            CollectGlyphButtons(root, all);
            up = all.FindAll(delegate (Border b) { return Glyph(b) == "↑"; });
            down = all.FindAll(delegate (Border b) { return Glyph(b) == "↓"; });
            remove = all.FindAll(delegate (Border b) { return Glyph(b) == "×"; });
        }

        private static void CollectGlyphButtons(DependencyObject node, List<Border> into)
        {
            if (node == null) return;
            var border = node as Border;
            if (border != null)
            {
                string g = Glyph(border);
                if (g == "↑" || g == "↓" || g == "×" || g == "↩") into.Add(border);
            }
            int count = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < count; i++) CollectGlyphButtons(VisualTreeHelper.GetChild(node, i), into);
        }

        private static string Glyph(Border border)
        {
            if (border == null) return "";
            var t = border.Child as TextBlock;
            return t != null ? t.Text : "";
        }

        /// <summary>Finds a real button (hand cursor) whose label is the given text.</summary>
        private static Border FindButton(DependencyObject root, string text)
        {
            if (root == null) return null;
            var border = root as Border;
            if (border != null && border.Cursor == Cursors.Hand && HasText(border.Child, text)) return border;
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                Border found = FindButton(VisualTreeHelper.GetChild(root, i), text);
                if (found != null) return found;
            }
            return null;
        }

        private static bool HasText(DependencyObject node, string text)
        {
            if (node == null) return false;
            var t = node as TextBlock;
            if (t != null) return t.Text == text;
            int count = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < count; i++) if (HasText(VisualTreeHelper.GetChild(node, i), text)) return true;
            return false;
        }

        private static List<string> Names(DockPage page)
        {
            var wanted = new List<string>();
            foreach (DockPin p in DeskOrderApp.Current.Dock.Pins) wanted.Add(p.DisplayName);
            return NamesInTree(page, wanted);
        }

        private static List<string> Names(List<DockPin> pins)
        {
            var list = new List<string>();
            foreach (DockPin p in pins) list.Add(p.DisplayName);
            return list;
        }

        /// <summary>Visual order of the pin name labels inside the page.</summary>
        private static List<string> NamesInTree(DependencyObject root, List<string> candidates)
        {
            var found = new List<string>();
            WalkText(root, candidates, found);
            return found;
        }

        private static void WalkText(DependencyObject node, List<string> candidates, List<string> found)
        {
            if (node == null) return;
            var t = node as TextBlock;
            if (t != null && candidates.Contains(t.Text) && !found.Contains(t.Text)) found.Add(t.Text);
            int count = VisualTreeHelper.GetChildrenCount(node);
            for (int i = 0; i < count; i++) WalkText(VisualTreeHelper.GetChild(node, i), candidates, found);
        }

        private static bool Same(List<string> a, string[] b)
        {
            if (a.Count != b.Length) return false;
            for (int i = 0; i < b.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static bool Contains(List<string> list, string value)
        {
            foreach (string s in list) if (s == value) return true;
            return false;
        }

        private static string Join(List<string> list)
        {
            var sb = new StringBuilder();
            foreach (string s in list)
            {
                if (sb.Length > 0) sb.Append(" | ");
                sb.Append(s);
            }
            return sb.ToString();
        }

        private static void Check(string name, bool ok, string detail)
        {
            Results.Add(new string[] { name, ok ? "true" : "false", detail });
            Log.Info("dock-ui-test " + (ok ? "PASS " : "FAIL ") + name + (string.IsNullOrEmpty(detail) ? "" : " :: " + detail));
        }

        private static string Escape(string text)
        {
            return (text ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
