using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace DeskOrder.Core
{
    public static class AppPaths
    {
        private static string _root;
        private static string _local;

        public static string Root { get { return _root; } }
        public static string LocalRoot { get { return _local; } }
        public static string RequestedRoot { get; private set; }
        public static string FallbackNote { get; private set; }
        public static bool Writable { get; private set; }

        public static string SettingsFile { get { return Path.Combine(_root, "settings.json"); } }
        public static string PinsFile { get { return Path.Combine(_root, "pins.json"); } }
        public static string ImportsFile { get { return Path.Combine(_root, "desktop-imports.json"); } }
        public static string PlaylistFile { get { return Path.Combine(_root, "playlist.json"); } }
        public static string DockItemsDir { get { return Path.Combine(_root, "DockItems"); } }
        public static string IconsDir { get { return Path.Combine(_root, "Icons"); } }
        public static string LogsDir { get { return Path.Combine(_root, "logs"); } }
        public static string CacheDir { get { return Path.Combine(_local, "cache"); } }
        public static string IconCacheDir { get { return Path.Combine(CacheDir, "icons"); } }
        public static string CoverCacheDir { get { return Path.Combine(CacheDir, "covers"); } }

        public static bool IsFallback { get { return !string.IsNullOrEmpty(FallbackNote); } }

        /// <summary>Name of the marker next to the exe that turns on portable mode.</summary>
        public const string PortableMarker = "portable.txt";

        /// <summary>Folder the exe lives in; tests point this at a temp folder.</summary>
        public static string PortableBaseOverride { get; set; }

        public static bool Portable { get; private set; }

        public static string PortableBase
        {
            get
            {
                if (!string.IsNullOrEmpty(PortableBaseOverride)) return PortableBaseOverride;
                return ExeDirectory();
            }
        }

        public static string PortableRoot
        {
            get
            {
                string based = PortableBase;
                return string.IsNullOrEmpty(based) ? "" : Path.Combine(based, "DeskOrderData");
            }
        }

        public static string MarkerPath
        {
            get
            {
                string based = PortableBase;
                return string.IsNullOrEmpty(based) ? "" : Path.Combine(based, PortableMarker);
            }
        }

        /// <summary>Portable when a marker file or the data folder sits next to the exe.</summary>
        public static bool WantsPortable()
        {
            try
            {
                string marker = MarkerPath;
                if (!string.IsNullOrEmpty(marker) && File.Exists(marker)) return true;
                string root = PortableRoot;
                return !string.IsNullOrEmpty(root) && Directory.Exists(root);
            }
            catch { return false; }
        }

        public static void Initialize(string dataDir) { Initialize(dataDir, false); }

        /// <summary>
        /// Picks the first writable data root. A restricted token (sandbox, low integrity,
        /// portable media) cannot write %APPDATA%, and every Dock edit used to die silently
        /// on that save; so we probe candidates instead of trusting the default path.
        /// </summary>
        public static void Initialize(string dataDir, bool exactOnly)
        {
            FallbackNote = "";
            Writable = false;
            Portable = false;
            RequestedRoot = string.IsNullOrWhiteSpace(dataDir) ? DefaultRoot() : SafeFullPath(dataDir);

            string chosen = null;
            foreach (string candidate in Candidates(dataDir, exactOnly))
            {
                if (TryPrepare(candidate)) { chosen = candidate; Writable = true; break; }
            }
            if (chosen == null) chosen = RequestedRoot;

            _root = chosen;
            _local = Path.Combine(_root, "local");
            Portable = string.IsNullOrWhiteSpace(dataDir) &&
                       string.Equals(_root, PortableRoot, StringComparison.OrdinalIgnoreCase);
            if (!string.Equals(_root, RequestedRoot, StringComparison.OrdinalIgnoreCase))
            {
                FallbackNote = (Portable ? "便携模式：" : "配置目录不可写（" + RequestedRoot + "），已改用 ") + _root;
            }
            else if (!Writable)
            {
                FallbackNote = "配置目录不可写：" + _root + "，本次改动无法保存";
            }

            Ensure(_root);
            Ensure(_local);
            Ensure(DockItemsDir);
            Ensure(IconsDir);
            Ensure(LogsDir);
            Ensure(CacheDir);
            Ensure(IconCacheDir);
            Ensure(CoverCacheDir);
        }

        public static string DefaultRoot()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeskOrder");
        }

        public static List<string> Candidates(string dataDir, bool exactOnly)
        {
            var list = new List<string>();
            if (!string.IsNullOrWhiteSpace(dataDir))
            {
                Add(list, SafeFullPath(dataDir));
                if (exactOnly) return list;
            }
            if (WantsPortable()) Add(list, PortableRoot);
            if (string.IsNullOrWhiteSpace(dataDir)) Add(list, DefaultRoot());
            Add(list, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeskOrder"));
            Add(list, PortableRoot);
            Add(list, Path.Combine(Path.GetTempPath(), "DeskOrder"));
            return list;
        }

        /// <summary>First candidate that can actually be created and written to.</summary>
        public static string FirstWritable(IEnumerable<string> candidates, out bool anyWritable)
        {
            anyWritable = false;
            string first = null;
            if (candidates == null) return null;
            foreach (string c in candidates)
            {
                if (first == null) first = c;
                if (TryPrepare(c)) { anyWritable = true; return c; }
            }
            return first;
        }

        /// <summary>Creates the directory (if needed) and proves it accepts a file write.</summary>
        public static bool TryPrepare(string dir)
        {
            if (string.IsNullOrWhiteSpace(dir)) return false;
            string probe = null;
            try
            {
                Directory.CreateDirectory(dir);
                probe = Path.Combine(dir, ".write-probe-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch
            {
                try { if (probe != null && File.Exists(probe)) File.Delete(probe); } catch { }
                return false;
            }
        }

        public static string ExeDirectory()
        {
            try
            {
                string path = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(path)) return Path.GetDirectoryName(path);
            }
            catch { }
            try { return AppDomain.CurrentDomain.BaseDirectory; }
            catch { return ""; }
        }

        private static void Add(List<string> list, string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;
            string full;
            try { full = Path.GetFullPath(path); }
            catch { return; }
            foreach (string existing in list)
            {
                if (string.Equals(existing, full, StringComparison.OrdinalIgnoreCase)) return;
            }
            list.Add(full);
        }

        private static string SafeFullPath(string path)
        {
            try { return Path.GetFullPath(path); }
            catch { return path; }
        }

        public static void Ensure(string dir)
        {
            try
            {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            }
            catch (Exception ex)
            {
                Log.Error("Cannot create directory " + dir, ex);
            }
        }
    }
}
