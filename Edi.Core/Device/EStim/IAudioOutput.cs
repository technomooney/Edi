namespace Edi.Core.Device.EStim
{
    public interface IAudioOutput : IDisposable
    {
        float Volume { get; set; }
        int DeviceNumber { get; }
        void Load(string path, long startMs);
        void Play();
        void Pause();
        void Stop();
    }
}
