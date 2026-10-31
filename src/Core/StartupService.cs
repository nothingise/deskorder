using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;
using Microsoft.Win32;
using DeskOrder.Interop;

namespace DeskOrder.Core
{
    public enum StartupKind
    {
        None = 0,
        RunKey = 1,
        StartupFolder = 2,
        ScheduledTask = 3
    }

    /// <summary>Result of one enable/disable attempt, including why it failed.</summary>
    public class StartupOutcome
    {
        public bool Ok;
        public StartupKind Kind = StartupKind.None;
        public string Error = "";
        public string Hint = "";

        public string Mechanism { get { return StartupService.Name(Kind); } }
    }

    /// <summary>What is registered right now, and whether it will actually run at logon.</summary>
    public class StartupStatus
    {
        public bool Enabled;
        public StartupKind Kind = StartupKind.None;
        public string Command = "";
        public string Expected = "";
        public bool Stale;
        public bool OsDisabled;
        public bool RunKeyWritable;
        public string Integrity = "";
        public string Problem = "";

        public string Mechanism { get { return StartupService.Name(Kind); } }
    }

    /// <summary>
    /// Autostart registration.
    ///
    /// A single mechanism is not enough in practice: the HKCU Run key can be refused
    /// (low integrity sandbox, policy), silently listed as disabled by Windows' own
    /// "启动应用" page, or left pointing at an exe the user has since moved. Every
    /// failure here is reported with the concrete reason, and registration falls back
    /// to the per-user Startup folder and then to a logon scheduled task.
    /// </summary>
    public static class StartupService
    {
        public const string ValueName = "DeskOrder";
        public const string TaskName = "DeskOrder";
        public const string LnkName = "DeskOrder.lnk";

        public const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string ApprovedPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

        /// <summary>Test hooks; null means the real location.</summary>
        public static string RunKeyOverride;
        public static string LnkOverride;
        public static bool AllowTaskFallback = true;

        private static string _exePath;

        // ---------------------------------------------------------------- targets

        public static string ExePath()
        {
            if (!string.IsNullOrEmpty(_exePath)) return _exePath;
            string path = null;
            try { path = Process.GetCurrentProcess().MainModule.FileName; }
            catch { }
            if (string.IsNullOrEmpty(path))
            {
                try { path = Assembly.GetEntryAssembly().Location; }
                catch { }
            }
            if (string.IsNullOrEmpty(path)) path = AppDomain.CurrentDomain.BaseDirectory;
            _exePath = Path.GetFullPath(path);
            return _exePath;
        }

        /// <summary>The command line handed to Windows, e.g. "C:\...\桌序.exe" --tray.</summary>
        public static string CommandLine()
        {
            return "\"" + ExePath() + "\" --tray";
        }

        public static string RunKeyName
        {
            get { return string.IsNullOrEmpty(RunKeyOverride) ? RunKeyPath : RunKeyOverride; }
        }

        public static string LnkPath()
        {
            if (!string.IsNullOrEmpty(LnkOverride)) return LnkOverride;
            string startup = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            return Path.Combine(startup, LnkName);
        }

        public static string Name(StartupKind kind)
        {
            switch (kind)
            {
                case StartupKind.RunKey: return "注册表 Run 键";
                case StartupKind.StartupFolder: return "启动文件夹";
                case StartupKind.ScheduledTask: return "登录计划任务";
                default: return "未登记";
            }
        }

        // ---------------------------------------------------------------- reading

        public static bool IsEnabled()
        {
            if (!string.IsNullOrEmpty(ReadRegisteredCommand())) return true;
            try { if (File.Exists(LnkPath())) return true; } catch { }
            if (AllowTaskFallback && string.IsNullOrEmpty(RunKeyOverride) && string.IsNullOrEmpty(LnkOverride))
                return TaskExists();
            return false;
        }

        /// <summary>Command line stored in the Run key, or "" when the key/value is absent.</summary>
        public static string ReadRegisteredCommand()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyName))
                {
                    if (key == null) return "";
                    object value = key.GetValue(ValueName);
                    return value == null ? "" : Convert.ToString(value);
                }
            }
            catch { return ""; }
        }

        /// <summary>Target the Startup-folder shortcut points at, or "" when absent.</summary>
        public static string ReadLnkTarget()
        {
            try
            {
                string path = LnkPath();
                if (!File.Exists(path)) return "";
                ShortcutInfo info = ShortcutResolver.Resolve(path);
                if (info == null) return "";
                if (string.IsNullOrEmpty(info.TargetPath)) return "";
                return info.TargetPath + (string.IsNullOrEmpty(info.Arguments) ? "" : " " + info.Arguments);
            }
            catch { return ""; }
        }

        /// <summary>
        /// True when Windows' own "启动应用" switch has the Run entry turned off. The
        /// value lives in StartupApproved\Run; the low bit of the first byte means
        /// disabled (0x03 off, 0x02 on).
        /// </summary>
        public static bool OsDisabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(ApprovedPath))
                {
                    if (key == null) return false;
                    byte[] raw = key.GetValue(ValueName) as byte[];
                    return IsApprovedDisabled(raw);
                }
            }
            catch { return false; }
        }

        /// <summary>Pure helper so the byte layout can be tested without a registry.</summary>
        public static bool IsApprovedDisabled(byte[] raw)
        {
            if (raw == null || raw.Length == 0) return false;
            return (raw[0] & 0x01) != 0;
        }

        public static bool CanWriteRunKey()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyName, true))
                {
                    return key != null;
                }
            }
            catch { return false; }
        }

        public static bool TaskExists()
        {
            // The settings page asks for the status while it builds, so the (comparatively
            // slow) schtasks call is cached for a few seconds.
            if (_taskCheckedAt != DateTime.MinValue && (DateTime.UtcNow - _taskCheckedAt).TotalSeconds < 10)
                return _taskExists;
            string output = Run("schtasks.exe", "/Query /TN \"" + TaskName + "\"");
            _taskExists = output != null;
            _taskCheckedAt = DateTime.UtcNow;
            return _taskExists;
        }

        private static bool _taskExists;
        private static DateTime _taskCheckedAt = DateTime.MinValue;

        /// <summary>Snapshot used by the settings page and by --startup status.</summary>
        public static StartupStatus Status()
        {
            var status = new StartupStatus();
            status.Expected = CommandLine();
            status.Integrity = ProcessIntegrity.CurrentLabel();
            status.RunKeyWritable = CanWriteRunKey();

            string runValue = ReadRegisteredCommand();
            string lnkTarget = ReadLnkTarget();
            bool task = false;
            if (runValue.Length == 0 && lnkTarget.Length == 0 &&
                AllowTaskFallback && string.IsNullOrEmpty(RunKeyOverride) && string.IsNullOrEmpty(LnkOverride))
                task = TaskExists();

            if (runValue.Length > 0)
            {
                status.Enabled = true;
                status.Kind = StartupKind.RunKey;
                status.Command = runValue;
            }
            else if (lnkTarget.Length > 0)
            {
                status.Enabled = true;
                status.Kind = StartupKind.StartupFolder;
                status.Command = lnkTarget;
            }
            else if (task)
            {
                status.Enabled = true;
                status.Kind = StartupKind.ScheduledTask;
                status.Command = status.Expected;
            }

            if (status.Enabled)
            {
                status.Stale = !SameCommand(status.Command, status.Expected);
                status.OsDisabled = status.Kind == StartupKind.RunKey && OsDisabled();
                if (status.OsDisabled)
                    status.Problem = "已被 Windows「启动应用」关闭，登录时不会运行。";
                else if (status.Stale)
                    status.Problem = "登记的是旧路径，登录后启动的还是旧程序。";
            }
            return status;
        }

        /// <summary>One line for the log: enough to tell whether the next logon will work.</summary>
        public static string StatusLine()
        {
            StartupStatus status = Status();
            var sb = new StringBuilder();
            sb.Append(status.Enabled ? "enabled" : "disabled");
            sb.Append(" kind=").Append(status.Kind);
            sb.Append(" stale=").Append(status.Stale);
            sb.Append(" osDisabled=").Append(status.OsDisabled);
            sb.Append(" runKeyWritable=").Append(status.RunKeyWritable);
            sb.Append(" integrity=").Append(status.Integrity);
            if (status.Enabled) sb.Append(" command=").Append(status.Command);
            if (status.Problem.Length > 0) sb.Append(" problem=").Append(status.Problem);
            return sb.ToString();
        }

        /// <summary>Text under the switch in the settings page.</summary>
        public static string Describe(StartupStatus status)
        {
            if (status == null) return "";
            if (!status.Enabled) return "未启用。开启后登录 Windows 时自动以 Dock + 托盘方式运行。";
            string where;
            if (status.Kind == StartupKind.RunKey) where = "已登记到注册表 Run 键，可在「任务管理器 → 启动应用」看到。";
            else if (status.Kind == StartupKind.StartupFolder) where = "已登记到当前用户的「启动」文件夹。";
            else where = "已登记为登录计划任务，可在「任务计划程序」看到 DeskOrder。";
            if (status.Problem.Length > 0)
                return where + " " + status.Problem + "\n当前登记：" + AppPaths.Display(status.Command) +
                       "\n应为：" + AppPaths.Display(status.Expected);
            return where + "\n启动命令：" + AppPaths.Display(status.Command);
        }

        /// <summary>A ready-to-paste command for a normal (medium integrity) PowerShell window.</summary>
        public static string ManualCommand()
        {
            return "Set-ItemProperty -Path 'HKCU:\\Software\\Microsoft\\Windows\\CurrentVersion\\Run' -Name 'DeskOrder' " +
                   "-Value ('\"' + '" + ExePath() + "' + '\" --tray')";
        }

        // ---------------------------------------------------------------- writing

        /// <summary>
        /// Registers or removes autostart. Returns the mechanism that succeeded, or the
        /// reason every mechanism was refused - never a silent failure.
        /// </summary>
        public static StartupOutcome SetEnabled(bool enabled)
        {
            var outcome = new StartupOutcome();
            if (enabled)
            {
                string error;
                if (TryRunKey(true, out error)) { outcome.Ok = true; outcome.Kind = StartupKind.RunKey; return outcome; }
                string runKeyError = error;
                if (TryStartupFolder(true, out error)) { outcome.Ok = true; outcome.Kind = StartupKind.StartupFolder; return outcome; }
                string folderError = error;
                if (AllowTaskFallback && string.IsNullOrEmpty(RunKeyOverride) && string.IsNullOrEmpty(LnkOverride) &&
                    TryScheduledTask(true, out error))
                {
                    outcome.Ok = true;
                    outcome.Kind = StartupKind.ScheduledTask;
                    return outcome;
                }
                string taskError = string.IsNullOrEmpty(error) ? "计划任务未创建" : error;

                outcome.Ok = false;
                outcome.Error = "注册表：" + runKeyError + "；启动文件夹：" + folderError + "；计划任务：" + taskError;
                outcome.Hint = BuildHint();
                Log.Error("Autostart registration failed - " + outcome.Error);
                return outcome;
            }

            string removeError = "";
            string first;
            bool removed = false;
            if (TryRunKey(false, out first)) removed = true; else removeError = Append(removeError, first);
            if (TryStartupFolder(false, out first)) removed = true; else removeError = Append(removeError, first);
            if (AllowTaskFallback && string.IsNullOrEmpty(RunKeyOverride) && string.IsNullOrEmpty(LnkOverride))
            {
                if (TryScheduledTask(false, out first)) removed = true; else removeError = Append(removeError, first);
            }
            outcome.Ok = removed || removeError.Length == 0;
            outcome.Error = outcome.Ok ? "" : removeError;
            return outcome;
        }

        /// <summary>Keeps an existing registration pointing at the exe that is running now.</summary>
        public static void RefreshIfEnabled()
        {
            try
            {
                string runValue = ReadRegisteredCommand();
                if (runValue.Length > 0)
                {
                    if (!SameCommand(runValue, CommandLine()))
                    {
                        string error;
                        if (TryRunKey(true, out error))
                            Log.Info("autostart path refreshed -> " + CommandLine());
                        else
                            Log.Error("autostart path refresh failed - " + error);
                    }
                    return;
                }
                if (ReadLnkTarget().Length > 0)
                {
                    string error;
                    if (!TryStartupFolder(true, out error)) Log.Error("autostart shortcut refresh failed - " + error);
                }
            }
            catch (Exception ex) { Log.Error("autostart refresh", ex); }
        }

        private static bool TryRunKey(bool enabled, out string error)
        {
            error = "";
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyName))
                {
                    if (key == null) { error = "无法打开 " + RunKeyName; return false; }
                    if (enabled)
                    {
                        key.SetValue(ValueName, CommandLine());
                        ClearApprovedFlag();
                        RemoveLnkQuietly();
                    }
                    else key.DeleteValue(ValueName, false);
                }
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static bool TryStartupFolder(bool enabled, out string error)
        {
            error = "";
            try
            {
                string path = LnkPath();
                if (enabled)
                {
                    if (!ShortcutResolver.Create(path, ExePath(), "--tray", Path.GetDirectoryName(ExePath())))
                    {
                        error = "无法创建快捷方式 " + path;
                        return false;
                    }
                    ClearApprovedFlag();
                    RemoveRunValueQuietly();
                    return true;
                }
                if (File.Exists(path)) File.Delete(path);
                ClearApprovedFlag();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        private static bool TryScheduledTask(bool enabled, out string error)
        {
            error = "";
            if (!enabled)
            {
                if (!TaskExists()) return true;
                string deleteError;
                if (Run("schtasks.exe", "/Delete /F /TN \"" + TaskName + "\"", out deleteError) == null)
                {
                    error = deleteError;
                    return false;
                }
                return true;
            }
            // /TR takes the program plus arguments; the inner quotes are escaped for schtasks.
            string arguments = "/Create /F /SC ONLOGON /TN \"" + TaskName + "\" /TR \"\\\"" + ExePath() + "\\\" --tray\"";
            string outputError;
            if (Run("schtasks.exe", arguments, out outputError) == null)
            {
                error = outputError;
                return false;
            }
            return true;
        }

        private static void ClearApprovedFlag()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(ApprovedPath, true))
                {
                    if (key != null) key.DeleteValue(ValueName, false);
                }
            }
            catch { }
        }

        private static void RemoveLnkQuietly()
        {
            try
            {
                string path = LnkPath();
                if (File.Exists(path)) File.Delete(path);
            }
            catch { }
        }

        private static void RemoveRunValueQuietly()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyName, true))
                {
                    if (key != null) key.DeleteValue(ValueName, false);
                }
            }
            catch { }
        }

        private static string BuildHint()
        {
            string integrity = ProcessIntegrity.CurrentLabel();
            var sb = new StringBuilder();
            if (ProcessIntegrity.IsBelowMedium())
            {
                sb.Append("当前进程是").Append(integrity);
                sb.Append("，Windows 不允许低完整性进程写注册表和用户配置目录。");
                sb.Append("请从自己的桌面直接双击 桌序.exe 启动（不要经由沙箱/受限环境），再打开这个开关；");
                sb.Append("或在普通 PowerShell 里执行「设置 → 复制启动命令」得到的那一行。");
            }
            else
            {
                sb.Append("当前进程是").Append(integrity);
                sb.Append("，但仍被拒绝；可能被安全软件或组策略拦截。");
                sb.Append("可点「复制启动命令」，在普通 PowerShell 里手动登记。");
            }
            return sb.ToString();
        }

        private static string Append(string current, string next)
        {
            if (string.IsNullOrEmpty(next)) return current;
            return current.Length == 0 ? next : current + "；" + next;
        }

        private static bool SameCommand(string left, string right)
        {
            return string.Equals(Normalize(left), Normalize(right), StringComparison.OrdinalIgnoreCase);
        }

        private static string Normalize(string command)
        {
            return (command ?? "").Trim().Replace("  ", " ");
        }

        private static string Run(string file, string arguments)
        {
            string error;
            return Run(file, arguments, out error);
        }

        /// <summary>Runs a helper process; returns stdout, or null with the reason in error.</summary>
        private static string Run(string file, string arguments, out string error)
        {
            error = "";
            try
            {
                var info = new ProcessStartInfo(file, arguments);
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.RedirectStandardOutput = true;
                info.RedirectStandardError = true;
                using (Process process = Process.Start(info))
                {
                    string output = process.StandardOutput.ReadToEnd();
                    string errors = process.StandardError.ReadToEnd();
                    if (!process.WaitForExit(15000))
                    {
                        try { process.Kill(); } catch { }
                        error = file + " 超时";
                        return null;
                    }
                    if (process.ExitCode != 0)
                    {
                        error = (errors + output).Trim();
                        if (error.Length == 0) error = file + " 退出码 " + process.ExitCode;
                        return null;
                    }
                    return output;
                }
            }
            catch (Exception ex)
            {
                error = ex.GetType().Name + ": " + ex.Message;
                return null;
            }
        }
    }
}
