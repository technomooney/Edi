using Edi.Core.Device.Interfaces;
using Edi.Core.Gallery.EStimAudio;
using Edi.Core.Services;
using Microsoft.Extensions.Logging;

namespace Edi.Core.Device.EStim
{
    public class EStimProvider : IDeviceProvider
    {
        private readonly ILogger _logger;
        private readonly List<EStimDevice> _devices = new();

        public EStimProvider(AudioRepository audioRepository, ConfigurationManager config, DeviceCollector deviceCollector, ILogger<EStimProvider> logger)
        {
            Config = config.Get<EStimConfig>();
            DeviceCollector = deviceCollector;
            AudioRepository = audioRepository;
            _logger = logger;

            _logger.LogInformation($"EStimProvider initialized with Config: {Config.DeviceId}");
        }

        public EStimConfig Config { get; }
        public DeviceCollector DeviceCollector { get; }
        public AudioRepository AudioRepository { get; }

        public async Task Init()
        {
            _logger.LogInformation("Initialization started.");

            foreach (var eStimDevice in _devices)
            {
                _logger.LogInformation($"Unloading device: {eStimDevice}");
                DeviceCollector.UnloadDevice(eStimDevice);
            }
            _devices.Clear();

            if (Config.DeviceId == -1)
            {
                _logger.LogWarning("DeviceId is set to -1. Initialization will be skipped.");
                return;
            }

            try
            {
                IAudioOutput output = CreateAudioOutput(Config.DeviceId);
                var device = new EStimDevice(AudioRepository, output, _logger);

                DeviceCollector.LoadDevice(device);
                _devices.Add(device);

                _logger.LogInformation($"Device loaded successfully: {device}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error initializing device with DeviceId {Config.DeviceId}: {ex.Message}");
            }
        }

        private static IAudioOutput CreateAudioOutput(int deviceNumber)
        {
#if WINDOWS_BUILD
            return new NAudioOutput(deviceNumber);
#else
            return new LibVlcAudioOutput(deviceNumber);
#endif
        }
    }
}
