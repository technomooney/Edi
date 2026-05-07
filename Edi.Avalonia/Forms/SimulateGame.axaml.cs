using Avalonia.Controls;
using Avalonia.Interactivity;
using Edi.Core;
using Edi.Core.Device.Simulator;
using Edi.Core.Gallery.Funscript;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Edi.Avalonia.Forms;

[PropertyChanged.DoNotNotify]
public partial class SimulateGame : Window
{
    private readonly IEdi edi = App.Edi!;
    private PreviewDevice? previewDevice;

    public SimulateGame()
    {
        if (Design.IsDesignMode)
        {
            InitializeComponent();
            return;
        }

        InitializeComponent();

        var repo    = edi.repos.OfType<FunscriptRepository>().First();
        var logger  = Program.AppHost!.Services.GetRequiredService<ILoggerFactory>().CreateLogger<PreviewDevice>();
        previewDevice = new PreviewDevice(repo, logger);
        DataContext = new { SimulatorDevice = previewDevice };

        Loaded  += SimulateGame_Loaded;
        Closing += SimulateGame_Closing;
    }

    private void SimulateGame_Loaded(object? sender, RoutedEventArgs e)
    {
        edi.DeviceCollector.LoadDevice(previewDevice!);
    }

    private void SimulateGame_Closing(object? sender, WindowClosingEventArgs e)
    {
        previewDevice?.StopGallery();
        if (previewDevice != null)
            edi.DeviceCollector.UnloadDevice(previewDevice);
        previewDevice = null;
    }
}
