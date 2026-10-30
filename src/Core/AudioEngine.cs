using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace DeskOrder.Core
{
    /// <summary>
    /// Zero-dependency WASAPI loopback capture. The audio is never stored: we only
    /// keep a short mono ring buffer to compute a 48-band spectrum and beat energy.
    /// </summary>
    public class AudioEngine : IDisposable
    {
        public const int Bands = 48;
        private const int FftSize = 1024;
        private const int RingSize = 16384;
        private const uint AUDCLNT_STREAMFLAGS_LOOPBACK = 0x00020000;
        private const int CLSCTX_ALL = 0x17;

        private readonly object _gate = new object();
        private readonly float[] _published = new float[Bands];
        private readonly float[] _smoothed = new float[Bands];
        private readonly float[] _ring = new float[RingSize];
        private int _ringWrite;
        private int _ringCount;
        private float _bass;
        private float _level;
        private float _beat;
        private bool _active;
        private bool _running;
        private volatile bool _stopRequested;
        private Thread _thread;
        private string _deviceName = "";
        private string _lastError = "";
        private DateTime _lastPacketUtc = DateTime.MinValue;
        private DateTime _lastBeatUtc = DateTime.MinValue;
        private float _bassAverage = 0.02f;
        private float _agc = 0.05f;
        private int _sampleRate = 48000;

        public bool IsRunning { get { lock (_gate) return _running; } }
        public bool IsActive { get { lock (_gate) return _active; } }
        public string DeviceName { get { lock (_gate) return _deviceName; } }
        public string LastError { get { lock (_gate) return _lastError; } }
        public float Bass { get { lock (_gate) return _bass; } }
        public float Level { get { lock (_gate) return _level; } }
        public float Beat { get { lock (_gate) return _beat; } }
        public int SampleRate { get { lock (_gate) return _sampleRate; } }

        public float[] GetSpectrum()
        {
            lock (_gate) return (float[])_published.Clone();
        }

        public void Start()
        {
            lock (_gate)
            {
                if (_running) return;
                _running = true;
                _stopRequested = false;
                _lastError = "";
            }
            _thread = new Thread(CaptureLoop);
            _thread.IsBackground = true;
            _thread.Name = "DeskOrder.Audio";
            _thread.SetApartmentState(ApartmentState.MTA);
            _thread.Start();
            Log.Info("Audio engine started");
        }

        public void Stop()
        {
            _stopRequested = true;
            Thread t;
            lock (_gate) { t = _thread; _thread = null; _running = false; }
            if (t != null) { try { t.Join(1200); } catch { } }
            lock (_gate)
            {
                _active = false;
                for (int i = 0; i < _published.Length; i++) { _published[i] = 0; _smoothed[i] = 0; }
                _bass = 0; _level = 0; _beat = 0;
            }
            Log.Info("Audio engine stopped");
        }

        private void CaptureLoop()
        {
            int hr = CoInitializeEx(IntPtr.Zero, 0);
            try
            {
                while (!_stopRequested)
                {
                    IntPtr pEnum = IntPtr.Zero;
                    IMMDeviceEnumerator enumerator = null;
                    IMMDevice device = null;
                    IAudioClient client = null;
                    IAudioCaptureClient capture = null;
                    IntPtr mixFormat = IntPtr.Zero;
                    try
                    {
                        Guid clsid = new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E");
                        Guid iidEnum = typeof(IMMDeviceEnumerator).GUID;
                        hr = CoCreateInstance(ref clsid, IntPtr.Zero, CLSCTX_ALL, ref iidEnum, out pEnum);
                        if (hr < 0) throw new Exception("CoCreateInstance 0x" + hr.ToString("X8"));
                        enumerator = (IMMDeviceEnumerator)Marshal.GetObjectForIUnknown(pEnum);
                        Marshal.Release(pEnum); pEnum = IntPtr.Zero;

                        hr = enumerator.GetDefaultAudioEndpoint(0, 0, out device);
                        if (hr < 0) throw new Exception("No default render endpoint 0x" + hr.ToString("X8"));
                        string devName = WavApi.GetDeviceId(device);

                        Guid iidClient = typeof(IAudioClient).GUID;
                        object clientObj;
                        hr = device.Activate(ref iidClient, CLSCTX_ALL, IntPtr.Zero, out clientObj);
                        if (hr < 0) throw new Exception("Activate IAudioClient 0x" + hr.ToString("X8"));
                        client = (IAudioClient)clientObj;
                        hr = client.GetMixFormat(out mixFormat);
                        if (hr < 0) throw new Exception("GetMixFormat 0x" + hr.ToString("X8"));
                        WavApi.Format format = WavApi.ParseFormat(mixFormat);
                        if (format == null) throw new Exception("Unsupported mix format");

                        hr = client.Initialize(0, AUDCLNT_STREAMFLAGS_LOOPBACK, 2000000, 0, mixFormat, IntPtr.Zero);
                        if (hr < 0) throw new Exception("Initialize loopback 0x" + hr.ToString("X8"));
                        Guid iidCapture = typeof(IAudioCaptureClient).GUID;
                        IntPtr pCapture;
                        hr = client.GetService(ref iidCapture, out pCapture);
                        if (hr < 0) throw new Exception("GetService capture 0x" + hr.ToString("X8"));
                        capture = (IAudioCaptureClient)Marshal.GetObjectForIUnknown(pCapture);
                        Marshal.Release(pCapture);
                        hr = client.Start();
                        if (hr < 0) throw new Exception("Start 0x" + hr.ToString("X8"));

                        lock (_gate)
                        {
                            _sampleRate = (int)format.SampleRate;
                            _deviceName = devName;
                            _lastError = "";
                            _active = true;
                        }
                        Log.Info("Audio loopback: " + format.Channels + "ch " + format.SampleRate + "Hz " + format.Bits + "bit device=" + devName);

                        DateTime lastAnalyze = DateTime.UtcNow;
                        DateTime lastDeviceCheck = DateTime.UtcNow;
                        while (!_stopRequested)
                        {
                            bool gotData = false;
                            uint next;
                            hr = capture.GetNextPacketSize(out next);
                            while (hr >= 0 && next > 0)
                            {
                                IntPtr data; uint frames; uint flags; long devPos; long qpcPos;
                                hr = capture.GetBuffer(out data, out frames, out flags, out devPos, out qpcPos);
                                if (hr < 0) break;
                                if (frames > 0)
                                {
                                    gotData = true;
                                    bool silent = (flags & 2) != 0;
                                    AppendSamples(data, (int)frames, silent, format);
                                    lock (_gate) _lastPacketUtc = DateTime.UtcNow;
                                }
                                capture.ReleaseBuffer(frames);
                                hr = capture.GetNextPacketSize(out next);
                            }

                            DateTime now = DateTime.UtcNow;
                            if ((now - lastAnalyze).TotalMilliseconds >= 33)
                            {
                                lastAnalyze = now;
                                Analyze();
                            }
                            if ((now - lastDeviceCheck).TotalSeconds >= 3)
                            {
                                lastDeviceCheck = now;
                                string current = WavApi.GetDeviceId(device);
                                if (!string.Equals(current, devName, StringComparison.OrdinalIgnoreCase))
                                {
                                    Log.Info("Default audio device changed; restarting capture");
                                    break;
                                }
                            }
                            if (!gotData) Thread.Sleep(8);
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (_gate)
                        {
                            _lastError = ex.Message;
                            _active = false;
                            _running = true; // thread still alive/retrying
                        }
                        Log.Error("Audio capture error", ex);
                        Thread.Sleep(1500);
                    }
                    finally
                    {
                        try { if (client != null) client.Stop(); } catch { }
                        if (capture != null) Marshal.ReleaseComObject(capture);
                        if (client != null) Marshal.ReleaseComObject(client);
                        if (device != null) Marshal.ReleaseComObject(device);
                        if (enumerator != null) Marshal.ReleaseComObject(enumerator);
                        if (mixFormat != IntPtr.Zero) Marshal.FreeCoTaskMem(mixFormat);
                        if (pEnum != IntPtr.Zero) Marshal.Release(pEnum);
                    }
                }
            }
            finally
            {
                CoUninitialize();
                lock (_gate) _running = false;
            }
        }

        private void AppendSamples(IntPtr data, int frames, bool silent, WavApi.Format format)
        {
            int channels = format.Channels;
            int count = frames * channels;
            float[] mono = new float[frames];
            if (silent)
            {
                // leave zeros
            }
            else if (format.Bits == 32 && format.IsFloat)
            {
                var buffer = new float[count];
                Marshal.Copy(data, buffer, 0, count);
                for (int f = 0; f < frames; f++)
                {
                    float sum = 0;
                    for (int c = 0; c < channels; c++) sum += buffer[f * channels + c];
                    mono[f] = sum / channels;
                }
            }
            else if (format.Bits == 16)
            {
                var buffer = new short[count];
                Marshal.Copy(data, buffer, 0, count);
                for (int f = 0; f < frames; f++)
                {
                    float sum = 0;
                    for (int c = 0; c < channels; c++) sum += buffer[f * channels + c] / 32768f;
                    mono[f] = sum / channels;
                }
            }
            else if (format.Bits == 32)
            {
                var buffer = new int[count];
                Marshal.Copy(data, buffer, 0, count);
                for (int f = 0; f < frames; f++)
                {
                    float sum = 0;
                    for (int c = 0; c < channels; c++) sum += buffer[f * channels + c] / 2147483648f;
                    mono[f] = sum / channels;
                }
            }
            else return;

            for (int i = 0; i < mono.Length; i++)
            {
                _ring[_ringWrite] = mono[i];
                _ringWrite++;
                if (_ringWrite >= RingSize) _ringWrite = 0;
                if (_ringCount < RingSize) _ringCount++;
            }
        }

        private void Analyze()
        {
            float[] window = new float[FftSize];
            int available = Math.Min(_ringCount, FftSize);
            int start = _ringWrite - available;
            if (start < 0) start += RingSize;
            for (int i = 0; i < available; i++) window[i] = _ring[(start + i) % RingSize];
            double rms = 0;
            for (int i = 0; i < FftSize; i++) rms += window[i] * window[i];
            float level = (float)Math.Sqrt(rms / FftSize);

            float[] re = new float[FftSize];
            float[] im = new float[FftSize];
            Array.Copy(window, re, FftSize);
            Fft.Hann(re);
            Fft.Transform(re, im);

            float[] raw = new float[Bands];
            int rate = _sampleRate > 0 ? _sampleRate : 48000;
            double binHz = (double)rate / FftSize;
            const double lo = 40.0;
            const double hi = 16000.0;
            for (int b = 0; b < Bands; b++)
            {
                double f0 = lo * Math.Pow(hi / lo, b / (double)Bands);
                double f1 = lo * Math.Pow(hi / lo, (b + 1) / (double)Bands);
                int i0 = Math.Max(1, (int)Math.Floor(f0 / binHz));
                int i1 = Math.Min(FftSize / 2 - 1, (int)Math.Ceiling(f1 / binHz));
                if (i1 < i0) i1 = i0;
                float sum = 0;
                int n = 0;
                for (int i = i0; i <= i1 && i < FftSize / 2; i++)
                {
                    sum += (float)Math.Sqrt(re[i] * re[i] + im[i] * im[i]) / FftSize;
                    n++;
                }
                raw[b] = n > 0 ? sum / n : 0;
            }

            float maxRaw = 0.00001f;
            for (int b = 0; b < Bands; b++) if (raw[b] > maxRaw) maxRaw = raw[b];
            _agc = Math.Max(maxRaw, _agc * 0.985f);
            float gain = 1.0f / Math.Max(0.015f, _agc);

            float bass = 0;
            for (int b = 0; b < 5; b++) bass += raw[b];
            bass = bass / 5f * gain;

            for (int b = 0; b < Bands; b++)
            {
                float v = raw[b] * gain * 0.62f;
                if (v < 0.012f) v = 0;
                if (v > 1f) v = 1f;
                _smoothed[b] = Math.Max(v, _smoothed[b] * 0.86f + v * 0.14f);
            }
            _bass = Math.Min(1f, bass);
            _bassAverage = _bassAverage * 0.94f + _bass * 0.06f;
            bool beatNow = _bass > Math.Max(0.045f, _bassAverage * 1.32f) && (DateTime.UtcNow - _lastBeatUtc).TotalMilliseconds > 240;
            if (beatNow) { _lastBeatUtc = DateTime.UtcNow; _beat = 1f; }
            else _beat *= 0.90f;

            bool active = level > 0.0008f || (DateTime.UtcNow - _lastPacketUtc).TotalMilliseconds < 1200;
            lock (_gate)
            {
                Array.Copy(_smoothed, _published, Bands);
                _level = level;
                _active = active;
            }
        }

        public void Dispose()
        {
            Stop();
        }

        // --- COM interop ---

        [DllImport("ole32.dll")]
        private static extern int CoInitializeEx(IntPtr pvReserved, uint flags);
        [DllImport("ole32.dll")]
        private static extern void CoUninitialize();
        [DllImport("ole32.dll")]
        private static extern int CoCreateInstance(ref Guid clsid, IntPtr outer, uint clsctx, ref Guid iid, out IntPtr ppv);

        [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int flow, uint mask, out IntPtr devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
        }

        [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid, uint clsctx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object iface);
            [PreserveSig] int OpenPropertyStore(uint access, out IntPtr store);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
            [PreserveSig] int GetState(out uint state);
        }

        [ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioClient
        {
            [PreserveSig] int Initialize(int shareMode, uint streamFlags, long bufferDuration, long periodicity, IntPtr format, IntPtr sessionGuid);
            [PreserveSig] int GetBufferSize(out uint frames);
            [PreserveSig] int GetStreamLatency(out long latency);
            [PreserveSig] int GetCurrentPadding(out uint frames);
            [PreserveSig] int IsFormatSupported(int shareMode, IntPtr format, out IntPtr closest);
            [PreserveSig] int GetMixFormat(out IntPtr format);
            [PreserveSig] int GetDevicePeriod(out long defaultPeriod, out long minPeriod);
            [PreserveSig] int Start();
            [PreserveSig] int Stop();
            [PreserveSig] int Reset();
            [PreserveSig] int SetEventHandle(IntPtr handle);
            [PreserveSig] int GetService(ref Guid iid, out IntPtr service);
        }

        [ComImport, Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioCaptureClient
        {
            [PreserveSig] int GetBuffer(out IntPtr data, out uint frames, out uint flags, out long devicePosition, out long qpcPosition);
            [PreserveSig] int ReleaseBuffer(uint frames);
            [PreserveSig] int GetNextPacketSize(out uint frames);
        }

        private static class WavApi
        {
            [StructLayout(LayoutKind.Sequential, Pack = 2)]
            public struct WAVEFORMATEX
            {
                public ushort FormatTag;
                public ushort Channels;
                public uint SampleRate;
                public uint AvgBytesPerSec;
                public ushort BlockAlign;
                public ushort BitsPerSample;
                public ushort CbSize;
            }

            [StructLayout(LayoutKind.Sequential, Pack = 2)]
            public struct WAVEFORMATEXTENSIBLE
            {
                public WAVEFORMATEX Format;
                public ushort ValidBits;
                public uint ChannelMask;
                public Guid SubFormat;
            }

            public class Format
            {
                public int Channels;
                public uint SampleRate;
                public int Bits;
                public bool IsFloat;
            }

            public static Format ParseFormat(IntPtr ptr)
            {
                WAVEFORMATEX wf = (WAVEFORMATEX)Marshal.PtrToStructure(ptr, typeof(WAVEFORMATEX));
                var f = new Format();
                f.Channels = wf.Channels;
                f.SampleRate = wf.SampleRate;
                f.Bits = wf.BitsPerSample;
                f.IsFloat = wf.FormatTag == 3;
                if (wf.FormatTag == 0xFFFE && wf.CbSize >= 22)
                {
                    WAVEFORMATEXTENSIBLE ext = (WAVEFORMATEXTENSIBLE)Marshal.PtrToStructure(ptr, typeof(WAVEFORMATEXTENSIBLE));
                    f.IsFloat = ext.SubFormat == new Guid("00000003-0000-0010-8000-00AA00389B71");
                }
                if (f.Channels <= 0 || f.SampleRate == 0) return null;
                if (!(f.Bits == 16 || f.Bits == 32)) return null;
                return f;
            }

            public static string GetDeviceId(IMMDevice device)
            {
                try
                {
                    string id;
                    if (device.GetId(out id) >= 0) return id ?? "";
                }
                catch { }
                return "";
            }
        }
    }
}
