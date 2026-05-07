#if !WINDOWS_BUILD
using LibVLCSharp.Shared;

namespace Edi.Core.Device.EStim
{
    public class LibVlcAudioOutput : IAudioOutput
    {
        private readonly LibVLC _libVlc;
        private readonly MediaPlayer _player;
        private float _volume = 1f;
        private string? _loadedPath;
        private long _loadedStartMs;

        public LibVlcAudioOutput(int deviceNumber)
        {
            DeviceNumber = deviceNumber;
            _libVlc = new LibVLC();
            _player = new MediaPlayer(_libVlc);
        }

        public int DeviceNumber { get; }

        public float Volume
        {
            get => _volume;
            set
            {
                _volume = value;
                // VLC volume is 0-100 int
                _player.Volume = (int)(value * 100);
            }
        }

        public void Load(string path, long startMs)
        {
            _loadedPath = path;
            _loadedStartMs = startMs;
        }

        public void Play()
        {
            if (_loadedPath == null) return;
            var media = new Media(_libVlc, _loadedPath, FromType.FromPath);
            // VLC `:start-time` is in seconds (float)
            media.AddOption($":start-time={(double)_loadedStartMs / 1000.0}");
            _player.Media = media;
            _player.Volume = (int)(_volume * 100);
            _player.Play();
        }

        public void Pause() => _player.Pause();
        public void Stop()  => _player.Stop();

        public void Dispose()
        {
            _player.Stop();
            _player.Dispose();
            _libVlc.Dispose();
        }
    }
}
#endif
