using System;
using System.Diagnostics;
using Microsoft.Win32;

namespace DeskOrder.Core
{
    public static class StartupService
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "DeskOrder";

        public static bool IsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    if (key == null) return false;
                    return key.GetValue(ValueName) != null;
                }
            }
            catch { return false; }
        }

        public static bool SetEnabled(bool enabled)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKey))
                {
                    if (key == null) return false;
                    if (enabled)
                    {
                        string exe = Process.GetCurrentProcess().MainModule.FileName;
                        key.SetValue(ValueName, "\"" + exe + "\" --tray");
                    }
                    else key.DeleteValue(ValueName, false);
                }
                return true;
            }
            catch (Exception ex)
            {
                Log.Error("Startup toggle failed", ex);
                return false;
            }
        }

        public static void RefreshIfEnabled()
        {
            if (!IsEnabled()) return;
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKey))
                {
                    if (key == null) return;
                    string current = Convert.ToString(key.GetValue(ValueName));
                    string exe = Process.GetCurrentProcess().MainModule.FileName;
                    string wanted = "\"" + exe + "\" --tray";
                    if (!string.Equals(current, wanted, StringComparison.OrdinalIgnoreCase))
                        key.SetValue(ValueName, wanted);
                }
            }
            catch { }
        }
    }
}
