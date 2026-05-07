using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Edi.Core;
using Edi.Core.Device;
using Edi.Core.Device.Buttplug;
using Edi.Core.Device.EStim;
using Edi.Core.Device.Handy;
using Edi.Core.Device.Interfaces;
using Edi.Core.Device.OSR;
using Edi.Core.Gallery;
using Edi.Core.Gallery.Definition;

namespace Edi.Avalonia.Forms;

[PropertyChanged.DoNotNotify]
public partial class MainWindow : Window
{
    private readonly IEdi edi = App.Edi!;
    private readonly EdiConfig config;
    private readonly HandyConfig handyConfig;
    private readonly ButtplugConfig buttplugConfig;
    private readonly EStimConfig estimConfig;
    private readonly OSRConfig osrConfig;
    private Timer? timer;
    private bool launched;
    private SimulateGame? _simulateGame;

    private record AudioDevice(int id, string name);
    private record ComPort(string name, string? value);
    private GamesConfig gamesConfig;

    public MainWindow()
    {
        // The designer instantiates windows without running the app, so App.Edi is null.
        // Returning after InitializeComponent() gives the designer enough to render the XAML.
        if (Design.IsDesignMode)
        {
            InitializeComponent();
            return;
        }

        config      = edi.ConfigurationManager.Get<EdiConfig>();
        handyConfig = edi.ConfigurationManager.Get<HandyConfig>();
        buttplugConfig = edi.ConfigurationManager.Get<ButtplugConfig>();
        estimConfig = edi.ConfigurationManager.Get<EStimConfig>();
        osrConfig   = edi.ConfigurationManager.Get<OSRConfig>();
        gamesConfig = edi.ConfigurationManager.Get<GamesConfig>();

        var galleries = edi.Definitions.Where(x => x.Type != "filler").ToList();
        galleries.Insert(0, new DefinitionGallery { Name = "" });
        galleries.Insert(1, new DefinitionGallery { Name = "(Random)" });
        galleries.InsertRange(2, edi.Definitions.Where(x => x.Type == "filler"));

        // DataContext is like Django's template context — the window's XAML binds to these properties
        DataContext = new
        {
            config,
            handyConfig,
            buttplugConfig,
            estimConfig,
            osrConfig,
            devices  = edi.Devices,
            galleries,
        };

        InitializeComponent();

        edi.DeviceManager.OnloadDevice   += DeviceManager_OnloadDeviceAsync;
        edi.DeviceManager.OnUnloadDevice += DeviceManager_OnUnloadDevice;
        edi.OnChangeStatus += Edi_OnChangeStatus;

        timer = new Timer(RefreshGrid);
        timer.Change(3000, 3000);

        Closing += MainWindow_Closing;
        swaggerLink.PointerPressed += (_, e) =>
        {
            if (e.ClickCount == 2)
                Process.Start(new ProcessStartInfo
                {
                    FileName = "http://localhost:5000/swagger/index.html",
                    UseShellExecute = true
                });
        };
        Opened  += async (_, _) =>
        {
            await edi.Pause();
            await Task.Delay(1000);
            LoadForm();
        };
    }

    private void LoadForm()
    {
        // Populate game selector — each entry is a KeyValuePair<string,string> (name → path)
        var allGames = gamesConfig.GetAll();
        if (allGames.Count > 0)
        {
            cmbGame.ItemsSource = allGames.ToList();
            cmbGame.SelectedIndex = 0;
        }

        var audios = new List<AudioDevice> { new(-1, "None") };

        // WaveOut only exists in NAudio on Windows — the class isn't compiled into the Linux build
#if WINDOWS_BUILD
        try
        {
            for (int i = 0; i < NAudio.Wave.WaveOut.DeviceCount; i++)
                audios.Add(new AudioDevice(i, NAudio.Wave.WaveOut.GetCapabilities(i).ProductName));
        }
        catch { }
#endif

        audioDevicesComboBox.ItemsSource  = audios;
        audioDevicesComboBox.SelectedItem = audios.FirstOrDefault(a => a.id == estimConfig.DeviceId);

        LoadOsrPorts();

        DevicesGrid.ItemsSource = edi.Devices;
    }

    private void btnRescan_Click(object? sender, RoutedEventArgs e)
    {
        cmbGame.SelectionChanged -= Game_SelectionChanged;
        var games = gamesConfig.Rescan();
        cmbGame.ItemsSource = games.ToList();
        if (games.Count > 0) cmbGame.SelectedIndex = 0;
        cmbGame.SelectionChanged += Game_SelectionChanged;
    }

    private async void Game_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (cmbGame.SelectedItem is not KeyValuePair<string, string> game) return;
        await edi.Init(game.Value);
        RefreshGalleryList();
    }

    private void RefreshGalleryList()
    {
        var galleries = edi.Definitions.Where(x => x.Type != "filler").ToList();
        galleries.Insert(0, new DefinitionGallery { Name = "" });
        galleries.Insert(1, new DefinitionGallery { Name = "(Random)" });
        galleries.InsertRange(2, edi.Definitions.Where(x => x.Type == "filler"));
        cmbGallerie.ItemsSource = galleries;
        cmbGallerie.SelectedIndex = 0;
    }

    private void LoadOsrPorts()
    {
        var comPorts = new List<ComPort> { new("None", null) };
        try
        {
            foreach (var port in SerialPort.GetPortNames())
                comPorts.Add(new ComPort(port, port));
        }
        catch { }

        comPortsComboBox.ItemsSource  = comPorts;
        comPortsComboBox.SelectedItem = comPorts.FirstOrDefault(p => p.value == osrConfig.COMPort);
    }

    // Called every 3 seconds from a background thread — needs to hop to the UI thread
    private void RefreshGrid(object? state)
    {
        Dispatcher.UIThread.Post(async () =>
        {
            if (edi.DeviceManager.Devices.Any(x => x.IsReady)
                && !launched
                && !string.IsNullOrEmpty(config.ExecuteOnReady)
                && File.Exists(config.ExecuteOnReady))
            {
                launched = true;
                lblStatus.Content = "launched: " + config.ExecuteOnReady;
                Process.Start(new ProcessStartInfo(new FileInfo(config.ExecuteOnReady).FullName)
                    { UseShellExecute = true });
            }
        });
    }

    private void Edi_OnChangeStatus(string message)
    {
        Dispatcher.UIThread.Post(() => lblStatus.Content = message);
    }

    private async void DeviceManager_OnloadDeviceAsync(IDevice device, List<IDevice> devices)
    {
        await Task.Delay(500);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            DevicesGrid.ItemsSource = null;
            DevicesGrid.ItemsSource = edi.Devices;
        });
    }

    private async void DeviceManager_OnUnloadDevice(IDevice device, List<IDevice> devices)
    {
        await Task.Delay(1000);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            DevicesGrid.ItemsSource = null;
            DevicesGrid.ItemsSource = edi.Devices;
        });
    }

    // Variant combo inside the DataGrid — DataContext here is the IDevice row
    private void Variants_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.DataContext is IDevice device && cb.SelectedItem is string variant)
            edi.DeviceManager.SelectVariant(device, variant);
    }

    // Audio device and COM port combos feed back into config (no SelectedValuePath in Avalonia)
    private void AudioDevice_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (audioDevicesComboBox.SelectedItem is AudioDevice device)
            estimConfig.DeviceId = device.id;
    }

    private void ComPort_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (comPortsComboBox.SelectedItem is ComPort port)
            osrConfig.COMPort = port.value;
    }

    private async void ReloadButton_Click(object? sender, RoutedEventArgs e)
    {
        await edi.Init();
    }

    private async void RePackButton_Click(object? sender, RoutedEventArgs e)
    {
        await edi.Repack();
    }

    private async void ReconnectButton_ClickAsync(object? sender, RoutedEventArgs e)
    {
        LoadOsrPorts();
        await edi.InitDevices();
    }

    private async void btnPlay_Click(object? sender, RoutedEventArgs e)
    {
        var selected = (cmbGallerie.SelectedItem as DefinitionGallery)?.Name ?? "";
        if (selected == "(Random)")
            selected = edi.Definitions.OrderBy(_ => Guid.NewGuid()).FirstOrDefault()?.Name ?? "";
        await edi.Play(selected, 0);
    }

    private async void btnStop_Click(object? sender, RoutedEventArgs e)   => await edi.Stop();
    private async void btnPause_Click(object? sender, RoutedEventArgs e)  => await edi.Pause();
    private async void btnResume_Click(object? sender, RoutedEventArgs e) => await edi.Resume(false);

    private void btnSimulator_Click(object? sender, RoutedEventArgs e)
    {
        if (_simulateGame == null || !_simulateGame.IsVisible)
        {
            _simulateGame = new SimulateGame();
            _simulateGame.Closed += (_, _) => _simulateGame = null;
            _simulateGame.Show();
            _simulateGame.Activate();
        }
        else
        {
            _simulateGame.Close();
        }
    }

    private async void Slider_ValueChanged(object? sender, RangeBaseValueChangedEventArgs e)
    {
        await edi.Intensity(Convert.ToInt32(sliderIntensity.Value));
    }

    private async void MainWindow_Closing(object? sender, WindowClosingEventArgs e)
    {
        await edi.Pause();
        await Task.Delay(1000);
    }
}

// IValueConverter is the same concept as Django template filters —
// it transforms a raw value into a display value in the XAML template
public class BoolToReadyIconConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "✅" : "🚫";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
