using System;
using System.Runtime.InteropServices;

namespace DeskOrder.Interop
{
    /// <summary>
    /// Reads the integrity level of the running process.
    ///
    /// Registering an autostart entry needs at least medium integrity: Windows refuses
    /// registry and profile writes from a low integrity (sandboxed / Protected Mode)
    /// process. Calling that out is the difference between "开机自启打不开" and a message
    /// the user can act on, so the startup diagnosis reports it verbatim.
    /// </summary>
    public static class ProcessIntegrity
    {
        private const int TokenQuery = 0x0008;
        private const int TokenIntegrityLevel = 25;

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(IntPtr process, int access, out IntPtr token);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool GetTokenInformation(IntPtr token, int infoClass, IntPtr info, int length, out int returned);

        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool ConvertSidToStringSidW(IntPtr sid, out IntPtr text);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr handle);

        [DllImport("kernel32.dll")]
        private static extern IntPtr LocalFree(IntPtr handle);

        /// <summary>Integrity SID such as S-1-16-8192, or "" when it cannot be read.</summary>
        public static string CurrentSid()
        {
            IntPtr token = IntPtr.Zero;
            IntPtr buffer = IntPtr.Zero;
            try
            {
                if (!OpenProcessToken(GetCurrentProcess(), TokenQuery, out token)) return "";
                int size;
                GetTokenInformation(token, TokenIntegrityLevel, IntPtr.Zero, 0, out size);
                if (size <= 0) return "";
                buffer = Marshal.AllocHGlobal(size);
                if (!GetTokenInformation(token, TokenIntegrityLevel, buffer, size, out size)) return "";
                IntPtr sid = Marshal.ReadIntPtr(buffer);
                if (sid == IntPtr.Zero) return "";
                IntPtr text;
                if (!ConvertSidToStringSidW(sid, out text)) return "";
                string result = Marshal.PtrToStringUni(text);
                LocalFree(text);
                return result ?? "";
            }
            catch { return ""; }
            finally
            {
                if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer);
                if (token != IntPtr.Zero) CloseHandle(token);
            }
        }

        /// <summary>Chinese label for a integrity SID, e.g. "中完整性（Medium）".</summary>
        public static string Describe(string sid)
        {
            int level = LevelOf(sid);
            if (level < 0) return "";
            if (level == 0x0000) return "不受信任（Untrusted）";
            if (level == 0x1000) return "低完整性（Low）";
            if (level == 0x2000) return "中完整性（Medium）";
            if (level == 0x3000) return "高完整性（High）";
            if (level == 0x4000) return "系统（System）";
            return sid;
        }

        public static string CurrentLabel()
        {
            string sid = CurrentSid();
            if (string.IsNullOrEmpty(sid)) return "未知";
            string label = Describe(sid);
            return string.IsNullOrEmpty(label) ? sid : label;
        }

        /// <summary>
        /// True when the process runs below medium integrity, so Windows will refuse the
        /// registry Run key and the per-user Startup folder.
        /// </summary>
        public static bool IsBelowMedium()
        {
            int level = LevelOf(CurrentSid());
            return level >= 0 && level < 0x2000;
        }

        /// <summary>Last sub authority of the SID, or -1 when it cannot be parsed.</summary>
        public static int LevelOf(string sid)
        {
            if (string.IsNullOrEmpty(sid)) return -1;
            int dash = sid.LastIndexOf('-');
            if (dash < 0 || dash + 1 >= sid.Length) return -1;
            int value;
            if (!int.TryParse(sid.Substring(dash + 1), out value)) return -1;
            return value;
        }
    }
}
