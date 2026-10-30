using System;
using System.IO;
using System.Text;

namespace DeskOrder.Core
{
    public static class Log
    {
        private static readonly object Gate = new object();
        private static string _file;
        private static bool _enabled = true;

        public static void Initialize(string logsDir)
        {
            try
            {
                AppPaths.Ensure(logsDir);
                _file = Path.Combine(logsDir, "app.log");
                if (File.Exists(_file) && new FileInfo(_file).Length > 1024 * 1024)
                {
                    string old = Path.Combine(logsDir, "app.old.log");
                    try { if (File.Exists(old)) File.Delete(old); File.Move(_file, old); }
                    catch { }
                }
            }
            catch { _enabled = false; }
            Info("---- DeskOrder start ----");
        }

        public static void Info(string message) { Write("INFO", message); }
        public static void Warn(string message) { Write("WARN", message); }

        public static void Error(string message) { Write("ERROR", message); }

        public static void Error(string message, Exception ex)
        {
            Write("ERROR", message + (ex == null ? "" : " :: " + ex.GetType().Name + ": " + ex.Message + Environment.NewLine + ex.StackTrace));
        }

        private static void Write(string level, string message)
        {
            if (!_enabled) return;
            try
            {
                lock (Gate)
                {
                    if (string.IsNullOrEmpty(_file)) return;
                    string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + level + "] " + message + Environment.NewLine;
                    File.AppendAllText(_file, line, new UTF8Encoding(false));
                }
            }
            catch { }
        }
    }
}
