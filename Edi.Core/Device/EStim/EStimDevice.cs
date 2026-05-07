using Edi.Core.Gallery.EStimAudio;
using Edi.Core.Gallery;
using System.Timers;
using Timer = System.Timers.Timer;
using System.Threading;
using PropertyChanged;
using Microsoft.Extensions.Logging;

namespace Edi.Core.Device.EStim
{
    [AddINotifyPropertyChangedInterface]
    public class EStimDevice : DeviceBase<AudioRepository, AudioGallery>
    {
        private readonly AudioRepository _repository;
        private readonly IAudioOutput _audioOutput;

        public EStimDevice(AudioRepository repository, IAudioOutput audioOutput, ILogger logger) : base(repository, logger)
        {
            Name = $"EStim ({audioOutput.DeviceNumber})";
            _repository = repository;
            _audioOutput = audioOutput;
        }

        internal override Task applyRange()
        {
            _audioOutput.Volume = Max / 100f;
            return Task.CompletedTask;
        }

        public override async Task PlayGallery(AudioGallery gallery, long seek = 0)
        {
            _audioOutput.Load(gallery.AudioPath, gallery.StartTime + seek);
            _audioOutput.Play();
        }

        public override async Task StopGallery()
        {
            _audioOutput.Pause();
        }
    }
}
