using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using DeskOrder.Interop;

namespace DeskOrder.Core
{
    public class DockPin
    {
        public string Id { get; set; }
        public string Path { get; set; }
        public string DisplayName { get; set; }
        public string Kind { get; set; }
        public string CustomIcon { get; set; }
        public bool Managed { get; set; }
        public string OriginPath { get; set; }
        public string AddedAt { get; set; }

        public DockPin()
        {
            Id = "";
            Path = "";
            DisplayName = "";
            Kind = "app";
            CustomIcon = "";
            Managed = false;
            OriginPath = "";
            AddedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
    }

    public class DockPinsFile
    {
        public List<DockPin> Pins { get; set; }
        public DockPinsFile() { Pins = new List<DockPin>(); }
    }


    public class ImportRecord
    {
        public string BatchId { get; set; }
        public string PinId { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string At { get; set; }
        public ImportRecord() { BatchId = ""; PinId = ""; From = ""; To = ""; At = ""; }
    }

    public class ImportsFile
    {
        public List<ImportRecord> Records { get; set; }
        public ImportsFile() { Records = new List<ImportRecord>(); }
    }
    public class DockService
    {
        private readonly AppSettings _settings;
        private List<DockPin> _pins = new List<DockPin>();
        private ImportsFile _imports = new ImportsFile();
        private readonly object _gate = new object();

        public event EventHandler Changed;
        public List<DockPin> Pins { get { lock (_gate) return new List<DockPin>(_pins); } }
        public int Count { get { lock (_gate) return _pins.Count; } }
        /// <summary>Non-empty when the last change could not be written to disk.</summary>
        public string LastSaveError { get; private set; }

        public DockService(AppSettings settings)
        {
            _settings = settings;
            LastSaveError = "";
            LoadImports();
            Load();
        }

        public void Load()
        {
            DockPinsFile file = Json.Read<DockPinsFile>(AppPaths.PinsFile);
            lock (_gate)
            {
                _pins = file != null && file.Pins != null ? file.Pins : new List<DockPin>();
            }
            if (_pins.Count == 0 && _settings.General.FirstRun)
            {
                SeedFromDesktop();
            }
        }

        private void Save()
        {
            var file = new DockPinsFile();
            lock (_gate) file.Pins = new List<DockPin>(_pins);
            Json.Write(AppPaths.PinsFile, file);
        }

        /// <summary>
        /// Persists but never throws: a read-only profile or restricted token must not
        /// abort the edit, otherwise clicking a Dock button looks like it does nothing.
        /// </summary>
        private bool TrySave(string what)
        {
            try
            {
                Save();
                LastSaveError = "";
                return true;
            }
            catch (Exception ex)
            {
                LastSaveError = ex.GetType().Name + ": " + ex.Message;
                Log.Error("Cannot save " + what + " to " + AppPaths.Root, ex);
                return false;
            }
        }

        private void RaiseChanged()
        {
            TrySave("dock pins");
            EventHandler h = Changed;
            if (h != null) h(this, EventArgs.Empty);
        }

        public void SeedFromDesktop()
        {
            try
            {
                var candidates = DesktopService.EnumerateShortcuts();
                int added = 0;
                lock (_gate)
                {
                    foreach (DesktopItem item in candidates)
                    {
                        if (added >= 8) break;
                        if (_pins.Any(delegate (DockPin p) { return string.Equals(p.Path, item.Path, StringComparison.OrdinalIgnoreCase); })) continue;
                        _pins.Add(CreatePin(item.Path, item.DisplayName, false, item.IsPublic));
                        added++;
                    }
                }
                if (added > 0)
                {
                    Log.Info("Seeded dock with " + added + " desktop shortcuts");
                    RaiseChanged();
                }
            }
            catch (Exception ex) { Log.Error("SeedFromDesktop", ex); }
        }

        public DockPin Add(string path, bool moveIfShortcut)
        {
            if (string.IsNullOrWhiteSpace(path)) return null;
            string full;
            try { full = System.IO.Path.GetFullPath(path); }
            catch (Exception ex) { Log.Warn("Dock add rejected " + path + ": " + ex.Message); return null; }
            if (!File.Exists(full) && !Directory.Exists(full)) return null;
            lock (_gate)
            {
                DockPin existing = _pins.FirstOrDefault(delegate (DockPin p) { return string.Equals(p.Path, full, StringComparison.OrdinalIgnoreCase); });
                if (existing != null) return existing;
            }

            string finalPath = full;
            bool managed = false;
            string origin = "";
            if (moveIfShortcut && _settings.Dock.MoveOnDrop && IsShortcut(full) && DesktopService.IsOnUserDesktop(full))
            {
                string moved = MoveIntoDockItems(full);
                if (!string.IsNullOrEmpty(moved))
                {
                    origin = full;
                    finalPath = moved;
                    managed = true;
                }
            }

            DockPin pin = CreatePin(finalPath, System.IO.Path.GetFileNameWithoutExtension(finalPath), managed, false);
            pin.OriginPath = origin;
            lock (_gate) _pins.Add(pin);
            RaiseChanged();
            return pin;
        }

        private DockPin CreatePin(string path, string name, bool managed, bool isPublic)
        {
            var pin = new DockPin();
            pin.Id = Guid.NewGuid().ToString("N");
            pin.Path = path;
            pin.DisplayName = string.IsNullOrEmpty(name) ? System.IO.Path.GetFileNameWithoutExtension(path) : name;
            pin.Kind = Directory.Exists(path) ? "folder" : (path.EndsWith(".url", StringComparison.OrdinalIgnoreCase) ? "url" : "app");
            pin.Managed = managed;
            pin.OriginPath = managed ? path : "";
            return pin;
        }

        private static bool IsShortcut(string path)
        {
            return path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".url", StringComparison.OrdinalIgnoreCase);
        }

        private string MoveIntoDockItems(string source)
        {
            try
            {
                string name = System.IO.Path.GetFileName(source);
                string target = System.IO.Path.Combine(AppPaths.DockItemsDir, name);
                target = UniquePath(target);
                File.Move(source, target);
                Log.Info("Moved shortcut into dock: " + source + " -> " + target);
                return target;
            }
            catch (Exception ex)
            {
                Log.Error("MoveIntoDockItems", ex);
                return null;
            }
        }

        private static string UniquePath(string path)
        {
            if (!File.Exists(path) && !Directory.Exists(path)) return path;
            string dir = System.IO.Path.GetDirectoryName(path);
            string name = System.IO.Path.GetFileNameWithoutExtension(path);
            string ext = System.IO.Path.GetExtension(path);
            for (int i = 2; i < 1000; i++)
            {
                string p = System.IO.Path.Combine(dir, name + " (" + i + ")" + ext);
                if (!File.Exists(p) && !Directory.Exists(p)) return p;
            }
            return path;
        }

        public void Remove(string id, bool restoreToDesktop)
        {
            DockPin pin = null;
            lock (_gate)
            {
                pin = _pins.FirstOrDefault(delegate (DockPin p) { return p.Id == id; });
                if (pin != null) _pins.Remove(pin);
            }
            if (pin != null && restoreToDesktop && pin.Managed && File.Exists(pin.Path))
            {
                try
                {
                    string target = pin.OriginPath;
                    if (string.IsNullOrEmpty(target) || !Directory.Exists(System.IO.Path.GetDirectoryName(target)))
                        target = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), System.IO.Path.GetFileName(pin.Path));
                    target = UniquePath(target);
                    File.Move(pin.Path, target);
                }
                catch (Exception ex) { Log.Error("Restore shortcut failed", ex); }
            }
            RaiseChanged();
        }

        public void Reorder(int from, int to)
        {
            lock (_gate)
            {
                if (from < 0 || from >= _pins.Count) return;
                if (to < 0) to = 0;
                if (to >= _pins.Count) to = _pins.Count - 1;
                if (from == to) return;
                DockPin p = _pins[from];
                _pins.RemoveAt(from);
                _pins.Insert(to, p);
            }
            RaiseChanged();
        }

        public void Move(string id, int delta)
        {
            lock (_gate)
            {
                int index = _pins.FindIndex(delegate (DockPin p) { return p.Id == id; });
                if (index < 0) return;
                int to = index + delta;
                if (to < 0 || to >= _pins.Count || to == index) return;
                DockPin moved = _pins[index];
                _pins.RemoveAt(index);
                _pins.Insert(to, moved);
            }
            RaiseChanged();
        }

        /// <summary>Tidy-up: alphabetical order, the "整理" action of the dock menu.</summary>
        public int SortByName()
        {
            List<DockPin> sorted;
            lock (_gate)
            {
                sorted = new List<DockPin>(_pins);
                sorted.Sort(delegate (DockPin a, DockPin b)
                {
                    int c = string.Compare(a.DisplayName ?? "", b.DisplayName ?? "", StringComparison.CurrentCultureIgnoreCase);
                    if (c != 0) return c;
                    return string.Compare(a.Id ?? "", b.Id ?? "", StringComparison.OrdinalIgnoreCase);
                });
                int moved = 0;
                for (int i = 0; i < _pins.Count; i++) if (_pins[i].Id != sorted[i].Id) { moved++; break; }
                _pins = sorted;
                if (moved == 0) return 0;
            }
            RaiseChanged();
            return 1;
        }

        public void SetCustomIcon(string id, string iconPath)
        {
            lock (_gate)
            {
                DockPin p = _pins.FirstOrDefault(delegate (DockPin x) { return x.Id == id; });
                if (p != null) p.CustomIcon = iconPath ?? "";
            }
            RaiseChanged();
        }

        public void NotifyChanged() { RaiseChanged(); }

        public bool IsRunning(DockPin pin)
        {
            try
            {
                string target = pin.Path;
                if (target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
                {
                    string resolved = ShortcutResolver.ResolveTargetPath(target);
                    if (!string.IsNullOrEmpty(resolved)) target = resolved;
                }
                if (target.EndsWith(".url", StringComparison.OrdinalIgnoreCase)) return false;
                if (string.Equals(System.IO.Path.GetFileNameWithoutExtension(target), "explorer", StringComparison.OrdinalIgnoreCase))
                    return false; // UWP / AppsFolder shortcut: do not treat Explorer as the app
                string exeName = System.IO.Path.GetFileNameWithoutExtension(target);
                if (string.IsNullOrEmpty(exeName)) return false;
                foreach (Process p in Process.GetProcesses())
                {
                    try
                    {
                        if (string.Equals(p.ProcessName, exeName, StringComparison.OrdinalIgnoreCase)) return true;
                    }
                    catch { }
                    finally { }
                }
            }
            catch { }
            return false;
        }

        public void Launch(DockPin pin)
        {
            try
            {
                if (pin == null || string.IsNullOrEmpty(pin.Path)) return;
                bool isShortcut = pin.Path.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase) ||
                                  pin.Path.EndsWith(".url", StringComparison.OrdinalIgnoreCase);
                // Shortcuts and UWP apps must be opened through the shell: they can carry
                // arguments, a working directory or an AppsFolder target. Resolving only
                // the .lnk target and "focusing" it opens Explorer or swallows the click.
                if (!isShortcut && IsRunning(pin) && FocusRunning(pin)) return;
                if (!File.Exists(pin.Path) && !Directory.Exists(pin.Path))
                {
                    Log.Warn("Dock target missing: " + pin.Path);
                    return;
                }
                var info = new ProcessStartInfo();
                info.FileName = pin.Path;
                info.UseShellExecute = true;
                if (!isShortcut && File.Exists(pin.Path))
                {
                    try { info.WorkingDirectory = System.IO.Path.GetDirectoryName(pin.Path); } catch { }
                }
                Process.Start(info);
                Log.Info("Launched " + pin.Path + (isShortcut ? " via shell shortcut" : ""));
            }
            catch (Exception ex) { Log.Error("Dock launch failed", ex); }
        }

        private bool FocusRunning(DockPin pin)
        {
            try
            {
                string target = pin.Path;
                if (target.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) target = ShortcutResolver.ResolveTargetPath(target);
                string exeName = System.IO.Path.GetFileNameWithoutExtension(target);
                foreach (Process p in Process.GetProcesses())
                {
                    if (!string.Equals(p.ProcessName, exeName, StringComparison.OrdinalIgnoreCase)) continue;
                    IntPtr found = FindMainWindow(p.Id);
                    if (found != IntPtr.Zero)
                    {
                        Win32.ShowWindow(found, Win32.SW_RESTORE);
                        if (Win32.SetForegroundWindow(found)) return true;
                        return false;
                    }
                }
            }
            catch { }
            return false;
        }

        private static IntPtr FindMainWindow(int pid)
        {
            IntPtr found = IntPtr.Zero;
            Win32.EnumWindows(delegate (IntPtr h, IntPtr l)
            {
                uint id;
                Win32.GetWindowThreadProcessId(h, out id);
                if ((int)id != pid) return true;
                if (!Win32.IsWindowVisible(h)) return true;
                long style = Win32.GetWindowLongPtr(h, Win32.GWL_EXSTYLE).ToInt64();
                if ((style & Win32.WS_EX_TOOLWINDOW) != 0) return true;
                if (Win32.GetWindow(h, 4) != IntPtr.Zero) return true; // GW_OWNER
                found = h;
                return false;
            }, IntPtr.Zero);
            return found;
        }


        private void LoadImports()
        {
            ImportsFile file = Json.Read<ImportsFile>(AppPaths.ImportsFile);
            if (file != null && file.Records != null) _imports = file;
        }

        private void SaveImports()
        {
            try
            {
                Json.Write(AppPaths.ImportsFile, _imports);
            }
            catch (Exception ex)
            {
                LastSaveError = ex.GetType().Name + ": " + ex.Message;
                Log.Error("Cannot save desktop-imports to " + AppPaths.Root, ex);
            }
        }

        public bool HasImports { get { return _imports.Records.Count > 0; } }
        public int ImportCount { get { return _imports.Records.Count; } }

        public int ImportShortcuts(List<DesktopItem> items, bool move, out string batchId)
        {
            batchId = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            int count = 0;
            if (items == null) return 0;
            foreach (DesktopItem item in items)
            {
                if (item == null || !item.Selected) continue;
                bool already = false;
                lock (_gate)
                {
                    already = _pins.Any(delegate (DockPin p) { return string.Equals(p.Path, item.Path, StringComparison.OrdinalIgnoreCase); });
                }
                if (already) continue;
                DockPin pin = Add(item.Path, move && !item.IsPublic);
                if (pin == null) continue;
                var rec = new ImportRecord();
                rec.BatchId = batchId;
                rec.PinId = pin.Id;
                rec.From = item.Path;
                rec.To = pin.Path;
                rec.At = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                _imports.Records.Add(rec);
                count++;
            }
            SaveImports();
            RaiseChanged();
            Log.Info("Imported " + count + " desktop shortcuts, batch=" + batchId);
            return count;
        }

        public int UndoLastImport()
        {
            if (_imports.Records.Count == 0) return 0;
            string batch = _imports.Records[_imports.Records.Count - 1].BatchId;
            var batchRecords = _imports.Records.FindAll(delegate (ImportRecord r) { return r.BatchId == batch; });
            int restored = 0;
            foreach (ImportRecord rec in batchRecords)
            {
                try
                {
                    lock (_gate)
                    {
                        DockPin pin = _pins.FirstOrDefault(delegate (DockPin p) { return p.Id == rec.PinId; });
                        if (pin != null) _pins.Remove(pin);
                    }
                    if (!string.IsNullOrEmpty(rec.To) && File.Exists(rec.To) && !string.Equals(rec.To, rec.From, StringComparison.OrdinalIgnoreCase))
                    {
                        string target = rec.From;
                        if (string.IsNullOrEmpty(target) || !Directory.Exists(Path.GetDirectoryName(target)))
                            target = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), Path.GetFileName(rec.To));
                        target = UniquePath(target);
                        File.Move(rec.To, target);
                        restored++;
                    }
                    _imports.Records.Remove(rec);
                }
                catch (Exception ex) { Log.Error("Undo import failed for " + rec.To, ex); }
            }
            SaveImports();
            RaiseChanged();
            Log.Info("Undo import restored " + restored + " items");
            return restored;
        }
        public DockPin Get(string id)
        {
            lock (_gate) return _pins.FirstOrDefault(delegate (DockPin p) { return p.Id == id; });
        }
    }
}

