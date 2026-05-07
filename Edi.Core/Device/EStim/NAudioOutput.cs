#if WINDOWS_BUILD
using NAudio.Wave;

namespace Edi.Core.Device.EStim
{
    public class NAudioOutput : IAudioOutput
    {
        private readonly WaveOutEvent _waveOut;
        // Mp3FileReader is not seekable after Play() without re-opening, so cache one per path
        private readonly Dictionary<string, Mp3FileReader> _readers = new();
        private Mp3FileReader? _current;

        public NAudioOutput(int deviceNumber)
        {
            _waveOut = new WaveOutEvent { DeviceNumber = deviceNumber };
            DeviceNumber = deviceNumber;
        }

        public int DeviceNumber { get; }

        public float Volume
        {
            get => _waveOut.Volume;
            set => _waveOut.Volume = value;
        }

        public void Load(string path, long startMs)
        {
            if (!_readers.TryGetValue(path, out var reader))
            {
                reader = new Mp3FileReader(path);
                _readers[path] = reader;
            }
            reader.CurrentTime = TimeSpan.FromMilliseconds(startMs);
            _current = reader;
            _waveOut.Stop();
            _waveOut.Init(reader);
        }

        public void Play()  => _waveOut.Play();
        public void Pause() => _waveOut.Pause();
        public void Stop()  => _waveOut.Stop();

        public void Dispose()
        {
            _waveOut.Dispose();
            foreach (var r in _readers.Values) r.Dispose();
            _readers.Clear();
        }
    }
}
#endif
