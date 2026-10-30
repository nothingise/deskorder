using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using DeskOrder.Interop;

namespace DeskOrder.Core
{
    public class TrackInfo
    {
        public string Path { get; set; }
        public string Title { get; set; }
        public string Artist { get; set; }
        public string Album { get; set; }
        public double Duration { get; set; }
        public BitmapSource Cover { get; set; }
        public bool MetadataLoaded { get; set; }

        public TrackInfo()
        {
            Path = "";
            Title = "";
            Artist = "";
            Album = "";
            Duration = 0;
            MetadataLoaded = false;
        }

        public string DisplayTitle
        {
            get
            {
                if (!string.IsNullOrEmpty(Title)) return Title;
                return Path == null ? "" : System.IO.Path.GetFileNameWithoutExtension(Path);
            }
        }
    }

    public class PlaylistFile
    {
        public List<string> Paths { get; set; }
        public PlaylistFile() { Paths = new List<string>(); }
    }

    public class PlayerService : IDisposable
    {
        private readonly AppSettings _settings;
        private readonly MciPlayer _mci;
        private readonly List<TrackInfo> _tracks = new List<TrackInfo>();
        private readonly Dispatcher _dispatcher;
        private readonly DispatcherTimer _timer;
        private readonly MetadataWorker _metadata;
        private int _index = -1;
        private bool _isPlaying;
        private double _duration;
        private double _position;
        private DateTime _playStartedUtc = DateTime.MinValue;
        private string _lastError = "";
        private bool _disposed;

        public event EventHandler Changed;
        public event EventHandler TrackChanged;
        public event EventHandler StateChanged;
        public event EventHandler PositionChanged;

        public PlayerService(AppSettings settings, Dispatcher dispatcher)
        {
            _settings = settings;
            _dispatcher = dispatcher;
            _mci = new MciPlayer();
            _timer = new DispatcherTimer(DispatcherPriority.Background, dispatcher);
            _timer.Interval = TimeSpan.FromMilliseconds(220);
            _timer.Tick += delegate { Tick(); };
            _timer.Start();
            _metadata = new MetadataWorker(delegate (TrackInfo info)
            {
                _dispatcher.BeginInvoke(new Action(delegate { OnMetadataReady(info); }));
            });
            LoadPlaylist();
        }

        public IList<TrackInfo> Tracks { get { return _tracks; } }
        public int Index { get { return _index; } }
        public TrackInfo Current { get { return _index >= 0 && _index < _tracks.Count ? _tracks[_index] : null; } }
        public bool IsPlaying { get { return _isPlaying; } }
        public double Duration { get { return _duration; } }
        public double Position { get { return _position; } }
        public string LastError { get { return _lastError; } }
        public bool Shuffle { get { return _settings.Music.Shuffle; } }
        public string Repeat { get { return _settings.Music.Repeat; } }
        public double Volume { get { return _settings.Music.Volume; } }

        private void LoadPlaylist()
        {
            try
            {
                PlaylistFile file = Json.Read<PlaylistFile>(AppPaths.PlaylistFile);
                if (file != null && file.Paths != null)
                {
                    foreach (string p in file.Paths)
                        if (File.Exists(p) && IsAudio(p)) _tracks.Add(CreateTrack(p));
                }
                if (_tracks.Count == 0)
                {
                    string folder = Environment.GetFolderPath(Environment.SpecialFolder.MyMusic);
                    if (Directory.Exists(folder))
                    {
                        foreach (string p in Directory.GetFiles(folder).Where(IsAudio).OrderBy(delegate (string x) { return x; }).Take(12))
                            _tracks.Add(CreateTrack(p));
                    }
                }
                string last = _settings.Music.LastTrack;
                if (!string.IsNullOrEmpty(last))
                {
                    _index = _tracks.FindIndex(delegate (TrackInfo t) { return string.Equals(t.Path, last, StringComparison.OrdinalIgnoreCase); });
                }
                if (_index < 0 && _tracks.Count > 0) _index = 0;
                foreach (TrackInfo t in _tracks) _metadata.Enqueue(t.Path);
                SavePlaylist();
            }
            catch (Exception ex) { Log.Error("Load playlist", ex); }
        }

        private TrackInfo CreateTrack(string path)
        {
            var t = new TrackInfo();
            t.Path = path;
            t.Title = System.IO.Path.GetFileNameWithoutExtension(path);
            return t;
        }

        public static bool IsAudio(string path)
        {
            string ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            return ext == ".mp3" || ext == ".wav" || ext == ".wma";
        }

        public int AddFiles(IEnumerable<string> paths)
        {
            int added = 0;
            if (paths == null) return 0;
            foreach (string path in paths)
            {
                if (!File.Exists(path) || !IsAudio(path)) continue;
                if (_tracks.Any(delegate (TrackInfo t) { return string.Equals(t.Path, path, StringComparison.OrdinalIgnoreCase); })) continue;
                _tracks.Add(CreateTrack(path));
                _metadata.Enqueue(path);
                added++;
            }
            if (added > 0)
            {
                if (_index < 0) _index = 0;
                SavePlaylist();
                RaiseChanged();
            }
            return added;
        }

        public int AddFolder(string folder)
        {
            try
            {
                if (!Directory.Exists(folder)) return 0;
                var files = Directory.GetFiles(folder).Where(IsAudio).OrderBy(delegate (string p) { return p; }).ToArray();
                return AddFiles(files);
            }
            catch (Exception ex) { Log.Error("Add music folder", ex); return 0; }
        }

        public void RemoveAt(int index)
        {
            if (index < 0 || index >= _tracks.Count) return;
            bool wasCurrent = index == _index;
            _tracks.RemoveAt(index);
            if (wasCurrent)
            {
                _mci.Close();
                _isPlaying = false;
                _index = _tracks.Count == 0 ? -1 : Math.Min(index, _tracks.Count - 1);
                _position = 0;
                _duration = 0;
                RaiseTrackChanged();
            }
            else if (index < _index) _index--;
            SavePlaylist();
            RaiseChanged();
        }

        public void Clear()
        {
            _mci.Close();
            _isPlaying = false;
            _tracks.Clear();
            _index = -1;
            _position = 0;
            _duration = 0;
            SavePlaylist();
            RaiseTrackChanged();
            RaiseChanged();
        }

        public void PlayIndex(int index)
        {
            if (index < 0 || index >= _tracks.Count) return;
            try
            {
                _index = index;
                TrackInfo track = _tracks[_index];
                _mci.Close();
                if (!_mci.Open(track.Path))
                {
                    _lastError = _mci.LastError;
                    _isPlaying = false;
                    Log.Warn("Cannot open track: " + _lastError + " " + track.Path);
                    RaiseStateChanged();
                    RaiseChanged();
                    return;
                }
                _mci.SetVolume(_settings.Music.Volume);
                _mci.Play();
                _isPlaying = true;
                _playStartedUtc = DateTime.UtcNow;
                _duration = _mci.LengthMs > 0 ? _mci.LengthMs / 1000.0 : track.Duration;
                _position = 0;
                _lastError = "";
                _settings.Music.LastTrack = track.Path;
                SettingsStore.Save(_settings);
                RaiseTrackChanged();
                RaiseChanged();
            }
            catch (Exception ex)
            {
                _lastError = ex.Message;
                Log.Error("PlayIndex", ex);
                _isPlaying = false;
                RaiseChanged();
            }
        }

        public void Toggle()
        {
            if (_tracks.Count == 0) return;
            if (_isPlaying)
            {
                if (_mci.Pause()) { _isPlaying = false; RaiseStateChanged(); }
                return;
            }
            if (_mci.IsOpen && _index >= 0)
            {
                if (_mci.Resume()) { _isPlaying = true; _playStartedUtc = DateTime.UtcNow; RaiseStateChanged(); }
                else PlayIndex(_index);
            }
            else PlayIndex(_index >= 0 ? _index : 0);
        }

        public void Next(bool manual)
        {
            if (_tracks.Count == 0) return;
            if (!manual && _settings.Music.Repeat == "one")
            {
                PlayIndex(_index);
                return;
            }
            if (!manual && _settings.Music.Repeat == "off" && _index >= _tracks.Count - 1)
            {
                _isPlaying = false;
                _mci.Stop();
                RaiseStateChanged();
                return;
            }
            int next;
            if (_settings.Music.Shuffle && _tracks.Count > 1)
            {
                var rnd = new Random();
                do { next = rnd.Next(_tracks.Count); } while (next == _index);
            }
            else next = (_index + 1) % _tracks.Count;
            PlayIndex(next);
        }

        public void Previous()
        {
            if (_tracks.Count == 0) return;
            int prev = (_index - 1 + _tracks.Count) % _tracks.Count;
            PlayIndex(prev);
        }

        public void Seek(double seconds)
        {
            if (Current == null) return;
            if (seconds < 0) seconds = 0;
            if (_duration > 0 && seconds > _duration) seconds = _duration;
            if (_mci.Seek((long)Math.Round(seconds * 1000)))
            {
                _position = seconds;
                RaisePositionChanged();
            }
        }

        public void SetVolume(double volume)
        {
            if (volume < 0) volume = 0;
            if (volume > 1) volume = 1;
            _settings.Music.Volume = volume;
            _mci.SetVolume(volume);
            SettingsStore.Save(_settings);
            RaiseChanged();
        }

        public void SetShuffle(bool value)
        {
            _settings.Music.Shuffle = value;
            SettingsStore.Save(_settings);
            RaiseChanged();
        }

        public void SetRepeat(string value)
        {
            if (value != "all" && value != "one" && value != "off") value = "all";
            _settings.Music.Repeat = value;
            SettingsStore.Save(_settings);
            RaiseChanged();
        }

        private void Tick()
        {
            if (_disposed) return;
            try
            {
                if (!_isPlaying || !_mci.IsOpen) return;
                double pos = _mci.PositionMs() / 1000.0;
                if (pos >= 0) _position = pos;
                if (_mci.LengthMs > 0) _duration = _mci.LengthMs / 1000.0;
                string mode = _mci.Mode();
                if (mode == "stopped" && (DateTime.UtcNow - _playStartedUtc).TotalMilliseconds > 600)
                {
                    _isPlaying = false;
                    _position = _duration;
                    RaisePositionChanged();
                    RaiseStateChanged();
                    Next(false);
                    return;
                }
                RaisePositionChanged();
            }
            catch { }
        }

        private void OnMetadataReady(TrackInfo info)
        {
            if (info == null) return;
            TrackInfo existing = _tracks.FirstOrDefault(delegate (TrackInfo t) { return string.Equals(t.Path, info.Path, StringComparison.OrdinalIgnoreCase); });
            if (existing == null) return;
            existing.Title = string.IsNullOrEmpty(info.Title) ? existing.Title : info.Title;
            existing.Artist = info.Artist;
            existing.Album = info.Album;
            existing.Duration = info.Duration > 0 ? info.Duration : existing.Duration;
            existing.Cover = info.Cover;
            existing.MetadataLoaded = true;
            RaiseChanged();
            if (existing == Current) RaiseTrackChanged();
        }

        public void SavePlaylist()
        {
            try
            {
                var f = new PlaylistFile();
                f.Paths = _tracks.Select(delegate (TrackInfo t) { return t.Path; }).ToList();
                Json.Write(AppPaths.PlaylistFile, f);
            }
            catch (Exception ex) { Log.Error("Save playlist", ex); }
        }

        private void RaiseChanged() { EventHandler h = Changed; if (h != null) h(this, EventArgs.Empty); }
        private void RaiseTrackChanged() { EventHandler h = TrackChanged; if (h != null) h(this, EventArgs.Empty); RaiseStateChanged(); }
        private void RaiseStateChanged() { EventHandler h = StateChanged; if (h != null) h(this, EventArgs.Empty); }
        private void RaisePositionChanged() { EventHandler h = PositionChanged; if (h != null) h(this, EventArgs.Empty); }

        public void Dispose()
        {
            _disposed = true;
            try { _timer.Stop(); } catch { }
            try { _mci.Dispose(); } catch { }
            try { _metadata.Dispose(); } catch { }
        }

        private sealed class MetadataWorker : IDisposable
        {
            private readonly BlockingCollection<string> _queue = new BlockingCollection<string>();
            private readonly Thread _thread;
            private readonly Action<TrackInfo> _callback;
            private volatile bool _disposed;

            public MetadataWorker(Action<TrackInfo> callback)
            {
                _callback = callback;
                _thread = new Thread(Loop);
                _thread.IsBackground = true;
                _thread.Name = "DeskOrder.Metadata";
                _thread.SetApartmentState(ApartmentState.STA);
                _thread.Start();
            }

            public void Enqueue(string path)
            {
                if (string.IsNullOrEmpty(path) || _disposed) return;
                try { _queue.Add(path); } catch { }
            }

            private void Loop()
            {
                try { CoInitializeEx(IntPtr.Zero, 0x2); } catch { }
                object shell = null;
                try { shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application")); } catch { }
                try
                {
                    foreach (string path in _queue.GetConsumingEnumerable())
                    {
                        if (_disposed) break;
                        TrackInfo info = new TrackInfo();
                        info.Path = path;
                        info.Title = System.IO.Path.GetFileNameWithoutExtension(path);
                        try
                        {
                            if (File.Exists(path))
                            {
                                if (shell != null)
                                {
                                    dynamic app = shell;
                                    dynamic folder = app.Namespace(System.IO.Path.GetDirectoryName(path));
                                    dynamic item = folder.ParseName(System.IO.Path.GetFileName(path));
                                    string title = Convert.ToString(item.ExtendedProperty("System.Music.Title"));
                                    string artist = Convert.ToString(item.ExtendedProperty("System.Music.Artist"));
                                    string album = Convert.ToString(item.ExtendedProperty("System.Music.AlbumTitle"));
                                    object dur = item.ExtendedProperty("System.Media.Duration");
                                    if (!string.IsNullOrEmpty(title)) info.Title = title;
                                    info.Artist = artist;
                                    info.Album = album;
                                    long ticks;
                                    if (dur != null && long.TryParse(Convert.ToString(dur), out ticks) && ticks > 0)
                                        info.Duration = ticks / 10000000.0;
                                }
                                info.Cover = IconService.GetThumbnail(path, 256);
                            }
                        }
                        catch (Exception ex) { Log.Warn("Metadata failed for " + path + ": " + ex.Message); }
                        info.MetadataLoaded = true;
                        if (!_disposed) { try { _callback(info); } catch { } }
                    }
                }
                finally
                {
                    if (shell != null) try { Marshal.ReleaseComObject(shell); } catch { }
                    try { CoUninitialize(); } catch { }
                }
            }

            [DllImport("ole32.dll")]
            private static extern int CoInitializeEx(IntPtr pv, uint flags);
            [DllImport("ole32.dll")]
            private static extern void CoUninitialize();

            public void Dispose()
            {
                _disposed = true;
                try { _queue.CompleteAdding(); } catch { }
                try { _thread.Join(800); } catch { }
            }
        }
    }
}
