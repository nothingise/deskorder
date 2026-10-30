using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace DeskOrder.Core
{
    /// <summary>
    /// Tiny MCI playback wrapper. Works without Media Foundation and supports
    /// wav/mp3 through the codecs shipped with Windows.
    /// </summary>
    public class MciPlayer : IDisposable
    {
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        private static extern int mciSendString(string command, StringBuilder returnValue, int returnLength, IntPtr hwndCallback);
        [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
        private static extern bool mciGetErrorString(int errorCode, StringBuilder errorText, int errorTextSize);

        private readonly string _alias;
        private bool _open;

        public string LastError { get; private set; }
        public long LengthMs { get; private set; }
        public string CurrentPath { get; private set; }

        public bool IsOpen { get { return _open; } }

        public MciPlayer()
        {
            _alias = "dsk" + Guid.NewGuid().ToString("N").Substring(0, 8);
        }

        public bool Open(string path)
        {
            Close();
            LastError = "";
            CurrentPath = path;
            LengthMs = 0;
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                LastError = "文件不存在";
                return false;
            }
            string ext = Path.GetExtension(path).ToLowerInvariant();
            string type = "";
            if (ext == ".wav") type = " type waveaudio";
            else if (ext == ".mp3" || ext == ".wma") type = " type mpegvideo";
            string command = "open \"" + path.Replace("\"", "'") + "\"" + type + " alias " + _alias;
            string ret;
            int err = Send(command, out ret);
            if (err != 0)
            {
                // retry without an explicit type so MCI can auto detect
                err = Send("open \"" + path.Replace("\"", "'") + "\" alias " + _alias, out ret);
                if (err != 0)
                {
                    SetError(err, "打开音频失败");
                    return false;
                }
            }
            _open = true;
            Send("set " + _alias + " time format milliseconds", out ret);
            string length = Query("status " + _alias + " length");
            long ms;
            if (long.TryParse(length, out ms)) LengthMs = ms;
            SetVolume(0.8);
            return true;
        }

        public bool Play()
        {
            if (!_open) return false;
            string ret;
            int err = Send("play " + _alias, out ret);
            if (err != 0) { SetError(err, "播放失败"); return false; }
            return true;
        }

        public bool Pause()
        {
            if (!_open) return false;
            string ret;
            int err = Send("pause " + _alias, out ret);
            if (err != 0) { SetError(err, "暂停失败"); return false; }
            return true;
        }

        public bool Resume()
        {
            if (!_open) return false;
            string ret;
            int err = Send("resume " + _alias, out ret);
            if (err != 0) { SetError(err, "继续播放失败"); return false; }
            return true;
        }

        public bool Stop()
        {
            if (!_open) return false;
            string ret;
            Send("stop " + _alias, out ret);
            return true;
        }

        public bool Seek(long ms)
        {
            if (!_open) return false;
            if (ms < 0) ms = 0;
            if (LengthMs > 0 && ms > LengthMs) ms = LengthMs;
            string ret;
            int err = Send("seek " + _alias + " to " + ms, out ret);
            if (err != 0) { SetError(err, "跳转失败"); return false; }
            return true;
        }

        public long PositionMs()
        {
            if (!_open) return 0;
            string value = Query("status " + _alias + " position");
            long ms;
            return long.TryParse(value, out ms) ? ms : 0;
        }

        public string Mode()
        {
            if (!_open) return "closed";
            return Query("status " + _alias + " mode").Trim().ToLowerInvariant();
        }

        public bool SetVolume(double volume)
        {
            if (!_open) return false;
            if (volume < 0) volume = 0;
            if (volume > 1) volume = 1;
            int value = (int)Math.Round(volume * 1000);
            string ret;
            int err = Send("setaudio " + _alias + " volume to " + value, out ret);
            return err == 0;
        }

        public void Close()
        {
            if (!_open) return;
            string ret;
            Send("stop " + _alias, out ret);
            Send("close " + _alias, out ret);
            _open = false;
            LengthMs = 0;
        }

        private string Query(string command)
        {
            string ret;
            Send(command, out ret);
            return ret ?? "";
        }

        private int Send(string command, out string ret)
        {
            var sb = new StringBuilder(512);
            int err = mciSendString(command, sb, sb.Capacity, IntPtr.Zero);
            ret = sb.ToString();
            return err;
        }

        private void SetError(int code, string prefix)
        {
            var sb = new StringBuilder(512);
            mciGetErrorString(code, sb, sb.Capacity);
            LastError = prefix + "：" + sb.ToString() + " (0x" + code.ToString("X") + ")";
            Log.Warn("MCI error: " + LastError);
        }

        public void Dispose()
        {
            Close();
        }
    }
}
