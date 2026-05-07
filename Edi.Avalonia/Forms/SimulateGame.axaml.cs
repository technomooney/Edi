using Avalonia.Controls;
using Avalonia.Interactivity;
using Edi.Core;
using Edi.Core.Device.Simulator;
using Edi.Core.Gallery.Funscript;

namespace Edi.Avalonia.Forms;

[PropertyChanged.DoNotNotify]
public partial class SimulateGame : Window
{
    private readonly IEdi edi = App.Edi!;
    private SimulatorDevice? simulatorDevice;

    public SimulateGame()
    {
        if (Design.IsDesignMode)
        {
            InitializeComponent();
            return;
        }

        InitializeComponent();

        simulatorDevice = new SimulatorDevice(edi.GetRepository<FunscriptRepository>(), edi.Logger);
        DataContext = new { SimulatorDevice = simulatorDevice };

        Loaded  += SimulateGame_Loaded;
        Closing += SimulateGame_Closing;
    }

    private void SimulateGame_Loaded(object? sender, RoutedEventArgs e)
    {
        edi.DeviceManager.LoadDevice(simulatorDevice!);
    }

    private void SimulateGame_Closing(object? sender, WindowClosingEventArgs e)
    {
        simulatorDevice?.StopGallery();
        if (simulatorDevice != null)
            edi.DeviceManager.UnloadDevice(simulatorDevice);
        simulatorDevice = null;
    }
}
